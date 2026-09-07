using System.Text.Json;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.History;

public sealed class JsonScanHistoryStore : IScanHistoryStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        @"PC-Cleaner\history.json");

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public async Task<List<ScanRecord>> LoadAsync()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<ScanRecord>();
            var json = await File.ReadAllTextAsync(FilePath);
            return JsonSerializer.Deserialize<List<ScanRecord>>(json, Opts) ?? new List<ScanRecord>();
        }
        catch { return new List<ScanRecord>(); }
    }

    public async Task AppendAsync(ScanRecord record)
    {
        var list = await LoadAsync();
        list.Add(record);
        // Keep last 50
        if (list.Count > 50) list = list.Skip(list.Count - 50).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(list, Opts));
    }

    public Task ClearAsync()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
        return Task.CompletedTask;
    }
}
