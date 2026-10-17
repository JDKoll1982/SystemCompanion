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
                Value = Display.Number(snapshot.Cpu.Cores > 0 ? snapshot.Cpu.Cores : null, "cores"),
                Plain = snapshot.Cpu.Threads > 0
                    ? $"{snapshot.Cpu.Threads} threads, up to {snapshot.Cpu.BaseMhz / 1000.0:0.0} GHz base"
                    : "The scan did not read the thread count.",
                State = "good"
            },
            new()
            {
                Label = "Memory",
                Value = Display.Number(snapshot.Memory.InstalledGb > 0 ? snapshot.Memory.InstalledGb : null, "GB", "0"),
                Plain = snapshot.Memory.ModuleCount > 0
                    ? $"{snapshot.Memory.ModuleCount} sticks running at {Display.Number(snapshot.Memory.ConfiguredMt > 0 ? snapshot.Memory.ConfiguredMt : null, "MT/s")}"
                    : "The scan did not read the memory sticks.",
                State = snapshot.Memory.ConfiguredMt >= 5600 ? "good" : "warn"
            },
            new()
            {
                Label = "Graphics",
                Value = Display.Number(vram > 0 ? vram : null, "GB"),
                Plain = primaryGpu?.Name ?? "No graphics card was reported by the scan.",
                State = "good"
            },
            new()
            {
                Label = "Windows",
                Value = string.IsNullOrWhiteSpace(snapshot.Windows.Build) ? Display.Unknown : $"build {snapshot.Windows.Build}",
                Plain = $"{Display.Text(snapshot.Windows.Edition)} · installed {Display.Text(snapshot.Windows.InstallDate)}",
                State = "neutral"
            }
        };

        Sections = BuildSections(snapshot, primaryGpu, nvidia);
    }

    public SystemSnapshot Snapshot { get; }
    public List<FactTile> Headline { get; }
    public List<SectionCard> Sections { get; }

    public string MachineName => string.IsNullOrWhiteSpace(Snapshot.ComputerName) ? "This PC" : Snapshot.ComputerName;
    public string Board => Display.Text(Snapshot.Identity.Board);
    public string BoardVersion => Display.Text(Snapshot.Identity.BoardVersion);
    public string BiosVersion => Display.Text(Snapshot.Identity.BiosVersion);
    public string BiosDate => Display.Text(Snapshot.Identity.BiosDate);
    public string ScannedAt => Display.Text(Snapshot.GeneratedLocal);
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
                new() { Name = "Model", Value = Display.Text(snapshot.Cpu.Name), Plain = "Reported by the motherboard firmware." },
                new() { Name = "Cores and threads",
                        Value = snapshot.Cpu.Threads > 0
                            ? $"{snapshot.Cpu.Cores} cores / {snapshot.Cpu.Threads} threads"
                            : Display.Unknown,
                        Plain = "More threads let more programs run at the same time." },
                new() { Name = "Base speed",
                        Value = Display.Number(snapshot.Cpu.BaseMhz > 0 ? snapshot.Cpu.BaseMhz : null, "MHz"),
                        Plain = "The guaranteed speed. It boosts higher on demand." },
                new() { Name = "Shared cache",
                        Value = Display.Number(snapshot.Cpu.L3CacheMb > 0 ? snapshot.Cpu.L3CacheMb : null, "MB"),
                        Plain = "Fast memory inside the processor." }
            }
        });

        var memoryRows = new List<InfoRow>
        {
            new() { Name = "Installed",
                    Value = Display.Number(snapshot.Memory.InstalledGb > 0 ? snapshot.Memory.InstalledGb : null, "GB", "0"),
                    Plain = snapshot.Memory.VisibleGb > 0
                        ? $"Windows can use {snapshot.Memory.VisibleGb:0.#} GB of it."
                        : "The scan did not read how much Windows can use." },
            new() { Name = "Sticks",
                    Value = Display.Number(snapshot.Memory.ModuleCount > 0 ? snapshot.Memory.ModuleCount : null),
                    Plain = "How many memory sticks the board reports." },
            new() { Name = "Running at",
                    Value = Display.Number(snapshot.Memory.ConfiguredMt > 0 ? snapshot.Memory.ConfiguredMt : null, "MT/s"),
                    Plain = snapshot.Memory.RatedMt > 0
                        ? $"The sticks are rated for {snapshot.Memory.RatedMt} MT/s. Matching numbers mean the fast profile is working."
                        : "The scan did not read the speed the sticks are rated for.",
                    State = snapshot.Memory.ConfiguredMt >= snapshot.Memory.RatedMt && snapshot.Memory.RatedMt > 0 ? "good" : "warn" }
        };
        foreach (var module in snapshot.Memory.Modules)
        {
            memoryRows.Add(new InfoRow
            {
                Name = Display.Text(module.Slot),
                Value = $"{Display.Number(module.SizeGb > 0 ? module.SizeGb : null, "GB", "0")} at {Display.Number(module.SpeedMt > 0 ? module.SpeedMt : null, "MT/s")}",
                Plain = string.IsNullOrWhiteSpace(module.Part)
                    ? "The scan did not read this stick's part number."
                    : $"Part number {module.Part}."
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
                Name = Display.Text(adapter.Name),
                Value = adapter.IsVirtual
                    ? "virtual display"
                    : Display.Number(adapter.VramGb is > 0 ? adapter.VramGb : null, "GB"),
                Plain = adapter.IsVirtual
                    ? "A software display used for remote sessions; it is not a real card."
                    : $"Driver {Display.Text(adapter.DriverVersion)} from {Display.Text(adapter.DriverDate)}"
            });
        }

        if (primaryGpu?.RefreshHz is > 0)
        {
            gpuRows.Add(new InfoRow
            {
                Name = "Screen mode",
                Value = $"{Display.Number(primaryGpu.CurrentWidth)} × {Display.Number(primaryGpu.CurrentHeight)} at {primaryGpu.RefreshHz} Hz",
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
                Name = Display.Text(drive.Model),
                Value = Display.Number(drive.SizeGb > 0 ? drive.SizeGb : null, "GB", "0"),
                Plain = Display.Text(drive.MediaType)
            });
        }

        foreach (var volume in snapshot.Storage.Volumes)
        {
            var percent = volume.SizeGb <= 0 ? 100 : volume.FreeGb / volume.SizeGb * 100;
            storageRows.Add(new InfoRow
            {
                Name = string.IsNullOrWhiteSpace(volume.Label) ? Display.Text(volume.Letter) : $"{volume.Letter} ({volume.Label})",
                Value = $"{Display.Number(volume.FreeGb, "GB", "0")} free of {Display.Number(volume.SizeGb, "GB", "0")}",
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
                new() { Name = "Power mode", Value = Display.Text(perf.PowerPlan),
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
                new() { Name = "Edition", Value = Display.Text(snapshot.Windows.Edition),
                        Plain = "Which version of Windows this machine runs." },
                new() { Name = "Version",
                        Value = string.IsNullOrWhiteSpace(snapshot.Windows.DisplayVersion)
                            ? Display.Text(snapshot.Windows.Build)
                            : $"{snapshot.Windows.DisplayVersion} (build {Display.Text(snapshot.Windows.Build)})",
                        Plain = "The feature update level and the build number behind it." },
                new() { Name = "Installed", Value = Display.Text(snapshot.Windows.InstallDate),
                        Plain = "When Windows was installed on this machine." },
                new() { Name = "Running for",
                        Value = snapshot.Windows.LastBootHours > 0 ? $"{snapshot.Windows.LastBootHours:0.#} hours" : Display.Unknown,
                        Plain = "A restart clears memory leaks and finishes pending updates." },
                new() { Name = "Long file paths", Value = snapshot.Windows.LongPaths ? "Allowed" : "Blocked",
                        Plain = "Deep project folders need this allowed.",
                        State = snapshot.Windows.LongPaths ? "good" : "warn" },
                new() { Name = "Virtual memory", Value = snapshot.Windows.PageFile.Automatic ? "Managed by Windows" : "Set by hand",
                        Plain = "Managed virtual memory grows the page file as programs need it.",
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
                        Name = Display.Text(item.Name),
                        Value = Display.Text(item.Command),
                        Plain = "This program starts every time you sign in."
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
                Value = Display.Text(version),
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
