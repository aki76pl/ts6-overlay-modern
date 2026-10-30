using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static TS6Overlay.L;

namespace TS6Overlay;

internal sealed class UpdateWindow : Window
{
    public UpdateWindow(Updater.Release release, Func<IProgress<int>, Task> install)
    {
        Title = T("Aktualizacja TS6 Overlay Modern");
        Width = 620; Height = 540; MinWidth = 420; MinHeight = 350;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Theme.B("#FF101827"); Foreground = Brushes.White;
        var panel = new DockPanel { Margin = new Thickness(24) };
        Content = panel;
        var title = new TextBlock { Text = T("Wersja {0} → {1}", Updater.Current, release.Version), FontSize = 23, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(title, Dock.Top); panel.Children.Add(title);
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); panel.Children.Add(footer);
        footer.Children.Add(new TextBlock { Text = T("Ustawienia, motywy i profile pozostaną zachowane. Po pobraniu program uruchomi się ponownie."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) });
        var progress = new ProgressBar { Height = 8, Maximum = 100, Visibility = Visibility.Collapsed };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
        footer.Children.Add(progress); footer.Children.Add(status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var later = new Button { Content = T("Później"), Padding = new Thickness(18, 9, 18, 9), Margin = new Thickness(0, 0, 10, 0) };
        var download = new Button { Content = T("Pobierz i zaktualizuj"), Padding = new Thickness(18, 9, 18, 9), Background = Brushes.Turquoise };
        bool busy = false;
        later.Click += (_, _) => Close();
        Closing += (_, e) => { if (busy) e.Cancel = true; };
        download.Click += async (_, _) =>
        {
            busy = true; later.IsEnabled = download.IsEnabled = false;
            progress.Visibility = Visibility.Visible; progress.IsIndeterminate = true;
            status.Text = T("Pobieranie aktualizacji…");
            try
            {
                await install(new Progress<int>(value => { progress.IsIndeterminate = false; progress.Value = value; status.Text = T("Pobieranie: {0}%", value); }));
                busy = false; Close();
            }
            catch (Exception ex)
            {
                busy = false; later.IsEnabled = download.IsEnabled = true;
                progress.IsIndeterminate = false;
                status.Text = T("Aktualizacja nie powiodła się: {0}", ex.Message);
            }
        };
        buttons.Children.Add(later); buttons.Children.Add(download); footer.Children.Add(buttons);
        panel.Children.Add(new TextBox { Text = string.IsNullOrWhiteSpace(release.Notes) ? T("Brak opisu zmian dla tego wydania.") : release.Notes,
            IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Theme.B("#FF192538"), Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(14) });
    }
}
