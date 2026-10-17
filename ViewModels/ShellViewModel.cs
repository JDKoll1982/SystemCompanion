using SystemCompanion.Models;
using SystemCompanion.Services;

namespace SystemCompanion.ViewModels;

/// <summary>Shared state for the whole window: the two data files and where they came from.</summary>
public sealed class ShellViewModel : ObservableBase
{
    public SystemSnapshot System { get; }
    public BiosProfile Bios { get; }
    public bool HasSystemData { get; }
    public bool HasBiosData { get; }

    public ShellViewModel()
    {
        System = AppData.ReadJson<SystemSnapshot>("current", "system-snapshot.json") ?? new SystemSnapshot();
        Bios = AppData.ReadJson<BiosProfile>("bios", "bios-profile.json") ?? new BiosProfile();
        HasSystemData = System.Cpu.Threads > 0;
        HasBiosData = Bios.Sections.Count > 0;
    }

    public string MachineName => string.IsNullOrWhiteSpace(System.ComputerName) ? "This PC" : System.ComputerName;

    public string MachineSummary
    {
        get
        {
            if (!HasSystemData)
            {
                return "No scan yet — run tools\\discover-system.ps1 to fill this in.";
            }

            return $"{System.Identity.Board}  ·  {System.Cpu.Name}  ·  {System.Memory.InstalledGb:0} GB memory  ·  {System.Windows.Edition}";
        }
    }

    public string LastScanned => string.IsNullOrWhiteSpace(System.GeneratedLocal)
        ? "not scanned yet"
        : System.GeneratedLocal;

    public string BiosSummary => HasBiosData
        ? $"BIOS profile saved {Bios.File.SavedOn}"
        : "No BIOS profile loaded";
}
