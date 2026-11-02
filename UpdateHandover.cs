using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace TS6Overlay;

internal static class UpdateHandover
{
    internal sealed record Request(string Target, int ParentPid, long ParentStartTicks, bool Install = false);
    internal const string ExitEvent = "TS6OverlayModern.exitForInstall";
    internal static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TS6OverlayModern", "updates");

    internal static void Start(string directory, string target, bool install = false)
    {
        string helper = Path.Combine(directory, "updater.exe");
        File.Copy(Environment.ProcessPath!, helper);
        using var parent = Process.GetCurrentProcess();
        string request = Path.Combine(directory, "request.json");
        File.WriteAllText(request, JsonSerializer.Serialize(new Request(target, parent.Id, parent.StartTime.ToUniversalTime().Ticks, install)));
        var start = new ProcessStartInfo(helper) { UseShellExecute = false, WorkingDirectory = directory };
        start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(request);
        _ = Process.Start(start) ?? throw new IOException("Nie udało się uruchomić pomocnika aktualizacji.");
    }

    internal static void Run(string requestPath)
    {
        string full = Path.GetFullPath(requestPath);
        string directory = Path.GetDirectoryName(full)!;
        if (!string.Equals(Path.GetDirectoryName(directory), Path.GetFullPath(Root), StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParseExact(Path.GetFileName(directory), "N", out _)
            || Path.GetFileName(full) != "request.json")
            throw new InvalidDataException("Nieprawidłowe żądanie aktualizacji.");
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(full)) ?? throw new InvalidDataException("Puste żądanie aktualizacji.");
        string target = Path.GetFullPath(request.Target);
        if (!Path.IsPathFullyQualified(request.Target) || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Nieprawidłowa ścieżka programu.");
        if (request.Install && !string.Equals(target, Path.GetFullPath(Installer.InstalledExe), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Nieprawidłowy katalog instalacji.");
        WaitForParent(request.ParentPid, request.ParentStartTicks);
        WaitForTargetExit(target);
        if (request.Install) Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Apply(Path.Combine(directory, "payload.exe"), target, path =>
        {
            _ = Process.Start(new ProcessStartInfo(path, request.Install ? "--installed" : "--updated") { UseShellExecute = true })
                ?? throw new IOException("Nie udało się uruchomić nowej wersji.");
        });
    }

    internal static void WaitForParent(int pid, long startTicks)
    {
        Process parent;
        try { parent = Process.GetProcessById(pid); }
        catch (ArgumentException) { return; }
        using (parent)
        {
            try
            {
                // A recycled process ID does not belong to the app being updated.
                if (parent.HasExited || parent.StartTime.ToUniversalTime().Ticks != startTicks) return;
                if (!parent.WaitForExit(30000)) throw new IOException("Program nadal działa. Zamknij TS6 Overlay i spróbuj ponownie.");
            }
            catch (InvalidOperationException) { } // The process exited between lookup and querying its handle.
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode is 6 or 87 or 1168) { }

        }
    }

    internal static void WaitForTargetExit(string target)
    {
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(target)))
        using (process)
        {
            if (process.Id == Environment.ProcessId) continue;
            try
            {
                if (process.HasExited || !string.Equals(process.MainModule?.FileName, target, StringComparison.OrdinalIgnoreCase)) continue;
                // Recent versions can save settings and close cleanly. Older hidden copies require manual closure.
                if (EventWaitHandle.TryOpenExisting(ExitEvent, out var exit)) { using (exit) exit.Set(); }
                if (!process.WaitForExit(30000))
                    throw new IOException("Poprzednia kopia TS6 Overlay nadal działa. Zakończ procesy TS6OverlayModern w Menedżerze zadań, uruchom pobrany plik i ponów instalację.");
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { } // A protected process is not assumed to be our app; file locks are still checked below.
        }
    }

    // The source has been staged before the parent shuts down; never truncate a running target.
    internal static void Apply(string payload, string target, Action<string> launch, int attempts = 80, int delayMs = 250)
    {
        string suffix = Guid.NewGuid().ToString("N");
        string candidate = target + ".new-" + suffix;
        string backup = target + ".backup-" + suffix;
        bool moved = false, installed = false, clearedReadOnly = false;
        FileAttributes originalAttributes = FileAttributes.Normal;
        try
        {
            File.Copy(payload, candidate);
            File.SetAttributes(candidate, FileAttributes.Normal);
            if (File.Exists(target))
            {
                originalAttributes = File.GetAttributes(target);
                if ((originalAttributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(target, originalAttributes & ~FileAttributes.ReadOnly);
                    clearedReadOnly = true;
                }
                Retry(() => File.Move(target, backup), attempts, delayMs);
                moved = true;
            }
            Retry(() => File.Move(candidate, target), attempts, delayMs);
            installed = true;
            launch(target);
        }
        catch (Exception failure)
        {
            try
            {
                if (installed) Retry(() => File.Delete(target), attempts, delayMs);
                if (moved) Retry(() => File.Move(backup, target), attempts, delayMs);
                if (clearedReadOnly && File.Exists(target)) File.SetAttributes(target, originalAttributes);
            }
            catch (Exception rollback)
            {
                string previous = File.Exists(backup) ? backup : target;
                throw new IOException($"Instalacja/podmiana nie powiodła się: {failure.Message}\nNie udało się zakończyć przywracania: {rollback.Message}\nPoprzedni plik: {previous}", failure);
            }
            throw;
        }
        finally { TryDelete(candidate); }
        TryDelete(backup);
    }

    static void Retry(Action action, int attempts, int delayMs)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { action(); return; }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && attempt + 1 < attempts)
            { Thread.Sleep(delayMs); }
        }
    }
    static void TryDelete(string path) { try { File.Delete(path); } catch { } }

    internal static void Cleanup()
    {
        try
        {
            if (!Directory.Exists(Root)) return;
            foreach (string directory in Directory.GetDirectories(Root))
                if (Guid.TryParseExact(Path.GetFileName(directory), "N", out _) && Directory.GetCreationTimeUtc(directory) < DateTime.UtcNow.AddDays(-1))
                    try { Directory.Delete(directory, true); } catch { }
        }
        catch { }
    }
}
