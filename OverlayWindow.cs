using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace TS6Overlay;

/// <summary>Przezroczyste okno na wierzchu, które przepuszcza kliknięcia do gry.</summary>
public sealed class OverlayWindow : Window
{
    readonly Config _cfg;
    readonly DispatcherTimer _ctrlTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    bool _editMode, _ctrlDrag;

    public OverlayView View { get; }
    /// <summary>Użytkownik przesunął nakładkę — App zapisuje pozycję w ustawieniach albo w profilu gry.</summary>
    public event Action<double, double>? Moved;
    public event Action<double>? Scaled;
    readonly Thumb _resize = new() { Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Cursor = Cursors.SizeNWSE, Background = Brushes.Turquoise, Opacity = .8, Visibility = Visibility.Collapsed, ToolTip = "Przeciągnij, aby zmienić rozmiar" };
    double _scale = 1;
    double _resizeStart, _resizeDelta, _resizeExtent;

    public OverlayWindow(Config cfg)
    {
        _cfg = cfg;
        Title = "TS6 Overlay";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Left = cfg.Left;
        Top = cfg.Top;

        View = new OverlayView(cfg);
        var canvas = new Grid();
        canvas.Children.Add(View);
        canvas.Children.Add(_resize);
        Content = canvas;
        _resize.DragStarted += (_, _) => { _resizeStart = _scale; _resizeDelta = 0; _resizeExtent = Math.Max(100, ActualWidth); };
        _resize.DragDelta += (_, e) =>
        {
            _resizeDelta += e.HorizontalChange + e.VerticalChange;
            ApplyScale(Math.Clamp(_resizeStart * (1 + _resizeDelta / _resizeExtent), .6, 2), Opacity);
            Scaled?.Invoke(_scale);
        };
        _resize.DragCompleted += (_, _) => { Snap(); Moved?.Invoke(Left, Top); };
        ApplyScale(cfg.Scale, cfg.Opacity);
        View.ApplyTheme(Theme.ByName(cfg.Theme));

        MouseLeftButtonDown += (_, e) =>
        {
            if (!(_editMode || _ctrlDrag) || e.ButtonState != MouseButtonState.Pressed) return;
            if (FindLink(e.OriginalSource as DependencyObject) is { NavigateUri: { } uri })
            {
                Links.Open(uri);
                e.Handled = true;
                return;
            }
            if (e.OriginalSource is DependencyObject source && IsResizeSource(source)) return;
            DragMove();
            Snap();
            Moved?.Invoke(Left, Top);
        };
        SourceInitialized += (_, _) => ApplyClickThrough();

        // Ctrl + mysz nad nakładką = można ją chwycić i przesunąć.
        _ctrlTimer.Tick += (_, _) => CheckCtrlDrag();
        _ctrlTimer.Start();
    }

    void CheckCtrlDrag()
    {
        if (_editMode || !IsVisible) return;
        bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
        bool dragging = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0 && _ctrlDrag;
        bool over = false;
        if (ctrl && GetCursorPos(out var pt))
        {
            var h = new WindowInteropHelper(this).Handle;
            over = GetWindowRect(h, out var r) && pt.X >= r.Left && pt.X < r.Right && pt.Y >= r.Top && pt.Y < r.Bottom;
        }
        bool want = (ctrl && over) || dragging;
        if (want == _ctrlDrag) return;
        _ctrlDrag = want;
        View.SetHighlight(want);
        ApplyClickThrough();
    }

    /// <summary>Hyperlink, w który kliknięto (przechodzi w górę drzewa od elementu pod kursorem).</summary>
    static System.Windows.Documents.Hyperlink? FindLink(DependencyObject? d)
    {
        while (d != null)
        {
            if (d is System.Windows.Documents.Hyperlink h) return h;
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return null;
    }

    public bool EditMode
    {
        get => _editMode;
        set
        {
            _editMode = value;
            _resize.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            View.SetHighlight(value, value ? L.T("Przeciągnij mnie myszką, potem „Zablokuj pozycję” w zasobniku") : null);
            ApplyClickThrough();
            if (!value) Moved?.Invoke(Left, Top);
        }
    }

    public void ApplyScale(double scale, double opacity)
    {
        _scale = Math.Clamp(scale, .6, 2);
        View.LayoutTransform = new ScaleTransform(_scale, _scale);
        Opacity = opacity;
    }

    bool IsResizeSource(DependencyObject source)
    {
        for (DependencyObject? d = source; d != null; d = VisualTreeHelper.GetParent(d))
            if (d == _resize) return true;
        return false;
    }

    void Snap()
    {
        if (!_cfg.SnapToEdges) return;
        var handle = new WindowInteropHelper(this).Handle;
        var area = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var a = transform.Transform(new Point(area.Left, area.Top));
        var b = transform.Transform(new Point(area.Right, area.Bottom));
        if (Math.Abs(Left - a.X) <= 16) Left = a.X;
        else if (Math.Abs(Left + ActualWidth - b.X) <= 16) Left = b.X - ActualWidth;
        if (Math.Abs(Top - a.Y) <= 16) Top = a.Y;
        else if (Math.Abs(Top + ActualHeight - b.Y) <= 16) Top = b.Y - ActualHeight;
    }

    // ---------- click-through ----------

    const int GWL_EXSTYLE = -20;
    const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
    const int VK_CONTROL = 0x11, VK_LBUTTON = 0x01;

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    static readonly IntPtr HWND_TOPMOST = new(-1);
    const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10, SWP_NOOWNERZORDER = 0x200;

    /// <summary>
    /// Gry (zwłaszcza w trybie okna bez ramek lub z optymalizacjami pełnego ekranu Windows) potrafią wejść nad
    /// okna „zawsze na wierzchu”. Ponowne ustawienie TOPMOST co chwilę przywraca nakładkę nad grą.
    /// </summary>
    public void KeepOnTop()
    {
        if (!IsVisible) return;
        var h = new WindowInteropHelper(this).Handle;
        if (h != IntPtr.Zero) SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    void ApplyClickThrough()
    {
        var h = new WindowInteropHelper(this).Handle;
        if (h == IntPtr.Zero) return;
        int ex = GetWindowLong(h, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        ex = _editMode || _ctrlDrag ? ex & ~WS_EX_TRANSPARENT : ex | WS_EX_TRANSPARENT;
        SetWindowLong(h, GWL_EXSTYLE, ex);
    }
}
