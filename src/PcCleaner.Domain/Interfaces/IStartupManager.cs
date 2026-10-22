using PcCleaner.Domain.Models;

namespace PcCleaner.Domain.Interfaces;

/// <summary>
/// Lists and enables/disables sign-in startup programs. Disabling never deletes
/// the entry; it uses the same "StartupApproved" flags as Task Manager, so the
/// change is fully reversible from either place.
/// </summary>
public interface IStartupManager
{
    Task<List<StartupEntry>> GetEntriesAsync();

    void SetEnabled(StartupEntry entry, bool enabled);
}
