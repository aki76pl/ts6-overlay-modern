using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace TS6Overlay;

internal static class UpdateHandover
{
    internal sealed record Request(string Target, int ParentPid, long ParentStartTicks);
    internal static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TS6OverlayModern", "updates");

    internal static void Start(string directory, string target)
    {
        string helper = Path.Combine(directory, "updater.exe");
        File.Copy(Environment.ProcessPath!, helper);
        using var parent = Process.GetCurrentProcess();
        string request = Path.Combine(directory, "request.json");
        File.WriteAllText(request, JsonSerializer.Serialize(new Request(target, parent.Id, parent.StartTime.ToUniversalTime().Ticks)));
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
        WaitForParent(request.ParentPid, request.ParentStartTicks);
        Apply(Path.Combine(directory, "payload.exe"), target, path =>
        {
            _ = Process.Start(new ProcessStartInfo(path, "--updated") { UseShellExecute = true })
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

    // Called only after the parent exits. Unique filenames never collide with a locked legacy .old.
    internal static void Apply(string payload, string target, Action<string> launch, int attempts = 80, int delayMs = 250)
    {
        string suffix = Guid.NewGuid().ToString("N");
        string candidate = target + ".new-" + suffix;
        string backup = target + ".backup-" + suffix;
        bool moved = false, installed = false;
        try
        {
            File.Copy(payload, candidate); // Same volume as target, including portable installs on other drives.
            Retry(() => File.Move(target, backup), attempts, delayMs);
            moved = true;
            Retry(() => File.Move(candidate, target), attempts, delayMs);
            installed = true;
            launch(target);
        }
        catch (Exception failure)
        {
            if (moved)
            {
                try
                {
                    if (installed) Retry(() => File.Delete(target), attempts, delayMs);
                    Retry(() => File.Move(backup, target), attempts, delayMs);
                }
                catch (Exception rollback)
                {
                    throw new IOException($"Podmiana nie powiodła się: {failure.Message}\nNie udało się przywrócić pliku: {rollback.Message}\nPoprzednia wersja jest w: {backup}", failure);
                }
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
