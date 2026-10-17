using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SystemCompanion.ViewModels;

namespace SystemCompanion.Views;

public sealed partial class HistoryPage : Page
{
    /// <summary>
    /// The page width at which two pickers fit side by side. The reflow runs from
    /// SizeChanged rather than an AdaptiveTrigger: the trigger did not fire on this
    /// page in WinUI 3, and clipped content is worse than an explicit measurement.
    /// </summary>
    private const double WideLayoutWidth = 660;

    private string? _leftPath;
    private string? _rightPath;
    private bool _syncing;

    public HistoryViewModel ViewModel { get; private set; }

    public HistoryPage()
    {
        ViewModel = new HistoryViewModel(Array.Empty<Services.HistoryEntry>());
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ShellViewModel shell)
        {
            Rebuild(shell);
        }

        DispatcherQueue.TryEnqueue(() => PageScroll.ChangeView(null, 0, null, true));
    }

    /// <summary>Rebuilds from the shell's current readings and keeps the pair the person picked.</summary>
    private void Rebuild(ShellViewModel shell)
    {
        ViewModel = new HistoryViewModel(shell.History, shell.SkippedReadings, _leftPath, _rightPath);
        Bindings.Update();
        SyncPickers();
    }

    private void HistoryPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= WideLayoutWidth;

        Grid.SetRow(LeftPicker, 0);
        Grid.SetColumn(LeftPicker, 0);
        Grid.SetColumnSpan(LeftPicker, wide ? 1 : 2);

        Grid.SetRow(RightPicker, wide ? 0 : 1);
        Grid.SetColumn(RightPicker, wide ? 1 : 0);
        Grid.SetColumnSpan(RightPicker, wide ? 1 : 2);
    }

    private void SyncPickers()
    {
        _syncing = true;
        LeftPicker.SelectedItem = ViewModel.Left;
        RightPicker.SelectedItem = ViewModel.Right;
        _syncing = false;
    }

    private void LeftPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || LeftPicker.SelectedItem is not Services.HistoryEntry entry)
        {
            return;
        }

        _leftPath = entry.Path;
        ViewModel.SelectByPath(_leftPath, _rightPath ?? ViewModel.Right?.Path);
    }

    private void RightPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || RightPicker.SelectedItem is not Services.HistoryEntry entry)
        {
            return;
        }

        _rightPath = entry.Path;
        ViewModel.SelectByPath(_leftPath ?? ViewModel.Left?.Path, _rightPath);
    }
}
