using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SystemCompanion.ViewModels;
using SystemCompanion.Views;
using Windows.Graphics;

namespace SystemCompanion;

public sealed partial class MainWindow : Window
{
    private readonly ShellViewModel _shell = new();

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

        appWindow.Resize(new SizeInt32(1500, 980));

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        LastScannedText.Text = $"Scanned {_shell.LastScanned}";
        BiosFooterText.Text = _shell.BiosSummary;

        var index = startPage.ToLowerInvariant() switch
        {
            "windows" => 1,
            "improve" or "improvements" => 2,
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
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

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

            default:
                HeaderTitle.Text = "What could be better";
                HeaderSubtitle.Text = "programming · gaming · AI";
                ContentFrame.Navigate(typeof(ImprovementsPage), _shell);
                break;
        }
    }
}
