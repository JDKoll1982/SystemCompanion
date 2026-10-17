using System.Text.Json.Serialization;

namespace SystemCompanion.Models;

/// <summary>
/// Everything the app knows about the machine, read from Data/current/system-snapshot.json.
/// </summary>
public sealed class SystemSnapshot
{
    public string GeneratedLocal { get; set; } = "";
    public string ComputerName { get; set; } = "";
    public string UserName { get; set; } = "";

    public MachineIdentity Identity { get; set; } = new();
    public CpuInfo Cpu { get; set; } = new();
    public MemoryInfo Memory { get; set; } = new();
    public GraphicsBlock Graphics { get; set; } = new();
    public StorageInfo Storage { get; set; } = new();
    public WindowsInfo Windows { get; set; } = new();
    public PerformanceInfo Performance { get; set; } = new();
    public ToolingInfo Tooling { get; set; } = new();
    public AiToolsInfo AiTools { get; set; } = new();
    public List<string> DotnetSdks { get; set; } = new();
    public List<string> WslDistros { get; set; } = new();
}

public sealed class MachineIdentity
{
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public string Board { get; set; } = "";
    public string BoardVersion { get; set; } = "";
    public string BiosVendor { get; set; } = "";
    public string BiosVersion { get; set; } = "";
    public string BiosDate { get; set; } = "";
}

public sealed class CpuInfo
{
    public string Name { get; set; } = "";
    public int Cores { get; set; }
    public int Threads { get; set; }
    public int BaseMhz { get; set; }
    public int L3CacheMb { get; set; }
}

public sealed class MemoryInfo
{
    public double InstalledGb { get; set; }
    public double VisibleGb { get; set; }
    public int ModuleCount { get; set; }
    public int ConfiguredMt { get; set; }
    public int RatedMt { get; set; }
    public List<MemoryModule> Modules { get; set; } = new();
}

public sealed class MemoryModule
{
    public string Slot { get; set; } = "";
    public double SizeGb { get; set; }
    public int SpeedMt { get; set; }
    public string Part { get; set; } = "";
}

public sealed class GraphicsBlock
{
    public List<GraphicsInfo> Adapters { get; set; } = new();
    public List<NvidiaGpu> Nvidia { get; set; } = new();
}

public sealed class GraphicsInfo
{
    public string Name { get; set; } = "";
    public bool IsVirtual { get; set; }
    public double? VramGb { get; set; }
    public string DriverVersion { get; set; } = "";
    public string DriverDate { get; set; } = "";
    public int? CurrentWidth { get; set; }
    public int? CurrentHeight { get; set; }
    public int? RefreshHz { get; set; }
}

public sealed class NvidiaGpu
{
    public string Name { get; set; } = "";
    public string Driver { get; set; } = "";
    public int VramMb { get; set; }
    public int UsedMb { get; set; }
    public int Utilization { get; set; }
    public int Temperature { get; set; }
}

public sealed class AiToolsInfo
{
    public bool LmStudio { get; set; }
    public bool OllamaDataDir { get; set; }
}

public sealed class StorageInfo
{
    public List<PhysicalDrive> Drives { get; set; } = new();
    public List<VolumeInfo> Volumes { get; set; } = new();
}

public sealed class PhysicalDrive
{
    public string Model { get; set; } = "";
    public double SizeGb { get; set; }
    public string Interface { get; set; } = "";
    public string MediaType { get; set; } = "";
}

public sealed class VolumeInfo
{
    public string Letter { get; set; } = "";
    public string Label { get; set; } = "";
    public double SizeGb { get; set; }
    public double FreeGb { get; set; }
    public string FileSystem { get; set; } = "";
}

public sealed class WindowsInfo
{
    public string Edition { get; set; } = "";
    public string Version { get; set; } = "";
    public string Build { get; set; } = "";
    public string DisplayVersion { get; set; } = "";
    public string InstallDate { get; set; } = "";
    public double LastBootHours { get; set; }
    public string SystemDrive { get; set; } = "C:";
    public bool LongPaths { get; set; }
    public PageFileInfo PageFile { get; set; } = new();
    public List<StartupItem> StartupItems { get; set; } = new();
}

public sealed class PageFileInfo
{
    public bool Automatic { get; set; }
    public List<PageFileEntry> Files { get; set; } = new();
}

public sealed class PageFileEntry
{
    public string Name { get; set; } = "";
    public long AllocatedMb { get; set; }
}

public sealed class StartupItem
{
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
    public string Location { get; set; } = "";
}

public sealed class PerformanceInfo
{
    public string PowerPlan { get; set; } = "";
    public bool? GameMode { get; set; }
    public bool? GameDvrEnabled { get; set; }
    public bool? HardwareScheduling { get; set; }
    public int? HardwareSchedulingRaw { get; set; }
    public bool? MemoryIntegrity { get; set; }
    public bool? VbsConfigured { get; set; }
    public string HypervisorLaunch { get; set; } = "";
    public DefenderInfo Defender { get; set; } = new();
}

public sealed class DefenderInfo
{
    public bool? RealtimeDisabled { get; set; }
    public int ExclusionCount { get; set; }
    public List<string> Exclusions { get; set; } = new();
}

public sealed class ToolingInfo
{
    [JsonPropertyName("dotnet")] public string? Dotnet { get; set; }
    [JsonPropertyName("git")] public string? Git { get; set; }
    [JsonPropertyName("node")] public string? Node { get; set; }
    [JsonPropertyName("npm")] public string? Npm { get; set; }
    [JsonPropertyName("python")] public string? Python { get; set; }
    [JsonPropertyName("pip")] public string? Pip { get; set; }
    [JsonPropertyName("docker")] public string? Docker { get; set; }
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("ollama")] public string? Ollama { get; set; }
    [JsonPropertyName("nvidia")] public string? Nvidia { get; set; }
}
