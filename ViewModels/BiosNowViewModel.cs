using SystemCompanion.Models;

namespace SystemCompanion.ViewModels;

/// <summary>Turns the plain-language BIOS summary into cards the page can bind to.</summary>
public sealed class BiosNowViewModel : ObservableBase
{
    public BiosNowViewModel(BiosProfile profile)
    {
        Profile = profile;
        Headline = profile.Headline
            .Select(f => new FactTile
            {
                Label = f.Label,
                Value = f.Value,
                Plain = f.Plain,
                State = f.State
            })
            .ToList();

        Sections = profile.Sections
            .Select(section => new SectionCard
            {
                Title = section.Title,
                Subtitle = section.Subtitle,
                Icon = section.Icon,
                Rows = section.Items
                    .Select(item => new InfoRow
                    {
                        Name = item.Name,
                        Value = item.Value,
                        Plain = item.Plain,
                        State = item.State,
                        Changed = item.Changed
                    })
                    .ToList()
            })
            .ToList();
    }

    public BiosProfile Profile { get; }

    public string FileName => Profile.File.Name;
    public string Board => Profile.File.Board;
    public string BiosVersion => Profile.File.BiosVersion;
    public string SavedOn => Profile.File.SavedOn;
    public string SavedOnDay => Profile.File.SavedOnDay;
    public string Cpu => Profile.File.Cpu;
    public string Checksum => Profile.File.Checksum;
    public bool IsValid => Profile.File.Valid;
    public string ValidityText => Profile.File.Valid ? "File is healthy" : "File looks damaged";
    public string SizeText => $"{Profile.File.SizeBytes:N0} bytes";
    public string VariableText => $"{Profile.File.SavedVariables} saved groups";
    public string ChangedText => $"{Profile.File.ChangedBytes} bytes differ from factory";
    public string SavedLine => string.IsNullOrWhiteSpace(SavedOnDay)
        ? SavedOn
        : $"{SavedOn} ({SavedOnDay})";

    public List<FactTile> Headline { get; }
    public List<SectionCard> Sections { get; }

    public bool HasData => Sections.Count > 0;
}
