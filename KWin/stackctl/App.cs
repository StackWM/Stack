using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

#pragma warning disable IL2026, IL3050

namespace StackCtl;

internal sealed partial class StackCtlApp
{
    private const string ScriptId = "losttech.stack";
    private const string KWinService = "org.kde.KWin";
    private const string KWinObject = "/KWin";
    private const string KWinScriptingObject = "/Scripting";
    private const string KWinScriptingIface = "org.kde.kwin.Scripting";
    private const string DisabledLayoutId = "disabled";

    private readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            return args[0] switch
            {
                "ping" => CmdPing(),
                "status" => CmdStatus(),
                "layout" => CmdLayout(args.Skip(1).ToArray()),
                "map" => CmdMap(args.Skip(1).ToArray()),
                "autocapture" => CmdAutocapture(args.Skip(1).ToArray()),
                "shortcuts" => CmdShortcuts(args.Skip(1).ToArray()),
                "script" => CmdScript(args.Skip(1).ToArray()),
                "layouts" => CmdLayouts(),
                "zones" => CmdZones(args.Skip(1).ToArray()),
                "screens" => CmdScreens(args.Skip(1).ToArray()),
                "import" => CmdImport(args.Skip(1).ToArray()),
                "apply-kwinrc" => CmdApplyKwinrc(args.Skip(1).ToArray()),
                "dev" => CmdDev(args.Skip(1).ToArray()),
                "reload" => CmdReload(),
                _ => Fail($"Unknown command: {args[0]}")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

int CmdPing()
{
    Console.WriteLine(IsScriptLoaded() ? "ok" : "not-loaded");
    return 0;
}

int CmdStatus()
{
    Console.WriteLine($"scriptLoaded={IsScriptLoaded()}");
    Console.WriteLine($"layoutId={GetKwinrcValue("Script-losttech.stack", "LayoutId") ?? ""}");
    Console.WriteLine($"defaultLayoutId={GetKwinrcValue("Script-losttech.stack", "DefaultLayoutId") ?? ""}");
    var map = GetKwinrcValue("Script-losttech.stack", "ScreenLayoutMap") ?? "";
    Console.WriteLine($"screenLayoutMap={(string.IsNullOrWhiteSpace(map) ? "" : "(set)")}");
    return 0;
}

int CmdLayout(string[] a)
{
    if (a.Length == 0) return Fail("layout: missing action (get/set)");
    return a[0] switch
    {
        "get" => Print(GetKwinrcValue("Script-losttech.stack", "LayoutId") ?? ""),
        "set" => a.Length < 2 ? Fail("layout set: missing <layoutId>") : SetLayoutAndMaybeReload(a[1], reload: a.Contains("--reload")),
        _ => Fail($"layout: unknown action: {a[0]}")
    };
}

int CmdMap(string[] a)
{
    if (a.Length == 0) return Fail("map: missing action (list/set/set-output/disable/disable-output/clear)");
    var action = a[0];
    var reload = a.Contains("--reload");

    switch (action)
    {
        case "list":
        {
            var asJson = a.Contains("--json");
            var map = ReadScreenLayoutMapFromKwinrc();
            if (asJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(map, _jsonOpts));
                return 0;
            }
            foreach (var kv in map.OrderBy(k => k.Key, StringComparer.Ordinal))
                Console.WriteLine($"{kv.Key} -> {kv.Value}");
            return 0;
        }
        case "set":
        {
            if (a.Length < 3) return Fail("map set: usage: stackctl map set <WxH@X,Y> <layoutId> [--reload]");
            var key = a[1];
            var layoutId = a[2];
            EnsureLayoutExists(layoutId);
            var map = ReadScreenLayoutMapFromKwinrc();
            map[key] = layoutId;
            WriteScreenLayoutMapToKwinrc(map);
            if (reload) ReloadKWin();
            Console.WriteLine("ok");
            return 0;
        }
        case "set-output":
        {
            if (a.Length < 3) return Fail("map set-output: usage: stackctl map set-output <outputName> <layoutId> [--reload]");
            var output = a[1];
            var layoutId = a[2];
            EnsureLayoutExists(layoutId);
            var screens = GetScreensFromKscreenDoctor();
            var s = screens.FirstOrDefault(x => string.Equals(x.Name, output, StringComparison.Ordinal));
            if (s is null) return Fail($"map set-output: unknown output: {output} (see: stackctl screens)");
            var map = ReadScreenLayoutMapFromKwinrc();
            map[s.Key] = layoutId;
            WriteScreenLayoutMapToKwinrc(map);
            if (screens.Count > 0)
                WriteScreenKeysToKwinrc(screens.Select(x => x.Key).ToArray());
            if (reload) ReloadKWin();
            Console.WriteLine($"ok {s.Key}");
            return 0;
        }
        case "disable":
        {
            if (a.Length < 2) return Fail("map disable: usage: stackctl map disable <WxH@X,Y> [--reload]");
            var key = a[1];
            var map = ReadScreenLayoutMapFromKwinrc();
            map[key] = DisabledLayoutId;
            WriteScreenLayoutMapToKwinrc(map);
            if (reload) ReloadKWin();
            Console.WriteLine("ok");
            return 0;
        }
        case "disable-output":
        {
            if (a.Length < 2) return Fail("map disable-output: usage: stackctl map disable-output <outputName> [--reload]");
            var output = a[1];
            var screens = GetScreensFromKscreenDoctor();
            var s = screens.FirstOrDefault(x => string.Equals(x.Name, output, StringComparison.Ordinal));
            if (s is null) return Fail($"map disable-output: unknown output: {output} (see: stackctl screens)");
            var map = ReadScreenLayoutMapFromKwinrc();
            map[s.Key] = DisabledLayoutId;
            WriteScreenLayoutMapToKwinrc(map);
            if (screens.Count > 0)
                WriteScreenKeysToKwinrc(screens.Select(x => x.Key).ToArray());
            if (reload) ReloadKWin();
            Console.WriteLine($"ok {s.Key}");
            return 0;
        }
        case "clear":
        {
            if (a.Length < 2) return Fail("map clear: usage: stackctl map clear <WxH@X,Y> [--reload]");
            var key = a[1];
            var map = ReadScreenLayoutMapFromKwinrc();
            _ = map.Remove(key);
            WriteScreenLayoutMapToKwinrc(map);
            if (reload) ReloadKWin();
            Console.WriteLine("ok");
            return 0;
        }
        default:
            return Fail($"map: unknown action: {action}");
    }
}

int CmdLayouts()
{
    foreach (var l in LoadLayouts())
        Console.WriteLine($"{l.Id}: {l.Name}");
    return 0;
}

int CmdShortcuts(string[] a)
{
    if (a.Length == 0) return Fail("shortcuts: missing action (win-arrows/status/import-windows)");
    var action = a[0];
    var restart = a.Contains("--restart");
    var behaviors = "/run/media/system/OS/Users/lost/AppData/Local/Lost Tech LLC/Stack/Behaviors.xml";
    for (var i = 0; i < a.Length; i++)
    {
        if (a[i] == "--behaviors" && i + 1 < a.Length) { behaviors = a[++i]; continue; }
    }

    return action switch
    {
        "status" => CmdShortcutsStatus(),
        "win-arrows" => CmdShortcutsWinArrows(restart),
        "import-windows" => CmdShortcutsImportWindows(behaviors, restart),
        _ => Fail($"shortcuts: unknown action: {action}")
    };
}

int CmdShortcutsStatus()
{
    var path = KglobalShortcutsPath();
    if (!File.Exists(path)) return Fail($"shortcuts: not found: {path}");

    var kwin = ReadIniGroup(path, "kwin");
    string Get(string k) => kwin.TryGetValue(k, out var v) ? v : "";

    Console.WriteLine($"kglobalshortcutsrc={path}");
    Console.WriteLine($"StackMoveLeft={Get("StackMoveLeft")}");
    Console.WriteLine($"StackMoveRight={Get("StackMoveRight")}");
    Console.WriteLine($"StackMoveUp={Get("StackMoveUp")}");
    Console.WriteLine($"StackMoveDown={Get("StackMoveDown")}");
    Console.WriteLine($"StackDetach={Get("StackDetach")}");
    Console.WriteLine($"StackCaptureAll={Get("StackCaptureAll")}");
    Console.WriteLine($"StackReloadConfig={Get("StackReloadConfig")}");
    Console.WriteLine($"StackSelectLayout={Get("StackSelectLayout")}");
    Console.WriteLine($"Window Quick Tile Left={Get("Window Quick Tile Left")}");
    Console.WriteLine($"Window Quick Tile Right={Get("Window Quick Tile Right")}");
    Console.WriteLine($"Window Quick Tile Top={Get("Window Quick Tile Top")}");
    Console.WriteLine($"Window Quick Tile Bottom={Get("Window Quick Tile Bottom")}");

    var busctl = Which("busctl");
    if (busctl is not null)
    {
        var qtLeft = GetShortcutLive(busctl, new[] { "kwin", "Window Quick Tile Left", "KWin", "Quick Tile Window to the Left" });
        var stackLeft = GetShortcutLive(busctl, new[] { "kwin", "StackMoveLeft", "KWin", "Stack: Move active window left" });
        Console.WriteLine($"live(Window Quick Tile Left)={FormatShortcutLive(qtLeft)}");
        Console.WriteLine($"live(StackMoveLeft)={FormatShortcutLive(stackLeft)}");
    }
    return 0;
}

int CmdShortcutsWinArrows(bool restart)
{
    var path = KglobalShortcutsPath();
    if (!File.Exists(path)) return Fail($"shortcuts: not found: {path}");

    BackupFile(path, suffix: "stackbak");

    // Disable KWin tiling shortcuts and map Stack move to Win+Arrows.
    var updates = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Use "none,none" to persistently override application defaults on restart.
        ["Window Quick Tile Left"] = "none,none,Quick Tile Window to the Left",
        ["Window Quick Tile Right"] = "none,none,Quick Tile Window to the Right",
        ["Window Quick Tile Top"] = "none,none,Quick Tile Window to the Top",
        ["Window Quick Tile Bottom"] = "none,none,Quick Tile Window to the Bottom",

        ["StackMoveLeft"] = "Meta+Left,Meta+Left,Stack: Move active window left",
        ["StackMoveRight"] = "Meta+Right,Meta+Right,Stack: Move active window right",
        ["StackMoveUp"] = "Meta+Up,Meta+Up,Stack: Move active window up",
        ["StackMoveDown"] = "Meta+Down,Meta+Down,Stack: Move active window down",
        ["StackDetach"] = "Meta+Escape,Meta+Escape,Stack: Detach active window",
    };

    PatchIniGroup(path, "kwin", updates);

    // On some Plasma Wayland setups, global shortcuts are hosted by KWin and won't
    // pick up kglobalshortcutsrc edits reliably. Apply live via DBus when possible.
    TryApplyWindowsDefaultsLive(
        moveArrows: true,
        detach: true,
        captureAll: false,
        captureActive: false,
        reloadConfig: false,
        selectLayout: false,
        disableQuickTile: true
    );

    if (restart)
    {
        RestartKglobalAccel();
        Console.WriteLine("ok (restarted plasma-kglobalaccel)");
        return 0;
    }

    Console.WriteLine("ok");
    Console.WriteLine("Note: if changes don’t apply immediately, run: ./KWin/bin/stackctl script reload");
    return 0;
}

int CmdScript(string[] a)
{
    if (a.Length == 0) return Fail("script: missing action (status/reload)");
    return a[0] switch
    {
        "status" => Print(IsScriptLoaded() ? "loaded" : "not-loaded"),
        "reload" => CmdScriptReload(),
        _ => Fail($"script: unknown action: {a[0]}")
    };
}

int CmdScriptReload()
{
    var qdbus = FindQdbus();
    var scriptPath = InstalledScriptMainPath();
    if (!File.Exists(scriptPath)) return Fail($"script reload: not found: {scriptPath} (run: ./KWin/install.sh)");

    _ = Run(new List<string> { qdbus, KWinService, KWinScriptingObject, $"{KWinScriptingIface}.unloadScript", ScriptId });
    _ = Run(new List<string> { qdbus, KWinService, KWinScriptingObject, $"{KWinScriptingIface}.loadScript", scriptPath, ScriptId });
    _ = Run(new List<string> { qdbus, KWinService, KWinScriptingObject, $"{KWinScriptingIface}.start" });

    Console.WriteLine(IsScriptLoaded() ? "ok" : "failed");
    return 0;
}

int CmdShortcutsImportWindows(string behaviorsXmlPath, bool restart)
{
    if (!File.Exists(behaviorsXmlPath))
        return Fail($"shortcuts import-windows: Behaviors.xml not found: {behaviorsXmlPath}");

    var path = KglobalShortcutsPath();
    if (!File.Exists(path)) return Fail($"shortcuts: not found: {path}");

    BackupFile(path, suffix: "stackbak");

    var doc = XDocument.Load(behaviorsXmlPath);
    var binds = doc.Descendants().Where(x => x.Name.LocalName == "CommandKeyBinding").ToList();

    var cmdToAction = new Dictionary<string, (string Action, string Description)>(StringComparer.OrdinalIgnoreCase)
    {
        ["Move window up"] = ("StackMoveUp", "Stack: Move active window up"),
        ["Move window down"] = ("StackMoveDown", "Stack: Move active window down"),
        ["Move window left"] = ("StackMoveLeft", "Stack: Move active window left"),
        ["Move window right"] = ("StackMoveRight", "Stack: Move active window right"),
        ["Detach window, and restore its bounds"] = ("StackDetach", "Stack: Detach active window"),
        ["Capture all windows"] = ("StackCaptureAll", "Stack: Capture all windows"),
        ["Reload Layouts"] = ("StackReloadConfig", "Stack: Reload config"),
        ["Select Layout"] = ("StackSelectLayout", "Stack: Cycle layout (current screen)"),
    };

    var updates = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Always disable KWin tiling so Meta+Arrows are owned by Stack.
        ["Window Quick Tile Left"] = "none,none,Quick Tile Window to the Left",
        ["Window Quick Tile Right"] = "none,none,Quick Tile Window to the Right",
        ["Window Quick Tile Top"] = "none,none,Quick Tile Window to the Top",
        ["Window Quick Tile Bottom"] = "none,none,Quick Tile Window to the Bottom",
    };

    foreach (var b in binds)
    {
        var cmd = b.Elements().FirstOrDefault(e => e.Name.LocalName == "CommandName")?.Value?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(cmd)) continue;
        if (!cmdToAction.TryGetValue(cmd, out var map)) continue;

        var key = b.Descendants().FirstOrDefault(e => e.Name.LocalName == "Key")?.Value?.Trim() ?? "";
        var mods = b.Descendants().FirstOrDefault(e => e.Name.LocalName == "Modifiers")?.Value?.Trim() ?? "";
        var accel = KdeAccelFromWindows(mods, key);
        if (string.IsNullOrWhiteSpace(accel)) continue;

        updates[map.Action] = $"{accel},{accel},{map.Description}";
    }

    PatchIniGroup(path, "kwin", updates);

    // Apply live via DBus, resolving conflicts (e.g. Meta+Escape).
    TryApplyWindowsDefaultsLive(
        moveArrows: true,
        detach: true,
        captureAll: true,
        captureActive: true,
        reloadConfig: true,
        selectLayout: true,
        disableQuickTile: true
    );

    if (restart)
    {
        RestartKglobalAccel();
        Console.WriteLine("ok (restarted plasma-kglobalaccel)");
        return 0;
    }

    Console.WriteLine("ok");
    Console.WriteLine("Note: if changes don’t apply immediately, run: ./KWin/bin/stackctl script reload");
    return 0;
}

string KdeAccelFromWindows(string windowsMods, string key)
{
    if (string.IsNullOrWhiteSpace(key)) return "";

    var parts = new List<string>();
    var mods = (windowsMods ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
    foreach (var m in mods)
    {
        if (m.Equals("Windows", StringComparison.OrdinalIgnoreCase)) parts.Add("Meta");
        else if (m.Equals("Control", StringComparison.OrdinalIgnoreCase) || m.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) parts.Add("Ctrl");
        else if (m.Equals("Alt", StringComparison.OrdinalIgnoreCase)) parts.Add("Alt");
        else if (m.Equals("Shift", StringComparison.OrdinalIgnoreCase)) parts.Add("Shift");
    }

    // Normalize some key names.
    var k = key switch
    {
        "Esc" => "Escape",
        _ => key
    };

    if (parts.Count == 0) return k;
    return string.Join('+', parts) + "+" + k;
}

void TryApplyWindowsDefaultsLive(
    bool moveArrows,
    bool detach,
    bool captureAll,
    bool captureActive,
    bool reloadConfig,
    bool selectLayout,
    bool disableQuickTile)
{
    var busctl = Which("busctl");
    if (busctl is null) return;

    // desired keycodes (Qt-style OR of modifiers + key)
    const int Meta = unchecked((int)0x1000_0000);
    const int Ctrl = 0x0400_0000;
    const int KeyEscape = 0x0100_0000;
    const int KeyLeft = 0x0100_0012;
    const int KeyUp = 0x0100_0013;
    const int KeyRight = 0x0100_0014;
    const int KeyDown = 0x0100_0015;

    int KeyLetter(char c) => char.ToUpperInvariant(c);

    var desired = new List<(string Unique, string Description, int KeyCode)>();
    if (moveArrows)
    {
        desired.Add(("StackMoveLeft", "Stack: Move active window left", Meta | KeyLeft));
        desired.Add(("StackMoveRight", "Stack: Move active window right", Meta | KeyRight));
        desired.Add(("StackMoveUp", "Stack: Move active window up", Meta | KeyUp));
        desired.Add(("StackMoveDown", "Stack: Move active window down", Meta | KeyDown));
    }
    if (detach) desired.Add(("StackDetach", "Stack: Detach active window", Meta | KeyEscape));
    if (captureAll) desired.Add(("StackCaptureAll", "Stack: Capture all windows", Meta | Ctrl | KeyLetter('J')));
    if (captureActive) desired.Add(("StackCaptureActive", "Stack: Capture active window", Meta | Ctrl | KeyLetter('K')));
    if (reloadConfig) desired.Add(("StackReloadConfig", "Stack: Reload config", Meta | Ctrl | KeyLetter('R')));
    if (selectLayout) desired.Add(("StackSelectLayout", "Stack: Cycle layout (current screen)", Meta | Ctrl | KeyLetter('L')));

    if (disableQuickTile)
    {
        // free Meta+Arrow from KWin tiling
        SetForeignShortcut(busctl, new[] { "kwin", "Window Quick Tile Left", "KWin", "Quick Tile Window to the Left" }, Array.Empty<int>());
        SetForeignShortcut(busctl, new[] { "kwin", "Window Quick Tile Right", "KWin", "Quick Tile Window to the Right" }, Array.Empty<int>());
        SetForeignShortcut(busctl, new[] { "kwin", "Window Quick Tile Top", "KWin", "Quick Tile Window to the Top" }, Array.Empty<int>());
        SetForeignShortcut(busctl, new[] { "kwin", "Window Quick Tile Bottom", "KWin", "Quick Tile Window to the Bottom" }, Array.Empty<int>());
    }

    foreach (var d in desired)
    {
        // Unbind any other action currently owning this keycode.
        var owner = GetActionForKey(busctl, d.KeyCode);
        if (owner.Length == 4 && !(owner[0] == "kwin" && owner[1] == d.Unique))
        {
            SetForeignShortcut(busctl, owner, Array.Empty<int>());
        }

        SetForeignShortcut(busctl, new[] { "kwin", d.Unique, "KWin", d.Description }, new[] { d.KeyCode });
    }
}

string[] GetActionForKey(string busctl, int keyCode)
{
    var (code, stdout, _stderr) = Run(new List<string>
    {
        busctl, "--user", "call", "org.kde.kglobalaccel", "/kglobalaccel", "org.kde.KGlobalAccel", "action", "i", keyCode.ToString()
    });
    if (code != 0) return Array.Empty<string>();

    // stdout formats like:
    //   as 4 "kwin" "Window Quick Tile Left" "KWin" "Quick Tile Window to the Left"
    // or:
    //   as 0
    var rx = new Regex("^as\\s+(\\d+)(.*)$", RegexOptions.Singleline);
    var m = rx.Match(stdout.Trim());
    if (!m.Success) return Array.Empty<string>();
    var n = int.Parse(m.Groups[1].Value);
    if (n == 0) return Array.Empty<string>();
    if (n != 4) return Array.Empty<string>();
    var rest = m.Groups[2].Value;
    var strings = Regex.Matches(rest, "\"([^\"]*)\"").Select(mm => mm.Groups[1].Value).ToArray();
    return strings.Length == 4 ? strings : Array.Empty<string>();
}

void SetForeignShortcut(string busctl, string[] actionId, int[] keys)
{
    if (actionId.Length != 4) throw new Exception("SetForeignShortcut: actionId must have 4 strings");
    var argv = new List<string>
    {
        busctl, "--user", "call", "org.kde.kglobalaccel", "/kglobalaccel", "org.kde.KGlobalAccel", "setForeignShortcut", "asai",
        "4", actionId[0], actionId[1], actionId[2], actionId[3],
        keys.Length.ToString()
    };
    foreach (var k in keys) argv.Add(k.ToString());
    var (code, _stdout, stderr) = Run(argv);
    if (code != 0) throw new Exception($"Failed to set shortcut {actionId[0]}:{actionId[1]}: {stderr.Trim()}");
}

int[] GetShortcutLive(string busctl, string[] actionId)
{
    var (code, stdout, _stderr) = Run(new List<string>
    {
        busctl, "--user", "call", "org.kde.kglobalaccel", "/kglobalaccel", "org.kde.KGlobalAccel", "shortcut", "as",
        "4", actionId[0], actionId[1], actionId[2], actionId[3]
    });
    if (code != 0) return Array.Empty<int>();

    // stdout formats like:
    //   ai 0
    //   ai 1 285212690
    var m = Regex.Match(stdout.Trim(), "^ai\\s+(\\d+)(.*)$", RegexOptions.Singleline);
    if (!m.Success) return Array.Empty<int>();
    var n = int.Parse(m.Groups[1].Value);
    if (n == 0) return Array.Empty<int>();
    var nums = Regex.Matches(m.Groups[2].Value, "(-?\\d+)").Select(mm => int.Parse(mm.Groups[1].Value)).ToArray();
    return nums.Length == n ? nums : nums.Take(n).ToArray();
}

string FormatShortcutLive(int[] keys)
{
    if (keys.Length == 0) return "none";
    if (keys.Length == 1) return FormatKeySequence(keys[0]);
    return string.Join(", ", keys.Select(FormatKeySequence));
}

string FormatKeySequence(int code)
{
    // This matches how Qt encodes QKeySequence (modifier bits OR'd with key).
    var mods = new List<string>();
    if ((code & unchecked((int)0x1000_0000)) != 0) mods.Add("Meta");
    if ((code & 0x0400_0000) != 0) mods.Add("Ctrl");
    if ((code & 0x0800_0000) != 0) mods.Add("Alt");
    if ((code & 0x0200_0000) != 0) mods.Add("Shift");

    var key = code & 0x01FF_FFFF;
    var keyName = key switch
    {
        0x0100_0000 => "Escape",
        0x0100_0012 => "Left",
        0x0100_0013 => "Up",
        0x0100_0014 => "Right",
        0x0100_0015 => "Down",
        _ when key is >= 0x20 and <= 0x7E => ((char)key).ToString(),
        _ => $"0x{key:X}"
    };

    if (mods.Count == 0) return keyName;
    return string.Join('+', mods) + "+" + keyName;
}

int CmdZones(string[] a)
{
    if (a.Length == 0) return Fail("zones: missing <layoutId> (see: stackctl layouts)");
    var layoutId = a[0];
    var asJson = a.Contains("--json");

    var layout = LoadLayouts().FirstOrDefault(l => l.Id == layoutId);
    if (layout is null) return Fail($"zones: unknown layoutId: {layoutId}");

    if (asJson)
    {
        Console.WriteLine(JsonSerializer.Serialize(layout.Zones, _jsonOpts));
        return 0;
    }

    foreach (var z in layout.Zones)
    {
        var drop = z.IsDropZone ? " drop" : "";
        var target = string.IsNullOrWhiteSpace(z.TargetZoneId) ? "" : $" -> {z.TargetZoneId}";
        Console.WriteLine($"{z.Id}{drop}{target}  mode={z.Mode}");
    }
    return 0;
}

int CmdScreens(string[] a)
{
    var asJson = a.Contains("--json");
    var screens = GetScreensFromKscreenDoctor();
    if (asJson)
    {
        Console.WriteLine(JsonSerializer.Serialize(screens, _jsonOpts));
        return 0;
    }
    foreach (var s in screens) Console.WriteLine($"{s.Name}: {s.Key} ({s.Geometry})");
    return 0;
}

int CmdImport(string[] a)
{
    var layoutMap = "/run/media/system/OS/Users/lost/AppData/Local/Lost Tech LLC/Stack/LayoutMap.xml";
    var outPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/kwin-stack/config.json");
    var apply = true;
    var reload = false;

    for (var i = 0; i < a.Length; i++)
    {
        if (a[i] == "--layoutmap" && i + 1 < a.Length) { layoutMap = a[++i]; continue; }
        if (a[i] == "--out" && i + 1 < a.Length) { outPath = a[++i]; continue; }
        if (a[i] == "--no-apply-kwinrc") { apply = false; continue; }
        if (a[i] == "--reload") { reload = true; continue; }
    }

    var mapped = ImportLayoutMap(layoutMap, outPath);
    Console.WriteLine($"Wrote {outPath}");
    Console.WriteLine($"Mapped {mapped} screen entries");

    if (apply)
    {
        ApplyKwinrcFromConfigJson(outPath);
        Console.WriteLine("Applied to kwinrc");
        if (reload)
        {
            ReloadKWin();
            Console.WriteLine("Reloaded KWin");
        }
    }
    return 0;
}

int CmdApplyKwinrc(string[] a)
{
    var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/kwin-stack/config.json");
    var reload = false;
    for (var i = 0; i < a.Length; i++)
    {
        if (a[i] == "--config" && i + 1 < a.Length) { configPath = a[++i]; continue; }
        if (a[i] == "--reload") { reload = true; continue; }
    }

    ApplyKwinrcFromConfigJson(configPath);
    if (reload) ReloadKWin();
    Console.WriteLine("ok");
    return 0;
}

int CmdReload()
{
    ReloadKWin();
    Console.WriteLine("ok");
    return 0;
}

int Print(string s)
{
    Console.WriteLine(s);
    return 0;
}

int Fail(string msg)
{
    Console.Error.WriteLine(msg);
    Console.Error.WriteLine("Run: stackctl --help");
    return 2;
}

void PrintHelp()
{
    Console.WriteLine(
        """
        stackctl - helper for the KWin Stack prototype

        Usage:
          stackctl ping
          stackctl status
          stackctl layout get
          stackctl layout set <layoutId> [--reload]
          stackctl map list [--json]
          stackctl map set <WxH@X,Y> <layoutId> [--reload]
          stackctl map set-output <outputName> <layoutId> [--reload]
          stackctl map disable <WxH@X,Y> [--reload]
          stackctl map disable-output <outputName> [--reload]
          stackctl map clear <WxH@X,Y> [--reload]
          stackctl autocapture status
          stackctl autocapture enable [--behaviors <Behaviors.xml>] [--groups <WindowGroups.xml>] [--default-zone <zoneId>] [--reload]
          stackctl autocapture disable [--reload]
          stackctl autocapture add-ignore [--title <text>] [--class <text>] [--match Anywhere|Prefix|Suffix|Exact] [--reload]
          stackctl shortcuts status
          stackctl shortcuts win-arrows [--restart]
          stackctl shortcuts import-windows [--behaviors <Behaviors.xml>] [--restart]
          stackctl script status
          stackctl script reload
          stackctl dev watch [--log <path>] [--debounce <ms>] [--no-install] [--no-reload] [--once]
          stackctl layouts
          stackctl zones <layoutId> [--json]
          stackctl screens [--json]

          # tooling
          stackctl import [--layoutmap <LayoutMap.xml>] [--out <config.json>] [--no-apply-kwinrc] [--reload]
          stackctl apply-kwinrc [--config <config.json>] [--reload]
          stackctl reload

        Notes:
          - Tabs/cycling/capture are handled by KWin shortcuts registered by the script.
          - Avoid calling org.kde.KWin.queryWindowInfo from scripts; it’s interactive and can feel like a freeze.
        """
    );
}

string? FindRepoRoot()
{
    var d = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (d is not null)
    {
        if (File.Exists(Path.Combine(d.FullName, "Stack.sln")) && Directory.Exists(Path.Combine(d.FullName, "KWin")))
            return d.FullName;
        d = d.Parent;
    }
    return null;
}

bool IsScriptLoaded()
{
    var qdbus = FindQdbus();
    var argv = new List<string> { qdbus, KWinService, KWinScriptingObject, $"{KWinScriptingIface}.isScriptLoaded", ScriptId };
    var (code, stdout, _stderr) = Run(argv);
    if (code != 0) return false;
    return stdout.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
}

string InstalledScriptMainPath()
{
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    return Path.Combine(home, ".local/share/kwin/scripts", ScriptId, "contents/code/main.js");
}

int SetLayoutAndMaybeReload(string layoutId, bool reload)
{
    SetKwinrcValue("Script-losttech.stack", "LayoutId", layoutId);
    if (reload) ReloadKWin();
    Console.WriteLine(layoutId);
    return 0;
}

void ReloadKWin()
{
    var qdbus = FindQdbus();
    _ = Run(new List<string> { qdbus, KWinService, KWinObject, "org.kde.KWin.reconfigure" });
}

void EnsureLayoutExists(string layoutId)
{
    var layouts = LoadLayouts();
    if (layouts.Any(l => l.Id == layoutId)) return;
    throw new Exception($"Unknown layoutId: {layoutId} (see: stackctl layouts)");
}

Dictionary<string, string> ReadScreenLayoutMapFromKwinrc()
{
    var raw = GetKwinrcValue("Script-losttech.stack", "ScreenLayoutMap");
    if (string.IsNullOrWhiteSpace(raw)) return new Dictionary<string, string>(StringComparer.Ordinal);
    try
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(raw, _jsonOpts)
               ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
    catch
    {
        return new Dictionary<string, string>(StringComparer.Ordinal);
    }
}

void WriteScreenLayoutMapToKwinrc(Dictionary<string, string> map)
{
    var minified = JsonSerializer.Serialize(map, new JsonSerializerOptions
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    });
    SetKwinrcValue("Script-losttech.stack", "ScreenLayoutMap", minified);
}

void WriteScreenKeysToKwinrc(string[] keys)
{
    var minified = JsonSerializer.Serialize(keys, new JsonSerializerOptions
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    });
    SetKwinrcValue("Script-losttech.stack", "ScreenKeys", minified);
}

string FindQdbus()
{
    foreach (var cand in new[] { "qdbus6", "qdbus" })
    {
        var p = Which(cand);
        if (p is not null) return p;
    }
    throw new Exception("stackctl: qdbus (or qdbus6) not found on PATH");
}

(int ExitCode, string Stdout, string Stderr) Run(List<string> argv, string? workdir = null)
{
    var psi = new ProcessStartInfo
    {
        FileName = argv[0],
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    for (var i = 1; i < argv.Count; i++) psi.ArgumentList.Add(argv[i]);
    if (!string.IsNullOrWhiteSpace(workdir))
        psi.WorkingDirectory = workdir;

    using var p = Process.Start(psi) ?? throw new Exception($"Failed to start: {argv[0]}");
    var stdout = p.StandardOutput.ReadToEnd();
    var stderr = p.StandardError.ReadToEnd();
    p.WaitForExit();
    return (p.ExitCode, stdout, stderr);
}

string? Which(string exe)
{
    var path = Environment.GetEnvironmentVariable("PATH") ?? "";
    foreach (var dir in path.Split(Path.PathSeparator))
    {
        if (string.IsNullOrWhiteSpace(dir)) continue;
        try
        {
            var full = Path.Combine(dir, exe);
            if (File.Exists(full)) return full;
        }
        catch { }
    }
    return null;
}

string KwinrcPath()
{
    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/kwinrc");
}

string KglobalShortcutsPath()
{
    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/kglobalshortcutsrc");
}

void BackupFile(string path, string suffix)
{
    var ts = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
    var backup = $"{path}.{suffix}-{ts}";
    File.Copy(path, backup, overwrite: false);
}

Dictionary<string, string> ReadIniGroup(string path, string group)
{
    var map = new Dictionary<string, string>(StringComparer.Ordinal);
    string? currentGroup = null;
    foreach (var line in File.ReadLines(path, Encoding.UTF8))
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
        {
            currentGroup = trimmed[1..^1];
            continue;
        }
        if (!string.Equals(currentGroup, group, StringComparison.Ordinal)) continue;
        if (string.IsNullOrWhiteSpace(trimmed)) continue;
        if (trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith(";", StringComparison.Ordinal)) continue;
        var eq = trimmed.IndexOf('=', StringComparison.Ordinal);
        if (eq <= 0) continue;
        var key = trimmed[..eq];
        var val = trimmed[(eq + 1)..];
        map[key] = val;
    }
    return map;
}

void PatchIniGroup(string path, string group, Dictionary<string, string> updates)
{
    var lines = File.ReadAllLines(path, Encoding.UTF8).ToList();
    var header = $"[{group}]";
    var start = lines.FindIndex(l => l.Trim() == header);
    if (start < 0) throw new Exception($"Group not found in {path}: {header}");

    var end = start + 1;
    for (; end < lines.Count; end++)
    {
        var t = lines[end].Trim();
        if (t.StartsWith("[") && t.EndsWith("]")) break;
    }

    var seen = new HashSet<string>(StringComparer.Ordinal);
    for (var i = start + 1; i < end; i++)
    {
        var raw = lines[i];
        var trimmed = raw.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) continue;
        var eq = trimmed.IndexOf('=', StringComparison.Ordinal);
        if (eq <= 0) continue;
        var key = trimmed[..eq];
        if (!updates.TryGetValue(key, out var newVal)) continue;
        lines[i] = $"{key}={newVal}";
        seen.Add(key);
    }

    // Insert any missing keys at end of group, before next header.
    var insertAt = end;
    foreach (var kv in updates)
    {
        if (seen.Contains(kv.Key)) continue;
        lines.Insert(insertAt, $"{kv.Key}={kv.Value}");
        insertAt++;
    }

    File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
}

void RestartKglobalAccel()
{
    var exe = Which("systemctl");
    if (exe is null) throw new Exception("systemctl not found on PATH");
    var (code, _stdout, stderr) = Run(new List<string> { exe, "--user", "restart", "plasma-kglobalaccel.service" });
    if (code != 0) throw new Exception($"Failed to restart plasma-kglobalaccel.service: {stderr.Trim()}");
}

string? GetKwinrcValue(string group, string key)
{
    var path = KwinrcPath();
    if (!File.Exists(path)) return null;

    string? currentGroup = null;
    foreach (var line in File.ReadLines(path, Encoding.UTF8))
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
        {
            currentGroup = trimmed[1..^1];
            continue;
        }
        if (!string.Equals(currentGroup, group, StringComparison.Ordinal)) continue;
        if (trimmed.StartsWith($"{key}=", StringComparison.Ordinal))
            return trimmed[(key.Length + 1)..];
    }
    return null;
}

void SetKwinrcValue(string group, string key, string value)
{
    var path = KwinrcPath();
    var lines = File.Exists(path) ? File.ReadAllLines(path, Encoding.UTF8).ToList() : new List<string>();

    var groupHeader = $"[{group}]";
    var groupStart = lines.FindIndex(l => l.Trim() == groupHeader);
    if (groupStart < 0)
    {
        if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1])) lines.Add("");
        lines.Add(groupHeader);
        lines.Add($"{key}={value}");
        File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
        return;
    }

    var end = groupStart + 1;
    for (; end < lines.Count; end++)
    {
        var t = lines[end].Trim();
        if (t.StartsWith("[") && t.EndsWith("]")) break;
    }

    for (var i = groupStart + 1; i < end; i++)
    {
        var t = lines[i].TrimStart();
        if (t.StartsWith($"{key}=", StringComparison.Ordinal))
        {
            lines[i] = $"{key}={value}";
            File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
            return;
        }
    }

    lines.Insert(end, $"{key}={value}");
    File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
}

void ApplyKwinrcFromConfigJson(string configPath)
{
    if (!File.Exists(configPath))
        throw new Exception($"Config not found: {configPath}");

    using var doc = JsonDocument.Parse(File.ReadAllText(configPath, Encoding.UTF8));
    var root = doc.RootElement;

    var defaultLayoutId = root.TryGetProperty("defaultLayoutId", out var d) ? (d.GetString() ?? "") : "";
    var screenLayoutMap = root.TryGetProperty("screenLayoutMap", out var m) ? m : default;

    if (!string.IsNullOrWhiteSpace(defaultLayoutId))
        SetKwinrcValue("Script-losttech.stack", "DefaultLayoutId", defaultLayoutId);

    if (screenLayoutMap.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
    {
        var minified = JsonSerializer.Serialize(screenLayoutMap, new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        });
        SetKwinrcValue("Script-losttech.stack", "ScreenLayoutMap", minified);
    }

    try
    {
        var screens = GetScreensFromKscreenDoctor();
        if (screens.Count > 0)
            WriteScreenKeysToKwinrc(screens.Select(s => s.Key).ToArray());
    }
    catch { }
}

int ImportLayoutMap(string layoutMapXmlPath, string outConfigPath)
{
    if (!File.Exists(layoutMapXmlPath))
        throw new Exception($"LayoutMap.xml not found: {layoutMapXmlPath}");

    var xamlToLayout = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Horizontal+Info.xaml"] = "horizontal-info",
        ["OOB Horizontal.xaml"] = "oob-horizontal",
        ["Large Horizontal Left - Customized.xaml"] = "large-horizontal-left-customized",
        ["Large Horizontal Left.xaml"] = "large-horizontal-left",
        ["Large Horizontal Right.xaml"] = "large-horizontal-right",
    };

    var doc = XDocument.Load(layoutMapXmlPath);
    var pairs = doc.Descendants().Where(x => x.Name.LocalName == "MutableKeyValuePairOfStringString").ToList();

    var rx = new Regex(@"^\s*(\d+)x(\d+)\s*@\s*(-?\d+)\s*,\s*(-?\d+)\s*$");
    var screenMap = new SortedDictionary<string, string>(StringComparer.Ordinal);

    foreach (var p in pairs)
    {
        var key = p.Elements().FirstOrDefault(e => e.Name.LocalName == "Key")?.Value ?? "";
        var val = p.Elements().FirstOrDefault(e => e.Name.LocalName == "Value")?.Value ?? "";

        var m = rx.Match(key);
        if (!m.Success) continue;

        var screenKey = $"{m.Groups[1].Value}x{m.Groups[2].Value}@{m.Groups[3].Value},{m.Groups[4].Value}";
        if (!xamlToLayout.TryGetValue(val.Trim(), out var layoutId)) continue;

        screenMap[screenKey] = layoutId;
    }

    var cfgDir = Path.GetDirectoryName(outConfigPath);
    if (!string.IsNullOrWhiteSpace(cfgDir)) Directory.CreateDirectory(cfgDir);

    var cfg = new
    {
        defaultLayoutId = "horizontal-info",
        screenLayoutMap = screenMap
    };

    File.WriteAllText(outConfigPath, JsonSerializer.Serialize(cfg, _jsonOpts) + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    return screenMap.Count;
}

string GetLayoutsDir()
{
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var userDir = Path.Combine(home, ".config/kwin-stack/layouts");
    if (Directory.Exists(userDir)) return userDir;
    var here = AppContext.BaseDirectory;
    return Path.GetFullPath(Path.Combine(here, "..", "layouts"));
}

List<LayoutFile> LoadLayouts()
{
    var dir = GetLayoutsDir();
    if (!Directory.Exists(dir)) return new();
    var layouts = new List<LayoutFile>();
    foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
    {
        try
        {
            var txt = File.ReadAllText(path, Encoding.UTF8);
            var layout = JsonSerializer.Deserialize<LayoutFile>(txt, _jsonOpts);
            if (layout?.Id is { Length: > 0 })
                layouts.Add(layout);
        }
        catch { }
    }
    layouts.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
    return layouts;
}

List<ScreenKey> GetScreensFromKscreenDoctor()
{
    var exe = Which("kscreen-doctor");
    if (exe is null) return new();

    var (code, stdout, _stderr) = Run(new List<string> { exe, "-o" });
    if (code != 0) return new();

    var text = StripAnsi(stdout);
    var lines = text.Split('\n');

    var screens = new List<ScreenKey>();
    string? currentName = null;

    foreach (var line in lines)
    {
        var t = line.TrimEnd();
        if (t.StartsWith("Output:", StringComparison.Ordinal))
        {
            var parts = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            currentName = parts.Length >= 3 ? parts[2] : null;
            continue;
        }
        if (t.Contains("Geometry:", StringComparison.Ordinal))
        {
            var idx = t.IndexOf("Geometry:", StringComparison.Ordinal);
            var rest = t[(idx + "Geometry:".Length)..].Trim();
            if (currentName is null) continue;
            screens.Add(new ScreenKey(currentName, GeometryToKey(rest), rest));
        }
    }

    return screens;
}

string StripAnsi(string s)
{
    var sb = new StringBuilder(s.Length);
    var i = 0;
    while (i < s.Length)
    {
        if (s[i] == '\u001b')
        {
            var end = s.IndexOf('m', i);
            if (end < 0) break;
            i = end + 1;
            continue;
        }
        sb.Append(s[i]);
        i++;
    }
    return sb.ToString();
}

string GeometryToKey(string geometry)
{
    var parts = geometry.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 2) return geometry;
    return $"{parts[1]}@{parts[0]}";
}

}
