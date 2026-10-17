using SystemCompanion.Models;
using SystemCompanion.Services;

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
                Label = Display.Text(f.Label),
                Value = Display.Text(f.Value),
                Plain = string.IsNullOrWhiteSpace(f.Plain) ? "This is what the saved settings file says." : f.Plain,
                State = f.State
            })
            .ToList();

        Sections = profile.Sections
            .Select(section => new SectionCard
            {
                Title = Display.Text(section.Title),
                Subtitle = section.Subtitle,
                Icon = section.Icon,
                Rows = section.Items
                    .Select(item => new InfoRow
                    {
                        Name = Display.Text(item.Name),
                        Value = Display.Text(item.Value),
                        Plain = string.IsNullOrWhiteSpace(item.Plain)
                            ? "The saved settings file has no explanation for this value yet."
                            : item.Plain,
                        State = item.State,
                        Changed = item.Changed
                    })
                    .ToList()
            })
            .ToList();
    }

    public BiosProfile Profile { get; }

    public string FileName => Display.Text(Profile.File.Name);
    public string Board => Display.Text(Profile.File.Board);
    public string BiosVersion => Display.Text(Profile.File.BiosVersion);
    public string SavedOn => Display.Text(Profile.File.SavedOn);
    public string SavedOnDay => Profile.File.SavedOnDay;
    public string Cpu => Display.Text(Profile.File.Cpu);
    public string Checksum => Display.Text(Profile.File.Checksum);
    public bool IsValid => Profile.File.Valid;
    public string ValidityText => Profile.File.Valid ? "File is healthy" : "File looks damaged";
    public string SizeText => Profile.File.SizeBytes > 0 ? $"{Profile.File.SizeBytes:N0} bytes" : Display.Unknown;
    public string VariableText => Profile.File.SavedVariables > 0 ? $"{Profile.File.SavedVariables} saved groups" : Display.Unknown;
    public string ChangedText => Profile.File.Valid ? $"{Profile.File.ChangedBytes} bytes differ from factory" : Display.Unknown;
    public string SavedLine => string.IsNullOrWhiteSpace(SavedOnDay)
        ? SavedOn
        : $"{SavedOn} ({SavedOnDay})";

    public List<FactTile> Headline { get; }
    public List<SectionCard> Sections { get; }

    public bool HasData => Sections.Count > 0;

    /// <summary>True when the file's firmware is not the one the explanations were built from.</summary>
    public bool HasFirmwareWarning => !FirmwareReference.Matches(Profile.File.BiosVersion);

    /// <summary>
    /// Reference-derived names are only shown when the tag matches, so a different
    /// firmware is never presented with names decoded from another file.
    /// </summary>
    public bool ShowsDecodedValues => !HasFirmwareWarning;

    public string FirmwareWarning
    {
        get
        {
            if (!HasFirmwareWarning)
            {
                return "";
            }

            var fileTag = string.IsNullOrWhiteSpace(Profile.File.BiosVersion)
                ? Display.Unknown
                : Profile.File.BiosVersion;

            return $"This file reports firmware {fileTag}. The setting names on this screen were "
                   + $"decoded from {FirmwareReference.SupportedTag}, so they are left out rather than "
                   + "shown against the wrong firmware.";
        }
    }
}
