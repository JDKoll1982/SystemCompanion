using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SystemCompanion.ViewModels;
using SystemCompanion.Views;
using SystemCompanion.Views.Controls;
using Windows.Graphics;

namespace SystemCompanion;

public sealed partial class MainWindow : Window
{
    private readonly ShellViewModel _shell = new();
    private readonly RescanBar _rescanBar = new();
    private string _currentTag = "bios";

    public MainWindow(string startPage = "bios")
    {
        InitializeComponent();

        Title = "System Companion";
        TryEnableMica();

        var appWindow = AppWindow;
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 900;
            presenter.PreferredMinimumHeight = 640;
        }

        SizeToWorkArea(appWindow);

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        ScanBarHost.Content = _rescanBar;
        _rescanBar.BindTo(_shell, ScanFinished);
        UpdateShellTexts();

        var index = startPage.ToLowerInvariant() switch
        {
            "windows" => 1,
            "improve" or "improvements" => 2,
            "history" => 3,
            _ => 0
        };

        Nav.SelectedItem = Nav.MenuItems[index];
    }

    private void TryEnableMica()
    {
        try
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
        }
        catch (Exception)
        {
            // Mica is unavailable on some builds; the ink background still looks right.
        }
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            ShowPage(tag);
        }
    }

    /// <summary>
    /// Opens at a comfortable size without ever exceeding the screen it lands on.
    /// Windows display scaling is applied on top of these effective pixels, so a
    /// large default would otherwise push the window past the edge of the display.
    /// </summary>
    private static void SizeToWorkArea(AppWindow appWindow)
    {
        var work = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(1500, work.Width);
        var height = Math.Min(980, work.Height);

        appWindow.MoveAndResize(new RectInt32(
            work.X + (work.Width - width) / 2,
            work.Y + (work.Height - height) / 2,
            width,
            height));
    }

    /// <summary>Shows a route. Re-showing the current route rebuilds the page from the shell.</summary>
    private void ShowPage(string tag)
    {
        _currentTag = tag;

        switch (tag)
        {
            case "bios":
                HeaderTitle.Text = "BIOS — right now";
                HeaderSubtitle.Text = _shell.HasBiosData
                    ? $"{_shell.Bios.File.Board} · profile saved {_shell.Bios.File.SavedOn}"
                    : "no profile loaded";
                ContentFrame.Navigate(typeof(BiosNowPage), _shell);
                break;

            case "windows":
                HeaderTitle.Text = "Windows — right now";
                HeaderSubtitle.Text = _shell.HasSystemData
                    ? $"{_shell.MachineName} · scanned {_shell.LastScanned}"
                    : "run the scan to fill this in";
                ContentFrame.Navigate(typeof(WindowsNowPage), _shell);
                break;

            case "history":
                HeaderTitle.Text = "What changed";
                HeaderSubtitle.Text = _shell.History.Count < 2
                    ? "a second reading is needed"
                    : $"{_shell.History.Count} readings kept";
                ContentFrame.Navigate(typeof(HistoryPage), _shell);
                break;

            default:
                HeaderTitle.Text = "What could be better";
                HeaderSubtitle.Text = "programming · gaming · AI";
                ContentFrame.Navigate(typeof(ImprovementsPage), _shell);
                break;
        }
    }

    private void ScanFinished(bool succeeded)
    {
        UpdateShellTexts();

        if (succeeded)
        {
            ShowPage(_currentTag);
        }
    }

    private void Changes_Click(object sender, RoutedEventArgs e)
    {
        Nav.SelectedItem = Nav.MenuItems[3];
    }

    /// <summary>Refreshes the footer and the change statement from the shell's current state.</summary>
    private void UpdateShellTexts()
    {
        LastScannedText.Text = $"Scanned {_shell.LastScanned}";
        BiosFooterText.Text = _shell.BiosSummary;
        ChangesLink.Content = _shell.ChangesText;
        ChangesLink.Visibility = _shell.HasChanges ? Visibility.Visible : Visibility.Collapsed;
    }
}
