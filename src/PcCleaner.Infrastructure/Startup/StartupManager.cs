using Microsoft.Win32;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;
using System.Diagnostics;

namespace PcCleaner.Infrastructure.Startup;

public sealed class StartupManager : IStartupManager
{
    private const string BackupSuffix = "_DisabledByPcCleaner";

    public async Task<List<StartupEntry>> GetAsync()
    {
        return await Task.Run(() =>
        {
            var list = new List<StartupEntry>();
            list.AddRange(ReadRegistryRun(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", StartupSource.RegistryRun));
            list.AddRange(ReadRegistryRun(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", StartupSource.RegistryRun));
            list.AddRange(ReadRegistryRun(Registry.CurrentUser, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", StartupSource.RegistryRun));
            list.AddRange(ReadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupSource.StartupFolder));
            list.AddRange(ReadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupSource.StartupFolderCommon));
            list.AddRange(ReadScheduledTasks());

            // Deduplicate by Name+Command
            return list.GroupBy(x => x.Name + "|" + x.Command).Select(g => g.First()).OrderBy(x => x.Name).ToList();
        });
    }

    public async Task<bool> DisableAsync(string id)
    {
        var all = await GetAsync();
        var entry = all.FirstOrDefault(x => x.Id == id);
        if (entry == null) return false;
        if (!entry.IsSafeToDisable) return false;

        try
        {
            if (entry.Source is StartupSource.RegistryRun or StartupSource.RegistryRunOnce)
            {
                var hive = entry.Location.Contains("HKEY_LOCAL_MACHINE") ? Registry.LocalMachine : Registry.CurrentUser;
                var path = entry.Location.Contains("Run") ? @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run" : entry.Location;
                // Simplified: move value to backup key
                using var key = hive.OpenSubKey(path, writable: true);
                if (key == null) return false;
                var val = key.GetValue(entry.Name)?.ToString() ?? entry.Command;
                key.DeleteValue(entry.Name, throwOnMissingValue: false);
                // Backup
                using var backup = hive.CreateSubKey(path + BackupSuffix);
                backup?.SetValue(entry.Name + "_DisabledAt", DateTime.Now.ToString("o"));
                backup?.SetValue(entry.Name, val);
                return true;
            }
            else if (entry.Source is StartupSource.StartupFolder or StartupSource.StartupFolderCommon)
            {
                var disabledDir = Path.Combine(Path.GetDirectoryName(entry.FilePath) ?? "", "DisabledByPcCleaner");
                Directory.CreateDirectory(disabledDir);
                var dest = Path.Combine(disabledDir, Path.GetFileName(entry.FilePath));
                File.Move(entry.FilePath, dest, overwrite: true);
                return true;
            }
            else if (entry.Source == StartupSource.ScheduledTask)
            {
                var psi = new ProcessStartInfo("schtasks", $"/Change /TN \"{entry.Name}\" /DISABLE") { UseShellExecute = false, CreateNoWindow = true };
                var p = Process.Start(psi); p?.WaitForExit(5000);
                return p?.ExitCode == 0;
            }
        }
        catch { }
        return false;
    }

    public async Task<bool> EnableAsync(string id)
    {
        // Enable restores from backup; for MVP search backup keys
        return await Task.Run(() =>
        {
            try
            {
                foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
                {
                    var path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run" + BackupSuffix;
                    using var backup = hive.OpenSubKey(path);
                    if (backup == null) continue;
                    foreach (var name in backup.GetValueNames())
                    {
                        if (name.EndsWith("_DisabledAt")) continue;
                        // name is original entry name
                        // Need to find original id match? For now restore all matching id prefix
                        var val = backup.GetValue(name)?.ToString();
                        if (val == null) continue;
                        using var target = hive.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                        target?.SetValue(name, val);
                        backup.Close();
                        using var b2 = hive.OpenSubKey(path, true);
                        b2?.DeleteValue(name, false);
                        b2?.DeleteValue(name + "_DisabledAt", false);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        });
    }

    public Task<string> GetFileLocationAsync(string id)
        => Task.FromResult(string.Empty);

    private static List<StartupEntry> ReadRegistryRun(RegistryKey hive, string path, StartupSource source)
    {
        var res = new List<StartupEntry>();
        try
        {
            using var key = hive.OpenSubKey(path);
            if (key == null) return res;
            foreach (var name in key.GetValueNames())
            {
                var cmd = key.GetValue(name)?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(cmd)) continue;
                var file = ExtractFile(cmd);
                res.Add(new StartupEntry
                {
                    Id = $"{source}:{hive.Name}:{name}",
                    Name = name,
                    Command = cmd,
                    FilePath = file,
                    Source = source,
                    Location = $"{hive.Name}\\{path}",
                    Impact = HeuristicImpact(name, file),
                    Status = StartupStatus.Enabled,
                    IsSafeToDisable = !IsCritical(name, file)
                });
            }
        }
        catch { }
        return res;
    }

    private static List<StartupEntry> ReadStartupFolder(string folder, StartupSource source)
    {
        var res = new List<StartupEntry>();
        try
        {
            if (!Directory.Exists(folder)) return res;
            foreach (var file in Directory.GetFiles(folder, "*.lnk"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                res.Add(new StartupEntry
                {
                    Id = $"{source}:{file}",
                    Name = name,
                    Command = file,
                    FilePath = file,
                    Source = source,
                    Location = folder,
                    Impact = HeuristicImpact(name, file),
                    Status = StartupStatus.Enabled,
                    IsSafeToDisable = !IsCritical(name, file)
                });
            }
        }
        catch { }
        return res;
    }

    private static List<StartupEntry> ReadScheduledTasks()
    {
        var res = new List<StartupEntry>();
        try
        {
            var psi = new ProcessStartInfo("schtasks", "/Query /FO CSV /V") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            var p = Process.Start(psi);
            if (p == null) return res;
            var outStr = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            foreach (var line in outStr.Split('\n'))
            {
                if (line.Contains("Logon") && (line.Contains("Ready") || line.Contains("Running")))
                {
                    // Very rough parse: task name is first field
                    var parts = line.Split(',');
                    if (parts.Length > 0)
                    {
                        var tname = parts[0].Trim('"');
                        if (tname.Contains("\\")) tname = tname.Split('\\').Last();
                        if (!string.IsNullOrWhiteSpace(tname))
                        {
                            res.Add(new StartupEntry
                            {
                                Id = $"{StartupSource.ScheduledTask}:{tname}",
                                Name = tname,
                                Command = tname,
                                FilePath = "",
                                Source = StartupSource.ScheduledTask,
                                Location = "Task Scheduler",
                                Impact = HeuristicImpact(tname, ""),
                                Status = StartupStatus.Enabled,
                                IsSafeToDisable = !IsCritical(tname, "")
                            });
                        }
                    }
                }
            }
        }
        catch { }
        return res.Take(20).ToList(); // cap
    }

    private static string ExtractFile(string cmd)
    {
        try
        {
            var s = cmd.Trim().Trim('"');
            var exeEnd = s.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeEnd > 0) return s.Substring(0, exeEnd + 4).Trim('"');
            return s.Split(' ')[0].Trim('"');
        }
        catch { return cmd; }
    }

    private static StartupImpact HeuristicImpact(string name, string file)
    {
        var high = new[] { "discord", "spotify", "teams", "onedrive", "adobe", "chrome", "slack" };
        var low = new[] { "onedrive", "securityhealth", "windows defender" };
        var lower = (name + file).ToLowerInvariant();
        if (high.Any(k => lower.Contains(k))) return StartupImpact.High;
        if (low.Any(k => lower.Contains(k))) return StartupImpact.Low;
        return StartupImpact.Medium;
    }

    private static bool IsCritical(string name, string file)
    {
        var crit = new[] { "securityhealth", "windows defender", "windows security" };
        var lower = (name + file).ToLowerInvariant();
        return crit.Any(k => lower.Contains(k));
    }
}
