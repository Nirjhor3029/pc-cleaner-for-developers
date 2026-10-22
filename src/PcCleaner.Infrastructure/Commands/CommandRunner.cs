using System.Diagnostics;

namespace PcCleaner.Infrastructure.Commands;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>Runs external tools (docker, dism, cleanmgr). Abstracted so tests never spawn processes.</summary>
public interface ICommandRunner
{
    /// <exception cref="System.ComponentModel.Win32Exception">The executable was not found.</exception>
    CommandResult Run(string fileName, string arguments, TimeSpan? timeout = null);
}

public class ProcessCommandRunner : ICommandRunner
{
    public CommandResult Run(string fileName, string arguments, TimeSpan? timeout = null)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {fileName}");

        // Read both streams concurrently so a full pipe can't deadlock the child
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        var waitMs = timeout.HasValue ? (int)timeout.Value.TotalMilliseconds : Timeout.Infinite;
        if (!process.WaitForExit(waitMs))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already exited */ }
            return new CommandResult(-1, string.Empty, $"{fileName} timed out");
        }

        process.WaitForExit(); // flush redirected output
        return new CommandResult(process.ExitCode, stdout.Result, stderr.Result);
    }
}
