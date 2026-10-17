using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using SystemCompanion.ViewModels;

namespace SystemCompanion.Views;

public sealed partial class ImprovementsPage : Page
{
    private readonly List<ToggleButton> _chips = new();

    public ImprovementsViewModel ViewModel { get; private set; }

    public ImprovementsPage()
    {
        ViewModel = new ImprovementsViewModel(new Models.SystemSnapshot(), new Models.BiosProfile());
        InitializeComponent();
        _chips.AddRange(new[] { ChipAll, ChipProgramming, ChipGaming, ChipAi, ChipEveryday });
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ShellViewModel shell)
        {
            ViewModel = new ImprovementsViewModel(shell.System, shell.Bios);
            Bindings.Update();
        }

        DispatcherQueue.TryEnqueue(() => PageScroll.ChangeView(null, 0, null, true));
    }

    private void Chip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked || clicked.Tag is not string tag)
        {
            return;
        }

        // behave like a radio group: exactly one chip stays checked
        foreach (var chip in _chips)
        {
            chip.IsChecked = ReferenceEquals(chip, clicked);
        }

        ViewModel.SetFilter(tag);
        Bindings.Update();
    }
}
