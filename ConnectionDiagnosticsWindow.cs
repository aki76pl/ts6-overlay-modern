using System.Diagnostics;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using static TS6Overlay.L;

namespace TS6Overlay;

internal sealed class ConnectionDiagnosticsWindow : Window
{
    readonly TsClient _client;
    readonly TextBlock _report = new() { TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 24 };
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly Button _probe = new() { Content = T("Sprawdź ponownie"), Padding = new Thickness(15, 8, 15, 8), Margin = new Thickness(0, 15, 0, 0) };
    string _port = T("Sprawdzam…");
    readonly CancellationTokenSource _stop = new();
    public ConnectionDiagnosticsWindow(TsClient client)
    {
        _client = client; Title = T("Sprawdź połączenie z TeamSpeak"); Width = 620; Height = 520;
        Background = Theme.B("#FF101827"); Foreground = Brushes.White; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = T("Diagnostyka połączenia"), FontSize = 24, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(_report); panel.Children.Add(_probe);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Loaded += async (_, _) => { Refresh(); _timer.Start(); await Probe(); };
        Closed += (_, _) => { _timer.Stop(); _stop.Cancel(); };
        _timer.Tick += (_, _) => Refresh(); _probe.Click += async (_, _) => await Probe();
    }
    async Task Probe()
    {
        _probe.IsEnabled = false; _port = T("Sprawdzam…"); Refresh();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token); timeout.CancelAfter(3000);
            using var tcp = new TcpClient(); await tcp.ConnectAsync("127.0.0.1", 5899, timeout.Token);
            _port = T("dostępny");
        }
        catch { _port = T("niedostępny — włącz lokalne API Remote Apps w TeamSpeak"); }
        finally { _probe.IsEnabled = true; if (!_stop.IsCancellationRequested) Refresh(); }
    }
    void Refresh()
    {
        var processes = Process.GetProcessesByName("TeamSpeak"); bool running = processes.Length > 0;
        foreach (var process in processes) process.Dispose();
        var d = _client.Diagnostics();
        string At(DateTimeOffset? time) => time?.ToLocalTime().ToString("HH:mm:ss") ?? T("jeszcze nie odebrano");
        string tip = !running ? T("Uruchom TeamSpeak.")
            : !d.ApiConnected ? T("Sprawdź, czy w TeamSpeak włączone jest Remote Apps. Program automatycznie ponawia połączenie.")
            : !d.Authorized ? T("W TeamSpeak otwórz Ustawienia → Remote Apps i zaakceptuj połączenie TS6 Overlay.")
            : d.Servers == 0 ? T("Połącz TeamSpeak z serwerem i wejdź na kanał.")
            : T("Powiedz kilka słów lub poproś kogoś o rozmowę. Czas ostatniego zdarzenia głosu powinien się zmienić. Cisza sama w sobie nie oznacza błędu.");
        _report.Text = T("TeamSpeak: {0}", running ? T("uruchomiony") : T("nie wykryto")) + "\n"
            + T("Port API 127.0.0.1:5899: {0}", _port) + "\n"
            + T("Połączenie WebSocket: {0}", d.ApiConnected ? T("aktywne") : T("brak")) + "\n"
            + T("Zgoda Remote Apps: {0}", d.Authorized ? T("potwierdzona") : T("niepotwierdzona")) + "\n"
            + T("Połączone serwery: {0}", d.Servers) + "\n"
            + T("Zdarzenia w bieżącej sesji: {0}; ostatnie: {1}", d.Events, At(d.LastEvent)) + "\n"
            + T("Ostatnie zdarzenie głosu: {0}", At(d.LastTalk)) + "\n\n" + tip;
    }
}
