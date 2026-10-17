using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using SystemCompanion.ViewModels;

namespace SystemCompanion.Views;

public sealed partial class ImprovementsPage : Page
{
    /// <summary>The page width below which the overall score card drops under the headline.</summary>
    private const double WideHeroWidth = 720;

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

    private void ImprovementsPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= WideHeroWidth;

        Grid.SetRow(OverallPanel, wide ? 0 : 1);
        Grid.SetColumn(OverallPanel, wide ? 1 : 0);
        Grid.SetColumnSpan(OverallPanel, wide ? 1 : 2);
        OverallPanel.HorizontalAlignment = wide
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Left;
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
