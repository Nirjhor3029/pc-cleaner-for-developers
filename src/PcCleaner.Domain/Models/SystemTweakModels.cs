namespace PcCleaner.Domain.Models;

/// <param name="Id">Stable identifier passed back to <c>ApplyAsync</c>.</param>
/// <param name="Title">Short option name, e.g. "Reduced".</param>
/// <param name="Description">What happens, what you gain and what you lose.</param>
/// <param name="Badge">Optional highlight such as "Recommended for desktops".</param>
public sealed record TweakOption(string Id, string Title, string Description, string? Badge = null);

/// <param name="CurrentOptionId">Option that matches the current system state, or null if none does.</param>
/// <param name="StatusText">Short state shown as a pill, e.g. "Full · 6.4 GB".</param>
/// <param name="Details">Extra facts (sizes, related settings) shown under the summary.</param>
/// <param name="OptionHints">Per-option extra text such as estimated size, keyed by option id.</param>
public sealed record TweakState(
    string? CurrentOptionId,
    string StatusText,
    string Details,
    IReadOnlyDictionary<string, string>? OptionHints = null);

public sealed record TweakApplyResult(bool Success, string Message);

/// <summary>Where a startup entry is registered.</summary>
public enum StartupLocation
{
    UserRegistry,
    MachineRegistry,
    MachineRegistry32,
    UserStartupFolder,
    CommonStartupFolder
}
