using SystemCompanion.Models;

namespace SystemCompanion.ViewModels;

/// <summary>Everything the Windows page shows, described in ordinary words.</summary>
public sealed class WindowsNowViewModel : ObservableBase
{
    public WindowsNowViewModel(SystemSnapshot snapshot)
    {
        Snapshot = snapshot;

        var primaryGpu = snapshot.Graphics.Adapters.FirstOrDefault(a => !a.IsVirtual);
        var nvidia = snapshot.Graphics.Nvidia.FirstOrDefault();
        var vram = nvidia is not null ? nvidia.VramMb / 1024.0 : primaryGpu?.VramGb ?? 0;

        Headline = new List<FactTile>
        {
            new()
            {
                Label = "Processor",
                Value = $"{snapshot.Cpu.Cores} cores",
                Plain = $"{snapshot.Cpu.Threads} threads, up to {snapshot.Cpu.BaseMhz / 1000.0:0.0} GHz base",
                State = "good"
            },
            new()
            {
                Label = "Memory",
                Value = $"{snapshot.Memory.InstalledGb:0} GB",
                Plain = $"{snapshot.Memory.ModuleCount} sticks running at {snapshot.Memory.ConfiguredMt} MT/s",
                State = snapshot.Memory.ConfiguredMt >= 5600 ? "good" : "warn"
            },
            new()
            {
                Label = "Graphics",
                Value = vram > 0 ? $"{vram:0.#} GB" : "—",
                Plain = primaryGpu?.Name ?? "No graphics card found",
                State = "good"
            },
            new()
            {
                Label = "Windows",
                Value = $"build {snapshot.Windows.Build}",
                Plain = $"{snapshot.Windows.Edition} · installed {snapshot.Windows.InstallDate}",
                State = "neutral"
            }
        };

        Sections = BuildSections(snapshot, primaryGpu, nvidia);
    }

    public SystemSnapshot Snapshot { get; }
    public List<FactTile> Headline { get; }
    public List<SectionCard> Sections { get; }

    public string MachineName => string.IsNullOrWhiteSpace(Snapshot.ComputerName) ? "This PC" : Snapshot.ComputerName;
    public string Board => Snapshot.Identity.Board;
    public string BoardVersion => Snapshot.Identity.BoardVersion;
    public string BiosVersion => Snapshot.Identity.BiosVersion;
    public string BiosDate => Snapshot.Identity.BiosDate;
    public string ScannedAt => Snapshot.GeneratedLocal;
    public bool HasData => Snapshot.Cpu.Threads > 0;

    private static List<SectionCard> BuildSections(
        SystemSnapshot snapshot, GraphicsInfo? primaryGpu, NvidiaGpu? nvidia)
    {
        var sections = new List<SectionCard>();

        sections.Add(new SectionCard
        {
            Title = "Processor",
            Subtitle = "The chip that does the thinking",
            Icon = "\uE9D9",
            Rows = new List<InfoRow>
            {
                new() { Name = "Model", Value = snapshot.Cpu.Name, Plain = "Reported by the motherboard firmware." },
                new() { Name = "Cores and threads",
                        Value = $"{snapshot.Cpu.Cores} cores / {snapshot.Cpu.Threads} threads",
                        Plain = "More threads let more programs run at the same time." },
                new() { Name = "Base speed", Value = $"{snapshot.Cpu.BaseMhz} MHz",
                        Plain = "The guaranteed speed. It boosts higher on demand." },
                new() { Name = "Shared cache", Value = $"{snapshot.Cpu.L3CacheMb} MB",
                        Plain = "Fast memory inside the processor." }
            }
        });

        var memoryRows = new List<InfoRow>
        {
            new() { Name = "Installed", Value = $"{snapshot.Memory.InstalledGb:0} GB", Plain = $"Windows can use {snapshot.Memory.VisibleGb:0.#} GB of it." },
            new() { Name = "Sticks", Value = snapshot.Memory.ModuleCount.ToString(), Plain = "Four sticks fill every memory slot on this board." },
            new() { Name = "Running at", Value = $"{snapshot.Memory.ConfiguredMt} MT/s",
                    Plain = $"The sticks are rated for {snapshot.Memory.RatedMt} MT/s. Matching numbers mean the fast profile is working.",
                    State = snapshot.Memory.ConfiguredMt >= snapshot.Memory.RatedMt && snapshot.Memory.RatedMt > 0 ? "good" : "warn" }
        };
        foreach (var module in snapshot.Memory.Modules)
        {
            memoryRows.Add(new InfoRow
            {
                Name = module.Slot,
                Value = $"{module.SizeGb:0} GB at {module.SpeedMt} MT/s",
                Plain = module.Part
            });
        }

        sections.Add(new SectionCard
        {
            Title = "Memory",
            Subtitle = "Short-term working space",
            Icon = "\uE950",
            Rows = memoryRows
        });

        var gpuRows = new List<InfoRow>();
        foreach (var adapter in snapshot.Graphics.Adapters)
        {
            gpuRows.Add(new InfoRow
            {
                Name = adapter.Name,
                Value = adapter.IsVirtual
                    ? "virtual display"
                    : adapter.VramGb is > 0 ? $"{adapter.VramGb:0.#} GB" : "—",
                Plain = adapter.IsVirtual
                    ? "A software display used for remote sessions; it is not a real card."
                    : $"Driver {adapter.DriverVersion} from {adapter.DriverDate}"
            });
        }

        if (primaryGpu?.RefreshHz is > 0)
        {
            gpuRows.Add(new InfoRow
            {
                Name = "Screen mode",
                Value = $"{primaryGpu.CurrentWidth} × {primaryGpu.CurrentHeight} at {primaryGpu.RefreshHz} Hz",
                Plain = "Higher refresh rates make motion look smoother.",
                State = primaryGpu.RefreshHz >= 120 ? "good" : "warn"
            });
        }

        if (nvidia is not null)
        {
            gpuRows.Add(new InfoRow
            {
                Name = "Card status right now",
                Value = $"{nvidia.Temperature} °C, {nvidia.Utilization}% busy",
                Plain = $"{nvidia.UsedMb} MB of {nvidia.VramMb} MB graphics memory in use."
            });
        }

        sections.Add(new SectionCard
        {
            Title = "Graphics",
            Subtitle = "What draws the screen (and runs AI models)",
            Icon = "\uE7FC",
            Rows = gpuRows
        });

        var storageRows = new List<InfoRow>();
        foreach (var drive in snapshot.Storage.Drives)
        {
            storageRows.Add(new InfoRow
            {
                Name = drive.Model,
                Value = $"{drive.SizeGb:0} GB",
                Plain = drive.MediaType
            });
        }

        foreach (var volume in snapshot.Storage.Volumes)
        {
            var percent = volume.SizeGb <= 0 ? 100 : volume.FreeGb / volume.SizeGb * 100;
            storageRows.Add(new InfoRow
            {
                Name = string.IsNullOrWhiteSpace(volume.Label) ? volume.Letter : $"{volume.Letter} ({volume.Label})",
                Value = $"{volume.FreeGb:0} GB free of {volume.SizeGb:0} GB",
                Plain = $"{percent:0}% free.",
                State = percent < 20 ? "warn" : "good"
            });
        }

        sections.Add(new SectionCard
        {
            Title = "Storage",
            Subtitle = "Where everything lives",
            Icon = "\uE7F1",
            Rows = storageRows
        });

        var perf = snapshot.Performance;
        sections.Add(new SectionCard
        {
            Title = "Windows speed settings",
            Subtitle = "The switches that decide how eagerly Windows uses the hardware",
            Icon = "\uE945",
            Rows = new List<InfoRow>
            {
                new() { Name = "Power mode", Value = perf.PowerPlan,
                        Plain = "Ultimate Performance keeps the processor at full readiness.",
                        State = perf.PowerPlan.Contains("Ultimate") || perf.PowerPlan.Contains("High performance") ? "good" : "warn" },
                new() { Name = "Game Mode", Value = YesNo(perf.GameMode),
                        Plain = "Holds back background work while a game is playing.",
                        State = perf.GameMode == true ? "good" : "warn" },
                new() { Name = "Background game recording", Value = perf.GameDvrEnabled == false ? "Off" : YesNo(perf.GameDvrEnabled),
                        Plain = "Off means the GPU is not recording constantly.",
                        State = perf.GameDvrEnabled == false ? "good" : "warn" },
                new() { Name = "Hardware GPU scheduling", Value = YesNo(perf.HardwareScheduling),
                        Plain = "Lets the graphics card schedule its own work.",
                        State = perf.HardwareScheduling == true ? "good" : "warn" },
                new() { Name = "Memory integrity", Value = YesNo(perf.MemoryIntegrity),
                        Plain = "Security layer around the kernel; it costs a few percent of speed.",
                        State = perf.MemoryIntegrity == true ? "neutral" : "good" },
                new() { Name = "Virtualisation security", Value = YesNo(perf.VbsConfigured),
                        Plain = "Part of the same protection family." },
                new() { Name = "Antivirus exclusions", Value = perf.Defender.ExclusionCount.ToString(),
                        Plain = "Folders the scanner skips. Builds are much faster when they are excluded.",
                        State = perf.Defender.ExclusionCount >= 3 ? "good" : "warn" }
            }
        });

        sections.Add(new SectionCard
        {
            Title = "Windows itself",
            Subtitle = "Version and housekeeping",
            Icon = "\uE770",
            Rows = new List<InfoRow>
            {
                new() { Name = "Edition", Value = snapshot.Windows.Edition },
                new() { Name = "Version", Value = $"{snapshot.Windows.DisplayVersion} (build {snapshot.Windows.Build})" },
                new() { Name = "Installed", Value = snapshot.Windows.InstallDate },
                new() { Name = "Running for", Value = $"{snapshot.Windows.LastBootHours:0.#} hours",
                        Plain = "A restart clears memory leaks and finishes pending updates." },
                new() { Name = "Long file paths", Value = snapshot.Windows.LongPaths ? "Allowed" : "Blocked",
                        Plain = "Deep project folders need this allowed.",
                        State = snapshot.Windows.LongPaths ? "good" : "warn" },
                new() { Name = "Virtual memory", Value = snapshot.Windows.PageFile.Automatic ? "Managed by Windows" : "Set by hand",
                        State = snapshot.Windows.PageFile.Automatic ? "good" : "warn" }
            }
        });

        if (snapshot.Windows.StartupItems.Count > 0)
        {
            sections.Add(new SectionCard
            {
                Title = "Starts with Windows",
                Subtitle = $"{snapshot.Windows.StartupItems.Count} programs launch at sign-in",
                Icon = "\uE7E8",
                Rows = snapshot.Windows.StartupItems
                    .Select(item => new InfoRow
                    {
                        Name = item.Name,
                        Value = "",
                        Plain = item.Command
                    })
                    .ToList()
            });
        }

        var toolRows = new List<InfoRow>();
        void AddTool(string name, string? version, string plain)
        {
            toolRows.Add(new InfoRow
            {
                Name = name,
                Value = string.IsNullOrWhiteSpace(version) ? "not found" : version!,
                Plain = plain,
                State = string.IsNullOrWhiteSpace(version) ? "neutral" : "good"
            });
        }

        AddTool("dotnet", snapshot.Tooling.Dotnet, snapshot.DotnetSdks.Count > 0
            ? $"SDKs: {string.Join(", ", snapshot.DotnetSdks)}"
            : "The .NET build tool.");
        AddTool("git", snapshot.Tooling.Git, "Version control.");
        AddTool("node", snapshot.Tooling.Node, "JavaScript runtime.");
        AddTool("npm", snapshot.Tooling.Npm, "JavaScript package manager.");
        AddTool("python", snapshot.Tooling.Python, "Python interpreter.");
        AddTool("pip", snapshot.Tooling.Pip, "Python package manager.");
        AddTool("docker", snapshot.Tooling.Docker, "Container runtime.");
        AddTool("VS Code", snapshot.Tooling.Code, "Code editor command line.");
        AddTool("ollama", snapshot.Tooling.Ollama, snapshot.AiTools.OllamaDataDir
            ? "Local AI runner (model folder found, command not on PATH)."
            : "Local AI runner.");
        AddTool("nvidia-smi", snapshot.Tooling.Nvidia, "NVIDIA driver reporting tool.");

        sections.Add(new SectionCard
        {
            Title = "Developer tools",
            Subtitle = "What is installed and ready",
            Icon = "\uE943",
            Rows = toolRows
        });

        return sections;
    }

    private static string YesNo(bool? value) => value switch
    {
        true => "On",
        false => "Off",
        _ => "Unknown"
    };
}
