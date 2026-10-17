namespace SystemCompanion.Models;

/// <summary>Which kind of work a suggestion helps with.</summary>
public enum Goal
{
    Programming,
    Gaming,
    Ai,
    Everyday
}

/// <summary>How a suggestion relates to the machine right now.</summary>
public enum SuggestionState
{
    /// <summary>Already set up the way we would recommend.</summary>
    AlreadyGood,

    /// <summary>Worth doing; measurable benefit.</summary>
    WorthDoing,

    /// <summary>Optional polish, small or situational benefit.</summary>
    Optional,

    /// <summary>Cannot be checked automatically; the reader must look.</summary>
    CheckYourself
}

public sealed class Suggestion
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string Why { get; init; }
    public required string HowTo { get; init; }
    public required Goal Goal { get; init; }

    /// <summary>Other goals this advice also helps with, so it is not repeated.</summary>
    public Goal[] AlsoHelps { get; init; } = Array.Empty<Goal>();

    public IEnumerable<Goal> Goals => new[] { Goal }.Concat(AlsoHelps);

    public required SuggestionState State { get; init; }

    /// <summary>Current reading, e.g. "Ultimate Performance".</summary>
    public string Current { get; init; } = "";

    /// <summary>What it would read once done, e.g. "High performance".</summary>
    public string Target { get; init; } = "";

    /// <summary>Expected feel of the change, in plain words.</summary>
    public string Benefit { get; init; } = "";

    /// <summary>Risk in plain words: "Safe", "Changes behaviour", "Advanced".</summary>
    public string Risk { get; init; } = "Safe";

    /// <summary>True when the change needs a restart.</summary>
    public bool NeedsRestart { get; init; }

    public string GoalLabel => Goal switch
    {
        Goal.Programming => "Programming",
        Goal.Gaming => "Gaming",
        Goal.Ai => "AI work",
        _ => "Everyday"
    };

    public string StateLabel => State switch
    {
        SuggestionState.AlreadyGood => "Already set up",
        SuggestionState.WorthDoing => "Worth doing",
        SuggestionState.Optional => "Optional",
        _ => "Check yourself"
    };

    public string StateGlyph => State switch
    {
        SuggestionState.AlreadyGood => "\uE73E",   // check
        SuggestionState.WorthDoing => "\uE945",    // lightning
        SuggestionState.Optional => "\uE946",      // info
        _ => "\uE9CE"                              // search
    };
}
