using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TS6Overlay;

// Opt-in rendering check for release validation; never used during normal startup.
internal static class DesignCheck
{
    internal static void Run(App app)
    {
        string output = Path.Combine(Config.Dir, "design-check");
        Directory.CreateDirectory(output);
        try
        {
            var settings = new SettingsWindow(app);
            settings.Show();
            for (int i = 0; i < 10; i++)
            {
                settings.SelectTab(i);
                settings.UpdateLayout();
                Save(settings, Path.Combine(output, $"settings-{i}.png"));
            }
            var overlay = new OverlayView(app.Cfg, preview: true);
            overlay.ApplyTheme(Theme.ByName("Aurora")); overlay.ShowDemo();
            overlay.Measure(new Size(600, 1000));
            overlay.Arrange(new Rect(overlay.DesiredSize)); overlay.UpdateLayout();
            Save(overlay, Path.Combine(output, "overlay.png"));
            int previousSpacing = app.Cfg.UserSpacing;
            double previousHeight = 0;
            foreach (int spacing in new[] { 0, 2, 20 })
            {
                app.Cfg.UserSpacing = spacing;
                overlay = new OverlayView(app.Cfg, preview: true);
                overlay.ApplyTheme(Theme.ByName("Aurora"));
                overlay.ShowDemo();
                overlay.Measure(new Size(600, 1000));
                overlay.Arrange(new Rect(overlay.DesiredSize)); overlay.UpdateLayout();
                if (overlay.ActualHeight <= previousHeight) throw new InvalidOperationException("Participant spacing did not increase overlay height.");
                previousHeight = overlay.ActualHeight;
                Save(overlay, Path.Combine(output, $"overlay-spacing-{spacing}.png"));
            }
            app.Cfg.UserSpacing = previousSpacing;
            var oldConfig = System.Text.Json.JsonSerializer.Deserialize<Config>("{}");
            if (oldConfig?.UserSpacing != 2) throw new InvalidOperationException("Missing spacing must default to 2.");
            var roundTrip = System.Text.Json.JsonSerializer.Deserialize<Config>(System.Text.Json.JsonSerializer.Serialize(new Config { UserSpacing = 12 }));
            if (roundTrip?.UserSpacing != 12) throw new InvalidOperationException("Spacing must survive configuration serialization.");
            CheckSpeakerTransitions();
            var cfg = new Config { MinimalMode = true, ShowChannelList = true };
            double verticalWidth = 0, verticalHeight = 0;
            foreach (bool horizontal in new[] { false, true })
            {
                cfg.HorizontalLayout = horizontal;
                var view = new OverlayView(cfg, preview: true);
                view.ApplyTheme(Theme.ByName("Aurora")); view.ShowDemo();
                view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                view.Arrange(new Rect(view.DesiredSize)); view.UpdateLayout();
                if (!horizontal) { verticalWidth = view.ActualWidth; verticalHeight = view.ActualHeight; }
                else if (view.ActualWidth <= verticalWidth || view.ActualHeight >= verticalHeight)
                    throw new InvalidOperationException("Horizontal minimal layout must be wider and shorter.");
                Save(view, Path.Combine(output, $"minimal-{horizontal}.png"));
            }
            var profile = new GameProfile { Game = "test", MinimalMode = true, HorizontalLayout = true, Scale = 1.3 };
            cfg.Profiles.Add(profile);
            cfg.Favorites.Add(new Favorite { Uid = "unique-user", Alias = "Pilot", Color = "#FF00FF00" });
            var restored = System.Text.Json.JsonSerializer.Deserialize<Config>(System.Text.Json.JsonSerializer.Serialize(cfg))!;
            if (restored.Profiles[0].HorizontalLayout != true || restored.FavoriteFor("unique-user")?.Alias != "Pilot" || restored.Profiles[0].Scale != 1.3)
                throw new InvalidOperationException("Profiles and aliases must survive configuration serialization.");
            settings.Close();
            File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: 10 settings sections; spacing; speaker hold/fade/resume/disconnect/identity; minimal layouts; saved profiles and aliases.");
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(output, "result.txt"), ex.ToString());
            Environment.ExitCode = 1;
        }
        finally { app.Shutdown(); }
    }

    static void CheckSpeakerTransitions()
    {
        var cfg = new Config { ShowChannelList = false, SpeakerHoldSeconds = 2, SpeakerFadeMilliseconds = 500 };
        var state = OverlayView.DemoState(cfg);
        var member = state.Servers[0].Members[0];
        var history = new SpeakerVisibility();
        void Expect(double value, long time)
        {
            if (Math.Abs(history.Opacity(1, member, cfg, time) - value) > .001)
                throw new InvalidOperationException($"Unexpected speaker opacity at {time}.");
        }
        history.Observe(state, 0); Expect(1, 0);
        member.Talking = false; history.Observe(state, 100);
        Expect(1, 2100); Expect(.5, 2350); Expect(0, 2600);
        member.Talking = true; history.Observe(state, 2400); Expect(1, 2400);
        member.Talking = false; history.Observe(state, 2500); Expect(1, 2500);
        cfg.SpeakerFade = false; Expect(0, 2501);
        cfg.ShowChannelList = true; Expect(1, 2600);
        cfg.ShowChannelList = false; cfg.SpeakerFade = true;
        history.Observe(OverlayState.Disconnected, 2600); Expect(0, 2601);
        member.Talking = true; history.Observe(state, 2700);
        member.Talking = false; member.Uid = "replacement-user"; history.Observe(state, 2800); Expect(0, 2800);
    }

    static void Save(FrameworkElement element, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
