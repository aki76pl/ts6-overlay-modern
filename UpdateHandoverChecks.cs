using System.Diagnostics;
using System.IO;

namespace TS6Overlay;

internal static class UpdateHandoverChecks
{
    internal static void Run(string output)
    {
        string directory = Path.Combine(output, "update-regression"); Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, "TS6OverlayModern.exe"), payload = Path.Combine(directory, "payload.exe");
        string legacy = Path.Combine(directory, "TS6Overlay.old"), config = Path.Combine(directory, "config.json");
        File.WriteAllText(config, "keep-settings"); File.WriteAllText(payload, "new-version");
        File.WriteAllText(target, "old-version"); File.WriteAllText(legacy, "legacy-version");
        File.SetAttributes(legacy, FileAttributes.ReadOnly);
        try
        {
            using (var legacyLock = new FileStream(legacy, FileMode.Open, FileAccess.Read, FileShare.None))
                UpdateHandover.Apply(payload, target, path =>
                {
                    if (File.ReadAllText(path) != "new-version") throw new InvalidOperationException("Incorrect replacement payload.");
                });
            if (File.ReadAllText(target) != "new-version" || File.ReadAllText(legacy) != "legacy-version")
                throw new InvalidOperationException("Locked legacy backup interfered with update.");
        }
        finally { File.SetAttributes(legacy, FileAttributes.Normal); }

        File.WriteAllText(target, "old-version"); bool failed = false;
        try { UpdateHandover.Apply(payload, target, _ => throw new IOException("Simulated launch failure"), 2, 1); }
        catch (IOException) { failed = true; }
        if (!failed || File.ReadAllText(target) != "old-version") throw new InvalidOperationException("Launch failure must restore the old executable.");

        failed = false;
        using (var targetLock = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try { UpdateHandover.Apply(payload, target, _ => throw new InvalidOperationException("Must not launch"), 2, 1); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failed = true; }
        }
        if (!failed || File.ReadAllText(target) != "old-version") throw new InvalidOperationException("Locked target must leave original intact.");

        var transientLock = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
        var unlock = Task.Run(async () => { await Task.Delay(150); transientLock.Dispose(); });
        try { UpdateHandover.Apply(payload, target, _ => { }, 40, 25); }
        finally { transientLock.Dispose(); unlock.GetAwaiter().GetResult(); }
        if (File.ReadAllText(target) != "new-version" || File.ReadAllText(config) != "keep-settings" || File.ReadAllText(payload) != "new-version")
            throw new InvalidOperationException("Retry must succeed and preserve settings and payload.");
        if (Directory.GetFiles(directory, "*.new-*").Length != 0 || Directory.GetFiles(directory, "*.backup-*").Length != 0)
            throw new InvalidOperationException("Update left pending replacement files.");

        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
            { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command"); start.ArgumentList.Add("Start-Sleep -Milliseconds 500");
        using var parent = Process.Start(start) ?? throw new IOException("Could not start parent test process.");
        UpdateHandover.WaitForParent(parent.Id, parent.StartTime.ToUniversalTime().Ticks);
        if (!parent.HasExited) throw new InvalidOperationException("Updater did not wait for parent exit.");
    }
}
