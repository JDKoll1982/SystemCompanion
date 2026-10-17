using SystemCompanion.Models;
using SystemCompanion.Services;

namespace SystemCompanion.ViewModels;

/// <summary>Shared state for the whole window: the two data files, the kept readings and where they came from.</summary>
public sealed class ShellViewModel : ObservableBase
{
    private readonly HistoryService _historyService = new();
    private readonly ScanRunner _scanRunner = new();
    private SystemSnapshot _system = new();
    private BiosProfile _bios = new();
    private IReadOnlyList<HistoryEntry> _history = Array.Empty<HistoryEntry>();
    private IReadOnlyList<HistoryEntry> _skippedReadings = Array.Empty<HistoryEntry>();
    private int _changesSinceLastScan;
    private bool _isScanning;
    private string _scanMessage = "";

    public ShellViewModel()
    {
        Reload();
    }

    public SystemSnapshot System => _system;
    public BiosProfile Bios => _bios;
    public bool HasSystemData => _system.Cpu.Threads > 0;
    public bool HasBiosData => _bios.Sections.Count > 0;

    /// <summary>Every kept reading, oldest first.</summary>
    public IReadOnlyList<HistoryEntry> History => _history;

    /// <summary>History files that could not be read, each carrying its plain-language note.</summary>
    public IReadOnlyList<HistoryEntry> SkippedReadings => _skippedReadings;

    public int ChangesSinceLastScan => _changesSinceLastScan;

    public bool HasChanges => _changesSinceLastScan > 0;

    public string ChangesText => _changesSinceLastScan switch
    {
        0 => "",
        1 => "1 thing changed since your last scan",
        _ => $"{_changesSinceLastScan} things changed since your last scan"
    };

    public string MachineName => string.IsNullOrWhiteSpace(_system.ComputerName) ? "This PC" : _system.ComputerName;

    public string MachineSummary
    {
        get
        {
            if (!HasSystemData)
            {
                return "No scan yet - run tools\\discover-system.ps1 to fill this in.";
            }

            return $"{_system.Identity.Board}  ·  {_system.Cpu.Name}  ·  {_system.Memory.InstalledGb:0} GB memory  ·  {_system.Windows.Edition}";
        }
    }

    public string LastScanned => string.IsNullOrWhiteSpace(_system.GeneratedLocal)
        ? "not scanned yet"
        : _system.GeneratedLocal;

    public string BiosSummary => HasBiosData
        ? $"BIOS profile saved {_bios.File.SavedOn}"
        : "No BIOS profile loaded";

    public bool IsScanning => _isScanning;

    public bool CanScan => !_isScanning;

    /// <summary>Shown under the rescan action: progress while running, then the outcome.</summary>
    public string ScanMessage => _scanMessage;

    public string ScanButtonText => _isScanning ? "Reading this PC..." : "Take a new reading";

    /// <summary>
    /// Runs one scan, then reloads on success. A failed scan changes nothing on screen
    /// and says so. A second request while one runs is ignored.
    /// </summary>
    public async Task<bool> RescanAsync()
    {
        if (_isScanning)
        {
            return false;
        }

        SetScanning(true);
        _scanMessage = "Reading this PC...";
        Raise(nameof(ScanMessage));

        var succeeded = false;
        try
        {
            var result = await _scanRunner.RunAsync();
            succeeded = result.Succeeded;
            if (succeeded)
            {
                Reload();
            }

            _scanMessage = result.Message;
        }
        finally
        {
            SetScanning(false);
            Raise(nameof(ScanMessage));
        }

        return succeeded;
    }

    private void SetScanning(bool value)
    {
        _isScanning = value;
        Raise(nameof(IsScanning));
        Raise(nameof(CanScan));
        Raise(nameof(ScanButtonText));
    }

    /// <summary>Re-reads every file and rebuilds the change count. Never writes anything.</summary>
    public void Reload()
    {
        _system = AppData.ReadJson<SystemSnapshot>("current", "system-snapshot.json") ?? new SystemSnapshot();
        _bios = AppData.ReadJson<BiosProfile>("bios", "bios-profile.json") ?? new BiosProfile();
        var all = _historyService.ReadAllEntries(AppData.PathFor("history"));
        _history = _historyService.Order(all);
        _skippedReadings = all.Where(entry => !entry.IsReadable).ToList();
        _changesSinceLastScan = CountChangesSinceLastScan();

        Raise(nameof(System));
        Raise(nameof(Bios));
        Raise(nameof(HasSystemData));
        Raise(nameof(HasBiosData));
        Raise(nameof(History));
        Raise(nameof(SkippedReadings));
        Raise(nameof(ChangesSinceLastScan));
        Raise(nameof(HasChanges));
        Raise(nameof(ChangesText));
        Raise(nameof(MachineName));
        Raise(nameof(MachineSummary));
        Raise(nameof(LastScanned));
        Raise(nameof(BiosSummary));
    }

    private int CountChangesSinceLastScan()
    {
        if (_history.Count < 2)
        {
            return 0;
        }

        var previous = _history[^2];
        var newest = _history[^1];
        return _historyService.Compare(previous, newest).Changed.Count;
    }
}
