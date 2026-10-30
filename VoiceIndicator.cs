using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace TS6Overlay;

internal static class VoiceIndicator
{
    // These animations indicate TeamSpeak's talking state, not microphone volume.
    internal static FrameworkElement Create(string style, bool talking, bool small, bool square, Brush brush, bool animate)
    {
        double size = small ? 8 : 10;
        var slot = new Grid { Width = 14, Height = 16, Margin = new Thickness(small ? 4 : 0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
        bool bars = style == "Bars";
        int count = bars ? 3 : 1;
        var shapes = new List<Shape>();
        for (int i = 0; i < count; i++)
        {
            Shape shape = bars || square ? new Rectangle() : new Ellipse();
            shape.Width = bars ? 3 : size;
            shape.Height = bars ? (talking ? 14 : 3) : size;
            shape.Fill = brush;
            shape.HorizontalAlignment = HorizontalAlignment.Center;
            shape.VerticalAlignment = VerticalAlignment.Center;
            if (bars) shape.Margin = new Thickness((i - 1) * 8, 0, 0, 0);
            shapes.Add(shape); slot.Children.Add(shape);
        }
        bool active = talking && animate && style != "Static";
        slot.Loaded += (_, _) =>
        {
            if (!active) return;
            for (int i = 0; i < shapes.Count; i++)
            {
                var shape = shapes[i];
                int duration = bars ? 240 + i * 75 : 600;
                var animation = new DoubleAnimation(bars ? .25 : .4, 1, TimeSpan.FromMilliseconds(duration))
                {
                    AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromMilliseconds(-(Environment.TickCount64 % (duration * 2))),
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                };
                if (bars)
                {
                    var scale = new ScaleTransform(1, 1);
                    shape.RenderTransformOrigin = new Point(.5, .5);
                    shape.RenderTransform = scale;
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
                }
                else shape.BeginAnimation(UIElement.OpacityProperty, animation);
            }
        };
        slot.Unloaded += (_, _) =>
        {
            foreach (var shape in shapes)
            {
                shape.BeginAnimation(UIElement.OpacityProperty, null);
                if (shape.RenderTransform is ScaleTransform scale) scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            }
        };
        return slot;
    }
}
