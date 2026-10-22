using PcCleaner.Infrastructure.Commands;

namespace PcCleaner.Infrastructure.SystemTools;

/// <summary>
/// Creates a System Restore point before changing system settings. Needs admin
/// and System Protection enabled on C:; Windows also allows only one new point
/// per 24 hours by default, in which case the call succeeds without a new point.
/// </summary>
public class RestorePointCreator
{
    private readonly ICommandRunner _runner;

    public RestorePointCreator(ICommandRunner runner)
    {
        _runner = runner;
    }

    public Task<(bool Success, string Message)> CreateAsync(string description = "PC Cleaner - before system changes")
    {
        return Task.Run(() =>
        {
            var safe = description.Replace("'", "''");
            var result = _runner.Run("powershell.exe",
                $"-NoProfile -NonInteractive -Command \"Checkpoint-Computer -Description '{safe}' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop\"",
                TimeSpan.FromMinutes(5));

            if (result.Succeeded)
                return (true, "Restore point created (or one already exists from the last 24 hours).");

            var error = result.StandardError.Trim();
            if (error.Contains("disabled", StringComparison.OrdinalIgnoreCase) || error.Contains("0x80042306"))
                return (false, "System Protection is turned off for C:. Turn it on in System Properties > System Protection.");
            return (false, string.IsNullOrEmpty(error) ? $"Failed (exit code {result.ExitCode})." : error);
        });
    }
}
