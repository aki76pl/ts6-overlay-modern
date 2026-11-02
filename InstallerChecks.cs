using System.IO;
using System.Text.Json;

namespace TS6Overlay;

internal static class InstallerChecks
{
    internal static void Run(string output)
    {
        string root = Path.Combine(output, "install-regression"); Directory.CreateDirectory(root);
        string source = Path.Combine(root, "downloaded-3.7.5.exe"), target = Path.Combine(root, "installed", "TS6OverlayModern.exe");
        string staging = Path.Combine(root, "staging"); File.WriteAllText(source, "new-program");
        string prepared = Installer.PreparePayload(source, target, staging);
        if (File.Exists(target) || File.ReadAllText(Path.Combine(prepared, "payload.exe")) != "new-program")
            throw new InvalidOperationException("First install staging must not modify destination.");
        UpdateHandover.Apply(Path.Combine(prepared, "payload.exe"), target, _ => { });
        if (File.ReadAllText(target) != "new-program") throw new InvalidOperationException("First installation failed.");

        File.WriteAllText(target, "old-program"); File.SetAttributes(target, FileAttributes.ReadOnly);
        try
        {
            prepared = Installer.PreparePayload(source, target, staging);
            if (File.ReadAllText(target) != "old-program") throw new InvalidOperationException("Staging overwrote existing program.");
            UpdateHandover.Apply(Path.Combine(prepared, "payload.exe"), target, _ => { });
            if (File.ReadAllText(target) != "new-program" || (File.GetAttributes(target) & FileAttributes.ReadOnly) != 0)
                throw new InvalidOperationException("Readonly existing EXE replacement failed.");
        }
        finally { File.SetAttributes(target, FileAttributes.Normal); }

        File.WriteAllText(target, "old-program");
        using (var held = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            prepared = Installer.PreparePayload(source, target, staging); bool denied = false;
            try { UpdateHandover.Apply(Path.Combine(prepared, "payload.exe"), target, _ => { }, 2, 1); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { denied = true; }
            if (!denied) throw new InvalidOperationException("Locked installation must fail safely.");
        }
        if (File.ReadAllText(target) != "old-program") throw new InvalidOperationException("Locked installation damaged previous version.");

        File.SetAttributes(target, FileAttributes.ReadOnly); bool failed = false;
        try { UpdateHandover.Apply(source, target, _ => throw new IOException("Simulated startup failure"), 2, 1); }
        catch (IOException) { failed = true; }
        if (!failed || File.ReadAllText(target) != "old-program" || (File.GetAttributes(target) & FileAttributes.ReadOnly) == 0)
            throw new InvalidOperationException("Rollback must restore previous file and readonly flag.");
        File.SetAttributes(target, FileAttributes.Normal); File.Delete(target);
        failed = false;
        try { UpdateHandover.Apply(source, target, _ => throw new IOException("Simulated startup failure"), 2, 1); }
        catch (IOException) { failed = true; }
        if (!failed || File.Exists(target)) throw new InvalidOperationException("Failed first installation must remove incomplete EXE.");
        bool selfCopyRejected = false;
        try { Installer.PreparePayload(source, source, staging); }
        catch (InvalidOperationException) { selfCopyRejected = true; }
        if (!selfCopyRejected || File.ReadAllText(source) != "new-program") throw new InvalidOperationException("Self-overwrite guard failed.");
        var request = new UpdateHandover.Request(target, 1, 2, Install: true);
        if (JsonSerializer.Deserialize<UpdateHandover.Request>(JsonSerializer.Serialize(request))?.Install != true)
            throw new InvalidOperationException("Install request mode was not preserved.");
        string config = Path.Combine(Config.Dir, "config.json");
        string before = File.Exists(config) ? File.ReadAllText(config) : "";
        try { Installer.PreparePayload(Path.Combine(root, "missing.exe"), target, staging); }
        catch (FileNotFoundException) { }
        if ((File.Exists(config) ? File.ReadAllText(config) : "") != before) throw new InvalidOperationException("Installer changed user settings.");
    }
}
