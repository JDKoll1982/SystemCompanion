using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SystemCompanion.ViewModels;

namespace SystemCompanion.Views;

public sealed partial class BiosNowPage : Page
{
    /// <summary>The page width below which the four identity pills stack instead of running across.</summary>
    private const double WidePillsWidth = 640;

    public BiosNowViewModel ViewModel { get; private set; }

    public BiosNowPage()
    {
        ViewModel = new BiosNowViewModel(new Models.BiosProfile());
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ShellViewModel shell)
        {
            ViewModel = new BiosNowViewModel(shell.Bios);
            Bindings.Update();
        }

        DispatcherQueue.TryEnqueue(() => PageScroll.ChangeView(null, 0, null, true));
    }

    private void BiosPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        PillStack.Orientation = e.NewSize.Width >= WidePillsWidth
            ? Orientation.Horizontal
            : Orientation.Vertical;
    }
}
