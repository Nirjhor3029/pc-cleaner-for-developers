using PcCleaner.Domain.Models;

namespace PcCleaner.Domain.Interfaces;

public interface IStartupManager
{
    Task<List<StartupEntry>> GetAsync();
    Task<bool> DisableAsync(string id);
    Task<bool> EnableAsync(string id);
    Task<string> GetFileLocationAsync(string id);
}
