using PcCleaner.Domain.Models;

namespace PcCleaner.Domain.Interfaces;

/// <summary>
/// A Windows setting that normally lives somewhere deep in Settings / Control
/// Panel / the command line, surfaced in System Tools as one card with a set of
/// mutually exclusive options (e.g. Hibernation: Full / Reduced / Off).
/// </summary>
public interface ISystemTweak
{
    string Id { get; }
    string Title { get; }

    /// <summary>Tab the card is shown on, e.g. "Power &amp; Storage".</summary>
    string Category { get; }

    /// <summary>One-paragraph plain-language explanation of what the setting is.</summary>
    string Summary { get; }

    /// <summary>Segoe MDL2 / Fluent icon glyph.</summary>
    string IconGlyph { get; }

    bool RequiresAdmin { get; }

    IReadOnlyList<TweakOption> Options { get; }

    Task<TweakState> GetStateAsync();

    Task<TweakApplyResult> ApplyAsync(string optionId);
}
