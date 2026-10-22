namespace PcCleaner.Domain.Models;

public class ScanItem
{
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public bool IsSelected { get; set; } = true;
    public bool IsDeletable { get; set; } = true;
    public string? Reason { get; set; }

    /// <summary>
    /// When set, the item is cleaned by running this command (e.g. <c>docker builder prune</c>,
    /// DISM) instead of deleting <see cref="FilePath"/>; FilePath is then only a display label.
    /// </summary>
    public CleanupCommand? Command { get; set; }
}

/// <summary>An external tool invocation that reclaims space; success is exit code 0.</summary>
public sealed record CleanupCommand(string FileName, string Arguments);
