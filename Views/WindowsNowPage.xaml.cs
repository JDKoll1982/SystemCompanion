using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SystemCompanion.ViewModels;

namespace SystemCompanion.Views;

public sealed partial class WindowsNowPage : Page
{
    public WindowsNowViewModel ViewModel { get; private set; }

    public WindowsNowPage()
    {
        ViewModel = new WindowsNowViewModel(new Models.SystemSnapshot());
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ShellViewModel shell)
        {
            ViewModel = new WindowsNowViewModel(shell.System);
            Bindings.Update();
        }

        // Start at the top even if something in the layout grabs focus.
        DispatcherQueue.TryEnqueue(() => PageScroll.ChangeView(null, 0, null, true));
    }
}
