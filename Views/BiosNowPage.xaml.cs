using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SystemCompanion.ViewModels;

namespace SystemCompanion.Views;

public sealed partial class BiosNowPage : Page
{
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
}
