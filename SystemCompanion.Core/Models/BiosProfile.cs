namespace SystemCompanion.Models;

/// <summary>
/// A friendly, plain-language translation of the saved BIOS profile.
/// Produced by tools/build-bios-summary.py from the decoded profile.
/// </summary>
public sealed class BiosProfile
{
    public string GeneratedAt { get; set; } = "";
    public BiosFileInfo File { get; set; } = new();
    public List<BiosFact> Headline { get; set; } = new();
    public List<BiosSection> Sections { get; set; } = new();
}

public sealed class BiosFileInfo
{
    public string Name { get; set; } = "";
    public string Board { get; set; } = "";
    public string BiosVersion { get; set; } = "";
    public string SavedOn { get; set; } = "";
    public string SavedOnDay { get; set; } = "";
    public bool Valid { get; set; }
    public string Checksum { get; set; } = "";
    public int SizeBytes { get; set; }
    public int SavedVariables { get; set; }
    public int ChangedBytes { get; set; }
    public string Cpu { get; set; } = "";
}

/// <summary>One headline tile: a short label, its value, and how to read it.</summary>
public sealed class BiosFact
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string Plain { get; set; } = "";
    public string State { get; set; } = "neutral"; // good | warn | neutral
}

public sealed class BiosSection
{
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Icon { get; set; } = "\uE9D9";
    public List<BiosEntry> Items { get; set; } = new();
}

public sealed class BiosEntry
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Plain { get; set; } = "";
    public string State { get; set; } = "neutral";
    public bool Changed { get; set; }
}
