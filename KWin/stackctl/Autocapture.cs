using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Xml.Linq;

namespace StackCtl;

internal sealed partial class StackCtlApp
{
    int CmdAutocapture(string[] a)
    {
        if (a.Length == 0) return Fail("autocapture: missing action (status/enable/disable/add-ignore)");
        var action = a[0];
        var reload = a.Contains("--reload");
        var behaviors = "/run/media/system/OS/Users/lost/AppData/Local/Lost Tech LLC/Stack/Behaviors.xml";
        var groups = "/run/media/system/OS/Users/lost/AppData/Local/Lost Tech LLC/Stack/WindowGroups.xml";
        var defaultZoneId = "main";
        var title = "";
        var @class = "";
        var match = "Anywhere";

        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] == "--behaviors" && i + 1 < a.Length) { behaviors = a[++i]; continue; }
            if (a[i] == "--groups" && i + 1 < a.Length) { groups = a[++i]; continue; }
            if (a[i] == "--default-zone" && i + 1 < a.Length) { defaultZoneId = a[++i]; continue; }
            if (a[i] == "--title" && i + 1 < a.Length) { title = a[++i]; continue; }
            if (a[i] == "--class" && i + 1 < a.Length) { @class = a[++i]; continue; }
            if (a[i] == "--match" && i + 1 < a.Length) { match = a[++i]; continue; }
        }

        return action switch
        {
            "status" => CmdAutocaptureStatus(),
            "disable" => CmdAutocaptureDisable(reload),
            "enable" => CmdAutocaptureEnableFromWindows(behaviors, groups, defaultZoneId, reload),
            "add-ignore" => CmdAutocaptureAddIgnore(title, @class, match, reload),
            _ => Fail($"autocapture: unknown action: {action}")
        };
    }

    int CmdAutocaptureStatus()
    {
        Console.WriteLine($"AutoCaptureOnNewWindow={GetKwinrcValue("Script-losttech.stack", "AutoCaptureOnNewWindow") ?? ""}");
        Console.WriteLine($"AutoCaptureDefaultZoneId={GetKwinrcValue("Script-losttech.stack", "AutoCaptureDefaultZoneId") ?? ""}");
        var raw = GetKwinrcValue("Script-losttech.stack", "AutoCaptureIgnoreFilters") ?? "";
        Console.WriteLine($"AutoCaptureIgnoreFilters={(string.IsNullOrWhiteSpace(raw) ? "" : "(set)")}");
        return 0;
    }

    int CmdAutocaptureDisable(bool reload)
    {
        SetKwinrcValue("Script-losttech.stack", "AutoCaptureOnNewWindow", "false");
        if (reload) ReloadKWin();
        Console.WriteLine("ok");
        return 0;
    }

    int CmdAutocaptureEnableFromWindows(string behaviorsXmlPath, string windowGroupsXmlPath, string defaultZoneId, bool reload)
    {
        if (!File.Exists(behaviorsXmlPath))
            return Fail($"autocapture enable: Behaviors.xml not found: {behaviorsXmlPath}");
        if (!File.Exists(windowGroupsXmlPath))
            return Fail($"autocapture enable: WindowGroups.xml not found: {windowGroupsXmlPath}");

        var ignoreGroupNames = ReadCaptureIgnoreGroupNamesFromBehaviors(behaviorsXmlPath);
        var ignoreFilters = ResolveWindowGroupFilters(windowGroupsXmlPath, ignoreGroupNames);

        var minified = JsonSerializer.Serialize(ignoreFilters, new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        });

        SetKwinrcValue("Script-losttech.stack", "AutoCaptureOnNewWindow", "true");
        SetKwinrcValue("Script-losttech.stack", "AutoCaptureDefaultZoneId", defaultZoneId);
        SetKwinrcValue("Script-losttech.stack", "AutoCaptureIgnoreFilters", minified);

        if (reload) ReloadKWin();
        Console.WriteLine($"ok (ignoreGroups={ignoreGroupNames.Count}, ignoreFilters={ignoreFilters.Count})");
        return 0;
    }

    int CmdAutocaptureAddIgnore(string title, string @class, string match, bool reload)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(@class))
            return Fail("autocapture add-ignore: missing --title <text> or --class <text>");

        match = string.IsNullOrWhiteSpace(match) ? "Anywhere" : match.Trim();
        if (match is not ("Anywhere" or "Prefix" or "Suffix" or "Exact"))
            return Fail("autocapture add-ignore: --match must be one of: Anywhere|Prefix|Suffix|Exact");

        var filters = ReadIgnoreFiltersFromKwinrc();
        AutoCaptureClause? titleClause = string.IsNullOrWhiteSpace(title) ? null : new AutoCaptureClause(title.Trim(), match);
        AutoCaptureClause? classClause = string.IsNullOrWhiteSpace(@class) ? null : new AutoCaptureClause(@class.Trim(), match);
        var newFilter = new AutoCaptureIgnoreFilter(titleClause, classClause, App: null);

        var exists = filters.Any(f =>
            string.Equals(f.Title?.Value, titleClause?.Value, StringComparison.Ordinal) &&
            string.Equals(f.Title?.Match, titleClause?.Match, StringComparison.Ordinal) &&
            string.Equals(f.Class?.Value, classClause?.Value, StringComparison.Ordinal) &&
            string.Equals(f.Class?.Match, classClause?.Match, StringComparison.Ordinal) &&
            f.App is null);

        if (!exists)
            filters.Add(newFilter);

        WriteIgnoreFiltersToKwinrc(filters);
        if (reload) ReloadKWin();
        Console.WriteLine(exists ? "ok (already present)" : "ok");
        return 0;
    }

    List<AutoCaptureIgnoreFilter> ReadIgnoreFiltersFromKwinrc()
    {
        var raw = GetKwinrcValue("Script-losttech.stack", "AutoCaptureIgnoreFilters") ?? "";
        if (string.IsNullOrWhiteSpace(raw)) return new List<AutoCaptureIgnoreFilter>();
        try
        {
            return JsonSerializer.Deserialize<List<AutoCaptureIgnoreFilter>>(raw, _jsonOpts)
                   ?? new List<AutoCaptureIgnoreFilter>();
        }
        catch
        {
            return new List<AutoCaptureIgnoreFilter>();
        }
    }

    void WriteIgnoreFiltersToKwinrc(List<AutoCaptureIgnoreFilter> filters)
    {
        var minified = JsonSerializer.Serialize(filters, new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        });
        SetKwinrcValue("Script-losttech.stack", "AutoCaptureIgnoreFilters", minified);
    }

    List<string> ReadCaptureIgnoreGroupNamesFromBehaviors(string behaviorsXmlPath)
    {
        var doc = XDocument.Load(behaviorsXmlPath);
        var list = doc.Descendants()
            .Where(x => x.Name.LocalName == "CaptureIgnoreList")
            .Descendants()
            .Where(x => x.Name.LocalName == "string")
            .Select(x => (x.Value ?? "").Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return list;
    }

    List<AutoCaptureIgnoreFilter> ResolveWindowGroupFilters(string windowGroupsXmlPath, IReadOnlyCollection<string> groupNames)
    {
        var doc = XDocument.Load(windowGroupsXmlPath);
        var groups = doc.Descendants().Where(x => x.Name.LocalName == "WindowGroup");

        var wanted = new HashSet<string>(groupNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var outFilters = new List<AutoCaptureIgnoreFilter>();

        foreach (var g in groups)
        {
            var name = g.Attribute("Name")?.Value?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!wanted.Contains(name)) continue;

            foreach (var f in g.Elements().Where(x => x.Name.LocalName == "Filter"))
            {
                AutoCaptureClause? TitleClause() => ClauseFromXml(f.Elements().FirstOrDefault(x => x.Name.LocalName == "TitleFilter"));
                AutoCaptureClause? ClassClause() => ClauseFromXml(f.Elements().FirstOrDefault(x => x.Name.LocalName == "ClassFilter"));
                AutoCaptureClause? ProcClause() => ClauseFromXml(f.Elements().FirstOrDefault(x => x.Name.LocalName == "ProcessFilter"));

                var title = TitleClause();
                var cls = ClassClause();
                var proc = ProcClause();

                if (title is null && cls is null && proc is null) continue;

                outFilters.Add(new AutoCaptureIgnoreFilter(title, cls, proc));
            }
        }

        return outFilters;
    }

    AutoCaptureClause? ClauseFromXml(XElement? el)
    {
        if (el is null) return null;
        var value = (el.Attribute("Value")?.Value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value)) return null;
        var match = (el.Attribute("Match")?.Value ?? "Anywhere").Trim();
        if (string.IsNullOrWhiteSpace(match)) match = "Anywhere";
        return new AutoCaptureClause(value, match);
    }
}
