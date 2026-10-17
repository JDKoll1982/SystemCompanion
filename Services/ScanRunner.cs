using System.Diagnostics;

namespace SystemCompanion.Services;

/// <summary>The outcome of one scan, in words the window can show as they are.</summary>
public sealed record ScanResult(bool Succeeded, string Message)
{
    public static ScanResult Ok() => new(true, "Reading finished");

    public static ScanResult Failed(string message) => new(false, message);
}

/// <summary>
/// Runs the read-only scan script and waits for it. Never starts a scan by itself,
/// and never touches the displayed values.
/// </summary>
public sealed class ScanRunner
{
    public async Task<ScanResult> RunAsync()
    {
        var script = FindScript();
        if (script is null)
        {
            return ScanResult.Failed("The scan tool could not be found, so nothing was changed.");
        }

        var start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("-OutputPath");
        start.ArgumentList.Add(AppData.PathFor("current", "system-snapshot.json"));

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return ScanResult.Failed("The scan could not start, so the previous values are still shown.");
            }

            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            await output;
            await error;

            return process.ExitCode == 0
                ? ScanResult.Ok()
                : ScanResult.Failed("The scan did not finish, so the previous values are still shown.");
        }
        catch (Exception)
        {
            return ScanResult.Failed("The scan could not start, so the previous values are still shown.");
        }
    }

    private static string? FindScript()
    {
        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        while (probe is not null)
        {
            var candidate = Path.Combine(probe.FullName, "tools", "discover-system.ps1");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            probe = probe.Parent;
        }

        return null;
    }
}
