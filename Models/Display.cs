using Microsoft.UI.Xaml.Media;
using SystemCompanion.Styles;

namespace SystemCompanion.Models;

/// <summary>A small headline tile: label, big value, one-line explanation.</summary>
public sealed class FactTile
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "";
    public string Plain { get; init; } = "";
    public string State { get; init; } = "neutral";

    public Brush AccentBrush => Palette.ForState(State);
    public Brush AccentTint => Palette.Tint(State switch
    {
        "good" => Palette.Mint,
        "warn" => Palette.Amber,
        _ => Palette.Muted
    }, 0x1F);
}

/// <summary>One line inside a card: name, value, and what it means.</summary>
public sealed class InfoRow
{
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";
    public string Plain { get; init; } = "";
    public string State { get; init; } = "neutral";
    public bool Changed { get; init; }
    public bool HasPlain => !string.IsNullOrWhiteSpace(Plain);

    public Brush AccentBrush => Palette.ForState(State);

    public Microsoft.UI.Xaml.Visibility ChangedVisibility =>
        Changed ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public Microsoft.UI.Xaml.Visibility PlainVisibility =>
        HasPlain ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
}

/// <summary>A titled group of rows.</summary>
public sealed class SectionCard
{
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string Icon { get; init; } = "\uE9D9";
    public List<InfoRow> Rows { get; init; } = new();
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
    public Microsoft.UI.Xaml.Visibility SubtitleVisibility =>
        HasSubtitle ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
}

/// <summary>A suggestion card shown on the Improvements page.</summary>
public sealed class SuggestionCard
{
    public required Suggestion Model { get; init; }

    public string Title => Model.Title;
    public string Summary => Model.Summary;
    public string Why => Model.Why;
    public string HowTo => Model.HowTo;
    public string Current => Model.Current;
    public string Target => Model.Target;
    public string Benefit => Model.Benefit;
    public string Risk => Model.Risk;
    public string GoalLabel => Model.GoalLabel;
    public string StateLabel => Model.StateLabel;
    public string StateGlyph => Model.StateGlyph;
    public string Goal => Model.Goal.ToString();

    public Brush AccentBrush => Model.State switch
    {
        SuggestionState.AlreadyGood => Palette.Good,
        SuggestionState.WorthDoing => Palette.Warn,
        SuggestionState.Optional => new SolidColorBrush(Palette.Cyan),
        _ => Palette.Neutral
    };

    public Brush AccentTint => Model.State switch
    {
        SuggestionState.AlreadyGood => Palette.Tint(Palette.Mint, 0x1C),
        SuggestionState.WorthDoing => Palette.Tint(Palette.Amber, 0x1C),
        SuggestionState.Optional => Palette.Tint(Palette.Cyan, 0x18),
        _ => Palette.Tint(Palette.Muted, 0x18)
    };

    public Microsoft.UI.Xaml.Visibility RestartVisibility =>
        Model.NeedsRestart ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
}
