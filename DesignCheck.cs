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
            settings.Close();
            File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: 10 settings sections; overlay gaps 0, 2, 20; default and saved spacing verified.");
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(output, "result.txt"), ex.ToString());
            Environment.ExitCode = 1;
        }
        finally { app.Shutdown(); }
    }

    static void Save(FrameworkElement element, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
