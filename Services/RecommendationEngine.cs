using System.Globalization;
using System.Text.RegularExpressions;
using SystemCompanion.Models;

namespace SystemCompanion.Services;

/// <summary>
/// Turns the raw snapshot and the BIOS summary into advice a non-technical person
/// can act on. Every suggestion states what the machine does now, what to change,
/// why it helps, and how risky it is.
/// </summary>
public sealed class RecommendationEngine
{
    private readonly SystemSnapshot _system;
    private readonly BiosProfile _bios;

    public RecommendationEngine(SystemSnapshot system, BiosProfile bios)
    {
        _system = system;
        _bios = bios;
    }

    public IReadOnlyList<Suggestion> Build()
    {
        var all = new List<Suggestion>();
        all.AddRange(WindowsBasics());
        all.AddRange(Programming());
        all.AddRange(Gaming());
        all.AddRange(AiWork());
        all.AddRange(FromBios());
        return all;
    }

    // ------------------------------------------------------------------ state
    private string PowerPlan => _system.Performance.PowerPlan;

    private bool? GameMode => _system.Performance.GameMode;

    private bool? GameDvr => _system.Performance.GameDvrEnabled;

    private bool? Hags => _system.Performance.HardwareScheduling;

    private bool? MemoryIntegrity => _system.Performance.MemoryIntegrity;

    private bool LongPaths => _system.Windows.LongPaths;

    private int ExclusionCount => _system.Performance.Defender.ExclusionCount;

    private string BiosFact(string label)
        => _bios.Headline.FirstOrDefault(f => f.Label == label)?.Value
           ?? _bios.Sections.SelectMany(s => s.Items).FirstOrDefault(i => i.Name == label)?.Value
           ?? "";

    private static SuggestionState From(bool? condition, SuggestionState whenFalse = SuggestionState.WorthDoing)
        => condition == true ? SuggestionState.AlreadyGood : whenFalse;

    // ---------------------------------------------------------- everyday base
    private IEnumerable<Suggestion> WindowsBasics()
    {
        var planIsFast = PowerPlan.Contains("Ultimate", StringComparison.OrdinalIgnoreCase)
                         || PowerPlan.Contains("High performance", StringComparison.OrdinalIgnoreCase);

        yield return new Suggestion
        {
            Id = "power-plan",
            Title = "Keep Windows in its fastest power mode",
            Summary = "Windows can slow the processor down to save electricity. Fast mode stops that.",
            Why = "In a balanced or power-saving mode Windows parks processor cores and lowers their speed "
                  + "even while you work. The fast modes keep every core ready, which makes everything feel "
                  + "more responsive. On a desktop that is plugged in all the time there is nothing to lose.",
            HowTo = "Open Settings > System > Power (or run 'powercfg.cpl'). Choose 'Ultimate Performance'. "
                    + "If you do not see it, open a Command Prompt and run: "
                    + "powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61",
            Goal = Goal.Everyday,
            State = planIsFast ? SuggestionState.AlreadyGood : SuggestionState.WorthDoing,
            Current = PowerPlan,
            Target = "Ultimate Performance",
            Benefit = "Small but constant; helps every program a little.",
            Risk = "Safe"
        };

        yield return new Suggestion
        {
            Id = "long-paths",
            Title = "Let Windows accept long file paths",
            Summary = "Deeply nested folders can break builds and installers.",
            Why = "Older Windows refused paths longer than about 260 characters. Modern development tools "
                  + "create deeply nested folders, and hitting that limit causes confusing 'file not found' "
                  + "errors during builds and package installs.",
            HowTo = "Open Registry Editor, go to "
                    + "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\FileSystem and set "
                    + "LongPathsEnabled to 1. Restart afterwards. This app already reports the current value.",
            Goal = Goal.Programming,
            State = LongPaths ? SuggestionState.AlreadyGood : SuggestionState.WorthDoing,
            Current = LongPaths ? "Allowed" : "Blocked",
            Target = "Allowed",
            Benefit = "Removes a whole class of build errors.",
            Risk = "Changes behaviour",
            NeedsRestart = true
        };

        var startupCount = _system.Windows.StartupItems.Count;
        yield return new Suggestion
        {
            Id = "startup-apps",
            Title = "Trim what starts with Windows",
            Summary = $"You have {startupCount} programs launching at sign-in.",
            Why = "Every program that starts with Windows keeps using memory and disk time. Trimming the "
                  + "list you do not need makes sign-in faster and leaves more resources for real work.",
            HowTo = "Open Task Manager > Startup apps. Right-click anything you do not need at sign-in and "
                    + "choose Disable. Keep audio, keyboard and mouse utilities.",
            Goal = Goal.Everyday,
            State = startupCount > 6 ? SuggestionState.Optional : SuggestionState.AlreadyGood,
            Current = $"{startupCount} startup items",
            Target = "Only what you need",
            Benefit = "Faster sign-in, less background noise.",
            Risk = "Safe"
        };
    }

    // ------------------------------------------------------------ programming
    private IEnumerable<Suggestion> Programming()
    {
        var freePercent = FreeSpacePercent(_system.Windows.SystemDrive);
        var sdkCount = _system.DotnetSdks.Count;

        yield return new Suggestion
        {
            Id = "defender-code-exclusions",
            Title = "Stop the antivirus from re-scanning your code",
            Summary = "Real-time scanning runs on every file a compiler reads or writes.",
            Why = "Windows Security opens and inspects every file that is created or changed. A build writes "
                  + "thousands of temporary files, so the scanner multiplies build time. Excluding only your "
                  + "source folders keeps the protection everywhere else and makes builds noticeably faster.",
            HowTo = "Windows Security > Virus & threat protection > Manage settings > Exclusions > Add exclusion. "
                    + "Add your repositories folder (for example C:\\Users\\<you>\\source\\repos) and any folder "
                    + "holding large package caches. Never exclude all of C:.",
            Goal = Goal.Programming,
            State = ExclusionCount >= 3 ? SuggestionState.AlreadyGood : SuggestionState.WorthDoing,
            Current = ExclusionCount == 0 ? "No exclusions" : $"{ExclusionCount} exclusion(s)",
            Target = "Repos + package caches excluded",
            Benefit = "Often 20-50% faster builds on large solutions.",
            Risk = "Changes behaviour"
        };

        yield return new Suggestion
        {
            Id = "system-drive-space",
            Title = "Keep plenty of free space on the system drive",
            Summary = $"Free space on {_system.Windows.SystemDrive} is {freePercent:0}%.",
            Why = "Solid-state drives slow down and wear faster when they are nearly full, and Windows needs "
                  + "room for updates, temporary files and virtual memory. Below roughly one fifth free the "
                  + "whole machine feels heavier.",
            HowTo = "Settings > System > Storage > Temporary files, and move big media or old builds to another "
                    + "drive. Keep at least 15% free on the drive that holds Windows.",
            Goal = Goal.Programming,
            State = freePercent < 20 ? SuggestionState.WorthDoing : SuggestionState.AlreadyGood,
            Current = $"{freePercent:0}% free",
            Target = "At least 20% free",
            Benefit = "Keeps the drive fast and avoids update failures.",
            Risk = "Safe"
        };

        yield return new Suggestion
        {
            Id = "single-dotnet-sdk",
            Title = "Pin projects to one .NET SDK version",
            Summary = sdkCount > 1
                ? $"{sdkCount} .NET SDKs are installed, so the newest wins by default."
                : "Only one .NET SDK is installed.",
            Why = "When several SDKs are installed, dotnet uses the newest one unless a project says "
                  + "otherwise. A global.json file in the folder above your solution pins the exact version, "
                  + "so a tooling update cannot silently change how a project builds.",
            HowTo = "In the folder above your solution run: dotnet new globaljson --sdk-version <version>. "
                    + "Pick a version from the list this app shows on the Windows page.",
            Goal = Goal.Programming,
            State = sdkCount > 1 ? SuggestionState.Optional : SuggestionState.AlreadyGood,
            Current = sdkCount > 1 ? $"{sdkCount} SDKs" : "1 SDK",
            Target = "Pinned with global.json",
            Benefit = "Reproducible builds; no surprise breakage.",
            Risk = "Safe"
        };

        yield return new Suggestion
        {
            Id = "wsl-for-linux-tooling",
            Title = "Add a Linux workspace only if you need one",
            Summary = _system.WslDistros.Count == 0
                ? "No Linux environment is installed."
                : $"{_system.WslDistros.Count} Linux environment(s) installed.",
            Why = "Many AI and web tools assume a Linux shell. Windows Subsystem for Linux gives you one with "
                  + "very little overhead, and files stay on the same disk. If you never use Linux tools you "
                  + "can ignore this completely.",
            HowTo = "Open a Command Prompt as administrator and run: wsl --install. Restart when asked. "
                    + "Keep your projects inside the Linux file system for the best speed.",
            Goal = Goal.Programming,
            State = _system.WslDistros.Count == 0 ? SuggestionState.Optional : SuggestionState.AlreadyGood,
            Current = _system.WslDistros.Count == 0 ? "None" : string.Join(", ", _system.WslDistros),
            Target = "Optional",
            Benefit = "Unlocks Linux-only tooling when you need it.",
            Risk = "Changes behaviour",
            NeedsRestart = true
        };

        yield return new Suggestion
        {
            Id = "page-file",
            Title = "Let Windows manage virtual memory",
            Summary = _system.Windows.PageFile.Automatic
                ? "Windows is managing the page file automatically."
                : "The page file has been set by hand.",
            Why = "The page file is disk space Windows uses when memory runs out. With 64 GB of memory you "
                  + "rarely touch it, but letting Windows size it avoids rare out-of-memory failures in big "
                  + "compiles and AI models.",
            HowTo = "System Properties > Advanced > Performance Settings > Advanced > Change. Tick "
                    + "'Automatically manage paging file size for all drives'.",
            Goal = Goal.Programming,
            State = _system.Windows.PageFile.Automatic ? SuggestionState.AlreadyGood : SuggestionState.WorthDoing,
            Current = _system.Windows.PageFile.Automatic ? "Automatic" : "Manual",
            Target = "Automatic",
            Benefit = "Avoids rare memory failures; nothing to tune.",
            Risk = "Safe",
            NeedsRestart = true
        };
    }

    // ----------------------------------------------------------------- gaming
    private IEnumerable<Suggestion> Gaming()
    {
        var primary = _system.Graphics.Adapters.FirstOrDefault(a => !a.IsVirtual);
        var refresh = primary?.RefreshHz ?? 0;
        var freeGameDrive = _system.Storage.Volumes
            .OrderByDescending(v => v.SizeGb)
            .FirstOrDefault();

        yield return new Suggestion
        {
            Id = "game-mode",
            Title = "Turn on Windows Game Mode",
            Summary = "Game Mode stops background work while you play.",
            Why = "When Game Mode is on, Windows holds back updates, driver installs and other housekeeping "
                  + "while a game has focus. It costs nothing and prevents stutter caused by background jobs.",
            HowTo = "Settings > Gaming > Game Mode > On.",
            Goal = Goal.Gaming,
            State = From(GameMode),
            Current = Describe(GameMode),
            Target = "On",
            Benefit = "Fewer stutters from background tasks.",
            Risk = "Safe"
        };

        yield return new Suggestion
        {
            Id = "gpu-scheduling",
            Title = "Let the graphics card manage its own memory",
            Summary = "Hardware-accelerated GPU scheduling hands memory control to the card.",
            Why = "Instead of Windows deciding when work is handed to the GPU, the card schedules it directly. "
                  + "This lowers latency slightly in modern games and is required for some newer features.",
            HowTo = "Settings > System > Display > Graphics > Change default graphics settings > "
                    + "Hardware-accelerated GPU scheduling > On. Restart.",
            Goal = Goal.Gaming,
            State = From(Hags),
            Current = Describe(Hags),
            Target = "On",
            Benefit = "Slightly smoother frame pacing.",
            Risk = "Changes behaviour",
            NeedsRestart = true
        };

        yield return new Suggestion
        {
            Id = "game-dvr",
            Title = "Turn off background game recording",
            Summary = "Game DVR records clips in the background even when you never use it.",
            Why = "The built-in recorder keeps a rolling buffer of gameplay, which uses GPU time and disk "
                  + "writes continuously. Turning it off gives that performance back. You can still record "
                  + "deliberately with the Xbox Game Bar.",
            HowTo = "Settings > Gaming > Captures > turn off 'Record what happened' and background recording.",
            Goal = Goal.Gaming,
            State = GameDvr == false ? SuggestionState.AlreadyGood : SuggestionState.WorthDoing,
            Current = GameDvr == false ? "Off" : "On",
            Target = "Off",
            Benefit = "A few percent more GPU headroom.",
            Risk = "Safe"
        };

        yield return new Suggestion
        {
            Id = "memory-integrity",
            Title = "Decide whether memory integrity is worth its cost",
            Summary = "This security layer protects the kernel and costs a few percent of speed.",
            Why = "Memory integrity (also called HVCI) keeps the operating system's core safe from malicious "
                  + "drivers. The trade-off is real: it adds work to every memory operation, which shows up "
                  + "most in games, virtual machines and AI inference. Keep it on if you install software from "
                  + "untrusted sources; turn it off if you want the last few percent and trust your software.",
            HowTo = "Windows Security > Device security > Core isolation details > Memory integrity. "
                    + "Turning it off requires a restart.",
            Goal = Goal.Gaming,
            AlsoHelps = new[] { Goal.Ai },
            State = MemoryIntegrity == true ? SuggestionState.WorthDoing : SuggestionState.AlreadyGood,
            Current = MemoryIntegrity == true ? "On (security first)" : "Off (performance first)",
            Target = "Your choice",
            Benefit = "Commonly 3-10% in games; also helps AI inference.",
            Risk = "Changes behaviour",
            NeedsRestart = true
        };

        yield return new Suggestion
        {
            Id = "refresh-rate",
            Title = "Make sure the monitor runs at its fastest refresh rate",
            Summary = refresh > 0 ? $"Windows is running the display at {refresh} Hz." : "Refresh rate could not be read.",
            Why = "A fast monitor only feels fast when Windows is set to its highest refresh rate. After a "
                  + "driver update Windows sometimes falls back to 60 Hz without telling you.",
            HowTo = "Settings > System > Display > Advanced display. Set 'Choose a refresh rate' to the highest "
                    + "value offered.",
            Goal = Goal.Gaming,
            State = refresh >= 120 ? SuggestionState.AlreadyGood : SuggestionState.WorthDoing,
            Current = refresh > 0 ? $"{refresh} Hz" : "Unknown",
            Target = "Highest available",
            Benefit = "Directly improves how smooth motion looks.",
            Risk = "Safe"
        };

        yield return new Suggestion
        {
            Id = "vrr-check",
            Title = "Check that variable refresh is switched on",
            Summary = "Variable refresh (G-Sync or FreeSync) removes tearing without capping frame rate.",
            Why = "When the monitor and graphics card agree on a variable refresh window, frames are shown as "
                  + "soon as they are ready. The app cannot read this setting, so it has to be checked by hand.",
            HowTo = "NVIDIA Control Panel > Display > Set up G-SYNC > enable for full screen. Then in the "
                    + "monitor's own on-screen menu make sure the adaptive-sync option is on.",
            Goal = Goal.Gaming,
            State = SuggestionState.CheckYourself,
            Current = "Not readable from Windows",
            Target = "Enabled",
            Benefit = "No tearing and less stutter.",
            Risk = "Safe"
        };

        if (freeGameDrive is not null)
        {
            var gameFree = freeGameDrive.SizeGb <= 0 ? 100 : freeGameDrive.FreeGb / freeGameDrive.SizeGb * 100;
            yield return new Suggestion
            {
                Id = "game-drive-space",
                Title = "Leave room on the game drive",
                Summary = $"{freeGameDrive.Letter} ({freeGameDrive.Label}) has {gameFree:0}% free.",
                Why = "Games stream data from disk while you play. On a nearly full drive the free space runs "
                      + "out of fast cells and level loading slows down.",
                HowTo = "Uninstall games you no longer play, or move them to a drive with more room.",
                Goal = Goal.Gaming,
                State = gameFree < 20 ? SuggestionState.WorthDoing : SuggestionState.AlreadyGood,
                Current = $"{gameFree:0}% free",
                Target = "At least 20% free",
                Benefit = "Faster level loads, fewer hitches.",
                Risk = "Safe"
            };
        }
    }

    // ---------------------------------------------------------------- AI work
    private IEnumerable<Suggestion> AiWork()
    {
        var primary = _system.Graphics.Adapters.FirstOrDefault(a => !a.IsVirtual);
        var nvidia = _system.Graphics.Nvidia.FirstOrDefault();
        var vram = nvidia is not null ? nvidia.VramMb / 1024.0 : primary?.VramGb ?? 0;
        var driverAge = DriverAgeDays(primary?.DriverDate);
        var hasLmStudio = _system.AiTools.LmStudio;
        var hasOllamaDir = _system.AiTools.OllamaDataDir;
        var ollamaOnPath = !string.IsNullOrWhiteSpace(_system.Tooling.Ollama);

        yield return new Suggestion
        {
            Id = "gpu-vram",
            Title = "Know how large a model your graphics card can hold",
            Summary = vram > 0 ? $"{vram:0.#} GB of graphics memory is available." : "Graphics memory could not be read.",
            Why = "Local AI models must fit entirely in graphics memory to run quickly. A rough rule: "
                  + "a 7-8 billion parameter model at 4-bit quality needs about 6 GB, a 13-14 billion model "
                  + "about 10 GB, and a 30 billion model about 20 GB. Anything larger spills into slow system "
                  + "memory.",
            HowTo = "Nothing to change. Use this number when choosing a model size in your AI app.",
            Goal = Goal.Ai,
            State = SuggestionState.AlreadyGood,
            Current = vram > 0 ? $"{vram:0.#} GB VRAM" : "Unknown",
            Target = "Fit the model inside VRAM",
            Benefit = "Prevents the classic 'it works but is 20x slower' surprise.",
            Risk = "Safe"
        };

        yield return new Suggestion
        {
            Id = "ai-model-exclusions",
            Title = "Exclude model folders from virus scanning",
            Summary = hasLmStudio || hasOllamaDir
                ? "Local AI model files are tens of gigabytes and get scanned on every load."
                : "No local AI model folder was found.",
            Why = "Model files are large, read-only blobs. Antivirus real-time scanning re-reads them each time "
                  + "a model loads, which delays start-up and steals disk bandwidth from the model itself.",
            HowTo = "Windows Security > Virus & threat protection > Manage settings > Exclusions. Add the "
                    + "folders your AI app uses for models (for example %USERPROFILE%\\.ollama\\models and the "
                    + "LM Studio models folder).",
            Goal = Goal.Ai,
            State = (hasLmStudio || hasOllamaDir) && ExclusionCount < 3
                ? SuggestionState.WorthDoing
                : SuggestionState.AlreadyGood,
            Current = hasLmStudio && hasOllamaDir ? "LM Studio + Ollama present"
                     : hasLmStudio ? "LM Studio present"
                     : hasOllamaDir ? "Ollama data present" : "No AI app detected",
            Target = "Model folders excluded",
            Benefit = "Faster model load, less disk contention.",
            Risk = "Changes behaviour"
        };

        yield return new Suggestion
        {
            Id = "ollama-on-path",
            Title = "Put the Ollama command on your PATH",
            Summary = ollamaOnPath
                ? "The ollama command is available in terminals."
                : hasOllamaDir ? "Ollama data exists but the command is not on PATH." : "Ollama not detected.",
            Why = "Command-line AI tools are launched by name from a terminal. If the folder holding the "
                  + "program is not on PATH, every script that calls it fails even though the app is installed.",
            HowTo = "Settings > System > About > Advanced system settings > Environment Variables. Edit the "
                    + "user 'Path' entry and add the folder that contains ollama.exe, then open a new terminal.",
            Goal = Goal.Ai,
            State = ollamaOnPath ? SuggestionState.AlreadyGood
                    : hasOllamaDir ? SuggestionState.WorthDoing : SuggestionState.Optional,
            Current = ollamaOnPath ? _system.Tooling.Ollama! : "Not on PATH",
            Target = "On PATH",
            Benefit = "Scripts and terminals can start it directly.",
            Risk = "Safe"
        };

        yield return new Suggestion
        {
            Id = "gpu-driver-fresh",
            Title = "Keep the graphics driver recent",
            Summary = driverAge is null ? "Driver date could not be read."
                : $"Installed {driverAge} days ago.",
            Why = "AI runtimes depend on the driver's compute support. Newer drivers add support for new "
                  + "model formats and often improve speed for existing ones. Very old drivers can also make "
                  + "a modern runtime refuse to start.",
            HowTo = "Download the current driver from the card maker's site and choose a clean install. "
                    + "Re-check this page afterwards.",
            Goal = Goal.Ai,
            State = driverAge is null ? SuggestionState.CheckYourself
                    : driverAge <= 120 ? SuggestionState.AlreadyGood : SuggestionState.WorthDoing,
            Current = driverAge is null ? "Unknown" : $"{driverAge} days old",
            Target = "Less than about 4 months",
            Benefit = "Best compatibility with new AI runtimes.",
            Risk = "Changes behaviour",
            NeedsRestart = true
        };

        yield return new Suggestion
        {
            Id = "ai-app-detected",
            Title = "Use the AI apps already installed",
            Summary = hasLmStudio || hasOllamaDir
                ? $"Detected: {(hasLmStudio ? "LM Studio" : "")}{(hasLmStudio && hasOllamaDir ? ", " : "")}{(hasOllamaDir ? "Ollama" : "")}."
                : "No local AI app detected.",
            Why = "Both LM Studio and Ollama can use the graphics card directly. LM Studio is easier for "
                  + "experiments; Ollama is easier to script and share with other tools.",
            HowTo = "Nothing required. Open the app and check that it reports GPU acceleration rather than "
                    + "processor-only mode.",
            Goal = Goal.Ai,
            State = hasLmStudio || hasOllamaDir ? SuggestionState.AlreadyGood : SuggestionState.Optional,
            Current = hasLmStudio && hasOllamaDir ? "LM Studio and Ollama"
                     : hasLmStudio ? "LM Studio"
                     : hasOllamaDir ? "Ollama" : "None",
            Target = "GPU acceleration confirmed",
            Benefit = "Confirms you are not running on the processor by mistake.",
            Risk = "Safe"
        };
    }

    // -------------------------------------------------------------- from BIOS
    private IEnumerable<Suggestion> FromBios()
    {
        var xmp = BiosFact("Fast memory profile");
        var speed = BiosFact("Memory speed");
        var resizableBar = BestValue("Re-Size BAR Support");
        var above4g = BestValue("Above 4G memory/Crypto Currency mining");

        yield return new Suggestion
        {
            Id = "bios-xmp",
            Title = "Fast memory profile in the BIOS",
            Summary = xmp.Length > 0 ? $"Profile reports: {xmp}." : "Memory profile could not be read.",
            Why = "Memory ships able to run faster than the safe default it starts at. The fast profile is "
                  + "the memory maker's own tested configuration, so it is the free performance win that "
                  + "carries the least risk.",
            HowTo = "Restart, press Delete to enter the BIOS, and look under Overclocking for "
                    + "'Extreme Memory Profile (XMP)'. Choose Profile 1, then save and exit.",
            Goal = Goal.Everyday,
            State = xmp.Contains("On", StringComparison.OrdinalIgnoreCase)
                ? SuggestionState.AlreadyGood : SuggestionState.WorthDoing,
            Current = xmp.Length > 0 ? xmp : "Unknown",
            Target = "On (Profile 1)",
            Benefit = "Large, free memory bandwidth increase.",
            Risk = "Safe",
            NeedsRestart = true
        };

        yield return new Suggestion
        {
            Id = "bios-resizable-bar",
            Title = "Let the graphics card see all of its memory at once",
            Summary = resizableBar.Length > 0 ? $"BIOS reports: {resizableBar}." : "Not found in the BIOS summary.",
            Why = "Resizable BAR lets the processor address the whole graphics memory in one window instead "
                  + "of small slices. Modern games and AI runtimes both benefit. It needs two BIOS switches: "
                  + "Re-Size BAR Support and Above 4G decoding.",
            HowTo = "In the BIOS look under Settings > Advanced > PCIe/PCI Sub-system Settings. Set "
                    + "'Re-Size BAR Support' to Enabled and 'Above 4G memory' to Enabled, then save and exit.",
            Goal = Goal.Gaming,
            State = resizableBar.Equals("Enabled", StringComparison.OrdinalIgnoreCase)
                ? SuggestionState.AlreadyGood : SuggestionState.WorthDoing,
            Current = resizableBar.Length > 0 ? resizableBar : "Unknown",
            Target = "Enabled",
            Benefit = "A few percent in games; helps large models too.",
            Risk = "Changes behaviour",
            NeedsRestart = true
        };

        yield return new Suggestion
        {
            Id = "bios-4g",
            Title = "Above 4G decoding",
            Summary = above4g.Length > 0 ? $"BIOS reports: {above4g}." : "Not found in the BIOS summary.",
            Why = "This lets the system hand out memory addresses above the 4 GB line, which is what makes "
                  + "Resizable BAR work and is required for some multi-GPU setups.",
            HowTo = "Settings > Advanced > PCIe/PCI Sub-system Settings > Above 4G memory/Crypto Currency "
                    + "mining > Enabled. Save and exit.",
            Goal = Goal.Gaming,
            State = above4g.Equals("Enabled", StringComparison.OrdinalIgnoreCase)
                ? SuggestionState.AlreadyGood : SuggestionState.WorthDoing,
            Current = above4g.Length > 0 ? above4g : "Unknown",
            Target = "Enabled",
            Benefit = "Required companion for Resizable BAR.",
            Risk = "Changes behaviour",
            NeedsRestart = true
        };

        yield return new Suggestion
        {
            Id = "bios-cpu-leave-alone",
            Title = "Leave the processor multipliers alone",
            Summary = "The BIOS has every processor multiplier on Auto.",
            Why = "Modern Intel processors boost themselves far better than a fixed multiplier can, because "
                  + "they balance heat and power hundreds of times a second. Raising the multiplier manually "
                  + "usually makes a machine hotter and less stable for very little gain.",
            HowTo = "Nothing to do. If you ever want more processor performance, lower the voltage slightly "
                    + "(a 'CPU Lite Load' step down) instead of raising multipliers.",
            Goal = Goal.Gaming,
            State = SuggestionState.AlreadyGood,
            Current = "Auto",
            Target = "Auto",
            Benefit = "Avoids instability and excess heat.",
            Risk = "Advanced"
        };

        yield return new Suggestion
        {
            Id = "bios-cooler-profile",
            Title = "Match the BIOS cooler profile to your real cooler",
            Summary = CoolerSummary(),
            Why = "The cooler profile tells the board how much power the processor may draw. Choosing a "
                  + "profile larger than your cooler can handle causes thermal throttling, which is slower "
                  + "than a lower, steady limit.",
            HowTo = "In the BIOS under Overclocking, open 'CPU Cooler Tuning' and pick the entry that matches "
                    + "your cooler. Save and exit.",
            Goal = Goal.Gaming,
            State = SuggestionState.CheckYourself,
            Current = CoolerSummary(),
            Target = "Matches the installed cooler",
            Benefit = "Sustained speed without throttling.",
            Risk = "Changes behaviour",
            NeedsRestart = true
        };
    }

    // ---------------------------------------------------------------- helpers
    private string CoolerSummary()
    {
        var value = _bios.Sections
            .SelectMany(s => s.Items)
            .FirstOrDefault(i => i.Name == "CPU cooler profile")?.Value;
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
    }

    private string BestValue(string fieldName)
    {
        var screenshotCheck = AppData.ReadJson<System.Text.Json.JsonElement>(
            "bios", "screenshot_check.json");
        if (screenshotCheck.ValueKind == System.Text.Json.JsonValueKind.Object
            && screenshotCheck.TryGetProperty("rows", out var rows))
        {
            foreach (var row in rows.EnumerateArray())
            {
                if (row.TryGetProperty("name", out var name)
                    && string.Equals(name.GetString(), fieldName, StringComparison.OrdinalIgnoreCase)
                    && row.TryGetProperty("label", out var labelTag)
                    && labelTag.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return labelTag.GetString() ?? "";
                }
            }
        }

        return "";
    }

    private double FreeSpacePercent(string driveLetter)
    {
        var volume = _system.Storage.Volumes
            .FirstOrDefault(v => string.Equals(v.Letter, driveLetter, StringComparison.OrdinalIgnoreCase));
        if (volume is null || volume.SizeGb <= 0)
        {
            return 100;
        }

        return volume.FreeGb / volume.SizeGb * 100;
    }

    private static int? DriverAgeDays(string? driverDate)
    {
        if (string.IsNullOrWhiteSpace(driverDate)
            || !DateTime.TryParse(driverDate, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            return null;
        }

        return (int)(DateTime.Now - date).TotalDays;
    }

    private static string Describe(bool? value) => value switch
    {
        true => "On",
        false => "Off",
        _ => "Unknown"
    };
}
