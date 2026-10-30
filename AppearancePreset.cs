using System.IO;
using System.Text.Json;

namespace TS6Overlay;

// Explicit allowlist: the file contains no tokens, people, messages or sound paths.
public sealed class AppearancePreset
{
    public int FormatVersion { get; set; } = 1;
    public Theme Theme { get; set; } = TS6Overlay.Theme.ByName("Aurora");
    public double Scale { get; set; } = 1;
    public double Opacity { get; set; } = .9;
    public double Left { get; set; } = 20;
    public double Top { get; set; } = 200;
    public int UserSpacing { get; set; } = 2;
    public bool MinimalMode { get; set; }
    public bool HorizontalLayout { get; set; }
    public bool ShowChannelList { get; set; } = true;
    public bool ShowFlags { get; set; } = true;
    public bool ShowAvatars { get; set; } = true;
    public bool SnapToEdges { get; set; } = true;
    public bool AutoHide { get; set; }
    public int AutoHideSeconds { get; set; } = 5;
    public string AutoHideMode { get; set; } = "Fade";
    public double AutoHideOpacity { get; set; } = .15;
    public string VoiceIndicatorStyle { get; set; } = "Pulse";
    public bool SpeakerFade { get; set; } = true;
    public int SpeakerHoldSeconds { get; set; } = 2;
    public int SpeakerFadeMilliseconds { get; set; } = 500;

    public static AppearancePreset Capture(Config c) => new()
    {
        Theme = TS6Overlay.Theme.ByName(c.Theme), Scale = c.Scale, Opacity = c.Opacity, Left = c.Left, Top = c.Top,
        UserSpacing = c.UserSpacing, MinimalMode = c.MinimalMode, HorizontalLayout = c.HorizontalLayout,
        ShowChannelList = c.ShowChannelList, ShowFlags = c.ShowFlags, ShowAvatars = c.ShowAvatars,
        SnapToEdges = c.SnapToEdges, AutoHide = c.AutoHide, AutoHideSeconds = c.AutoHideSeconds, AutoHideMode = c.AutoHideMode, AutoHideOpacity = c.AutoHideOpacity,
        VoiceIndicatorStyle = c.VoiceIndicatorStyle, SpeakerFade = c.SpeakerFade,
        SpeakerHoldSeconds = c.SpeakerHoldSeconds, SpeakerFadeMilliseconds = c.SpeakerFadeMilliseconds,
    };
    public void Export(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    public static AppearancePreset Read(string path)
    {
        if (new FileInfo(path).Length > 256000) throw new InvalidDataException("Plik wyglądu jest zbyt duży.");
        string json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("FormatVersion", out _) || !document.RootElement.TryGetProperty("Theme", out _))
            throw new InvalidDataException("To nie jest pakiet wyglądu TS6 Overlay.");
        var preset = JsonSerializer.Deserialize<AppearancePreset>(json) ?? throw new InvalidDataException("Pusty plik wyglądu.");
        if (preset.FormatVersion != 1 || preset.Theme == null || !preset.Theme.IsValid()
            || !double.IsFinite(preset.Theme.Radius) || preset.Theme.Radius < 0 || preset.Theme.Radius > 100
            || !double.IsFinite(preset.Theme.BorderWidth) || preset.Theme.BorderWidth < 0 || preset.Theme.BorderWidth > 20
            || string.IsNullOrWhiteSpace(preset.Theme.Font) || preset.Theme.Font.Length > 100
            || !double.IsFinite(preset.AutoHideOpacity) || !double.IsFinite(preset.Scale) || !double.IsFinite(preset.Opacity) || !double.IsFinite(preset.Left) || !double.IsFinite(preset.Top))
            throw new InvalidDataException("Nieprawidłowy lub nieobsługiwany plik wyglądu.");
        return preset;
    }
    public void Apply(Config c)
    {
        var theme = Theme with { Name = "Wygląd " + Guid.NewGuid().ToString("N")[..8] }; theme.SaveCustom();
        c.Theme = theme.Name; c.Scale = Math.Clamp(Scale, .6, 2); c.Opacity = Math.Clamp(Opacity, .3, 1);
        c.Left = Math.Clamp(Left, -10000, 10000); c.Top = Math.Clamp(Top, -10000, 10000);
        c.UserSpacing = Math.Clamp(UserSpacing, 0, 20); c.MinimalMode = MinimalMode; c.HorizontalLayout = HorizontalLayout;
        c.ShowChannelList = ShowChannelList; c.ShowFlags = ShowFlags; c.ShowAvatars = ShowAvatars;
        c.VoiceIndicatorStyle = VoiceIndicatorStyle is "Bars" or "Static" ? VoiceIndicatorStyle : "Pulse";
        c.SnapToEdges = SnapToEdges; c.AutoHide = AutoHide; c.AutoHideSeconds = Math.Clamp(AutoHideSeconds, 2, 60);
        c.AutoHideMode = AutoHideMode == "Header" ? "Header" : "Fade"; c.AutoHideOpacity = Math.Clamp(AutoHideOpacity, 0, .6);
        c.SpeakerFade = SpeakerFade; c.SpeakerHoldSeconds = Math.Clamp(SpeakerHoldSeconds, 0, 10);
        c.SpeakerFadeMilliseconds = Math.Clamp(SpeakerFadeMilliseconds, 100, 2000);
    }
}
