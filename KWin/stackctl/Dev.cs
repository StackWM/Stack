using System.Threading;

namespace StackCtl;

internal sealed partial class StackCtlApp
{
    int CmdDev(string[] a)
    {
        if (a.Length == 0) return Fail("dev: missing action (watch)");
        return a[0] switch
        {
            "watch" => CmdDevWatch(a.Skip(1).ToArray()),
            _ => Fail($"dev: unknown action: {a[0]}")
        };
    }

    int CmdDevWatch(string[] a)
    {
        var doInstall = true;
        var doReload = true;
        var debounceMs = 800;
        var once = false;
        var logPath = "";

        for (var i = 0; i < a.Length; i++)
        {
            switch (a[i])
            {
                case "--no-install":
                    doInstall = false;
                    continue;
                case "--no-reload":
                    doReload = false;
                    continue;
                case "--once":
                    once = true;
                    continue;
                case "--debounce":
                    if (i + 1 >= a.Length) return Fail("dev watch: --debounce <ms>");
                    debounceMs = int.TryParse(a[++i], out var ms) ? ms : 800;
                    continue;
                case "--log":
                    if (i + 1 >= a.Length) return Fail("dev watch: --log <path>");
                    logPath = a[++i];
                    continue;
            }
        }

        var root = FindRepoRoot();
        if (root is null) return Fail("dev watch: could not locate repo root (expected Stack.sln)");

        void LogLine(string s)
        {
            if (string.IsNullOrWhiteSpace(logPath))
            {
                Console.WriteLine(s);
                return;
            }

            var full = Path.GetFullPath(Path.IsPathRooted(logPath) ? logPath : Path.Combine(root, logPath));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.AppendAllText(full, s + Environment.NewLine);
        }

        void DevStep(string reason)
        {
            var stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
            LogLine($"{stamp} change: {reason}");

            if (doInstall)
            {
                LogLine($"{stamp} run: ./KWin/install.sh");
                var (code, stdout, stderr) = Run(new List<string> { "bash", "-lc", "./KWin/install.sh" }, workdir: root);
                if (!string.IsNullOrWhiteSpace(stdout)) LogLine(stdout.TrimEnd());
                if (!string.IsNullOrWhiteSpace(stderr)) LogLine(stderr.TrimEnd());
                if (code != 0) LogLine($"{stamp} install failed: exit={code}");
            }

            if (doReload)
            {
                LogLine($"{stamp} run: stackctl script reload");
                var ok = ReloadScript();
                LogLine($"{stamp} reload={(ok ? "ok" : "failed")}");
            }
        }

        if (once)
        {
            DevStep("--once");
            return 0;
        }

        LogLine("watching: KWin/kwin-stack, KWin/layouts (Ctrl+C to stop)");

        using var ev = new AutoResetEvent(false);
        var lastReason = "";
        var lastEventAt = DateTimeOffset.MinValue;

        void OnChange(string reason)
        {
            lastReason = reason;
            lastEventAt = DateTimeOffset.Now;
            ev.Set();
        }

        using var w1 = MakeWatcher(Path.Combine(root, "KWin", "kwin-stack"), includeSubdirs: true, OnChange);
        using var w2 = MakeWatcher(Path.Combine(root, "KWin", "layouts"), includeSubdirs: false, OnChange);

        while (true)
        {
            _ = ev.WaitOne();
            Thread.Sleep(debounceMs);
            if ((DateTimeOffset.Now - lastEventAt).TotalMilliseconds < debounceMs) continue;
            DevStep(lastReason);
        }
    }

    FileSystemWatcher MakeWatcher(string path, bool includeSubdirs, Action<string> onChange)
    {
        var w = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = includeSubdirs,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        void Handler(object? _, FileSystemEventArgs e)
        {
            var p = e.FullPath ?? "";
            if (p.EndsWith("~", StringComparison.Ordinal) || p.EndsWith(".swp", StringComparison.Ordinal)) return;
            onChange($"{e.ChangeType}: {p}");
        }

        w.Changed += Handler;
        w.Created += Handler;
        w.Deleted += Handler;
        w.Renamed += (_, e) => onChange($"Renamed: {e.OldFullPath} -> {e.FullPath}");
        w.EnableRaisingEvents = true;
        return w;
    }

    bool ReloadScript()
    {
        var qdbus = FindQdbus();
        var scriptPath = InstalledScriptMainPath();
        if (!File.Exists(scriptPath)) return false;

        _ = Run(new List<string> { qdbus, KWinService, KWinScriptingObject, $"{KWinScriptingIface}.unloadScript", ScriptId });
        _ = Run(new List<string> { qdbus, KWinService, KWinScriptingObject, $"{KWinScriptingIface}.loadScript", scriptPath, ScriptId });
        _ = Run(new List<string> { qdbus, KWinService, KWinScriptingObject, $"{KWinScriptingIface}.start" });
        return IsScriptLoaded();
    }
}
