using System.Collections.ObjectModel;
using SystemCompanion.Models;
using SystemCompanion.Services;

namespace SystemCompanion.ViewModels;

/// <summary>Builds and filters the improvement suggestions.</summary>
public sealed class ImprovementsViewModel : ObservableBase
{
    private readonly List<Suggestion> _all;
    private string _filter = "All";
    private int _visibleCount;

    public ImprovementsViewModel(SystemSnapshot system, BiosProfile bios)
    {
        _all = new RecommendationEngine(system, bios, AppData.ReadBiosValues()).Build().ToList();
        Refresh();

        AlreadyGoodCount = _all.Count(s => s.State == SuggestionState.AlreadyGood);
        WorthDoingCount = _all.Count(s => s.State == SuggestionState.WorthDoing);
        OptionalCount = _all.Count(s => s.State == SuggestionState.Optional);
        CheckCount = _all.Count(s => s.State == SuggestionState.CheckYourself);
    }

    public ObservableCollection<SuggestionCard> Items { get; } = new();

    public int AlreadyGoodCount { get; }
    public int WorthDoingCount { get; }
    public int OptionalCount { get; }
    public int CheckCount { get; }
    public int TotalCount => _all.Count;

    public string HeadlineValue => WorthDoingCount == 0 ? "Nothing urgent" : $"{WorthDoingCount} worth doing";

    public string HeadlinePlain => WorthDoingCount == 0
        ? "This machine is already set up well for programming, gaming and AI work."
        : "Changes with a measurable benefit, ordered so the biggest wins come first.";

    public string ProgressText => $"{AlreadyGoodCount} of {TotalCount} checks already pass";

    public string Filter
    {
        get => _filter;
        private set
        {
            if (Set(ref _filter, value))
            {
                Refresh();
            }
        }
    }

    public string VisibleText => _visibleCount == 0
        ? "Nothing in this group"
        : $"{_visibleCount} suggestion{(_visibleCount == 1 ? "" : "s")}";

    public void SetFilter(string filter)
    {
        Filter = filter;
    }

    private void Refresh()
    {
        var wanted = _filter;

        var filtered = _all
            .Where(s => wanted == "All" || s.Goals.Any(g => g.ToString() == wanted))
            .OrderBy(s => s.State switch
            {
                SuggestionState.WorthDoing => 0,
                SuggestionState.Optional => 1,
                SuggestionState.CheckYourself => 2,
                _ => 3
            })
            .ThenBy(s => s.Title)
            .ToList();

        Items.Clear();
        foreach (var suggestion in filtered)
        {
            Items.Add(new SuggestionCard { Model = suggestion });
        }

        _visibleCount = filtered.Count;
        Raise(nameof(VisibleText));
    }
}
