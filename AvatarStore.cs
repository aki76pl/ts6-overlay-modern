using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TS6Overlay;

internal static class AvatarStore
{
    sealed class Cached { public string Data = ""; public BitmapSource? Image; }
    static readonly ConditionalWeakTable<Favorite, Cached> Cache = new();
    internal static string Initials(string name)
    {
        var parts = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => StringInfo.GetNextTextElement(p))).ToUpperInvariant();
    }
    internal static string Import(string path)
    {
        if (new FileInfo(path).Length > 10 * 1024 * 1024) throw new InvalidDataException("Wybierz obraz mniejszy niż 10 MB.");
        using var stream = File.OpenRead(path);
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 64; image.StreamSource = stream; image.EndInit(); image.Freeze();
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, 64, 64)));
            double scale = Math.Max(64.0 / image.PixelWidth, 64.0 / image.PixelHeight);
            double w = image.PixelWidth * scale, h = image.PixelHeight * scale;
            dc.DrawImage(image, new Rect((64 - w) / 2, (64 - h) / 2, w, h)); dc.Pop();
        }
        var bitmap = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream(); encoder.Save(output);
        return Convert.ToBase64String(output.ToArray());
    }
    internal static BitmapSource? ImageFor(Favorite? favourite)
    {
        if (favourite == null) return null;
        var cached = Cache.GetValue(favourite, _ => new Cached());
        string data = favourite.AvatarPng ?? "";
        if (cached.Data == data) return cached.Image;
        cached.Data = data; cached.Image = null;
        if (data.Length == 0 || data.Length > 100000) return null;
        try
        {
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 64; image.StreamSource = new MemoryStream(Convert.FromBase64String(data)); image.EndInit(); image.Freeze(); cached.Image = image;
        }
        catch { }
        return cached.Image;
    }
    internal static FrameworkElement Create(string name, Favorite? favourite, Brush text, Brush background, bool small)
    {
        double size = small ? 18 : 20;
        FrameworkElement content = ImageFor(favourite) is { } image
            ? new Image { Source = image, Width = size, Height = size, Stretch = Stretch.UniformToFill }
            : new TextBlock { Text = Initials(name), FontSize = small ? 8 : 10, FontWeight = FontWeights.SemiBold, Foreground = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var avatar = new Grid { Width = size, Height = size, Background = background, Clip = new EllipseGeometry(new Point(size / 2, size / 2), size / 2, size / 2), Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
        avatar.Children.Add(content); return avatar;
    }
}
