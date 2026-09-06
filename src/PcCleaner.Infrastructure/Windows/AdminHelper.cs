using System.Diagnostics;
using System.Security.Principal;

namespace PcCleaner.Infrastructure.Windows;

public static class AdminHelper
{
    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool Elevate(string? arguments = null)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty,
                Verb = "runas",
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true
            };

            Process.Start(processInfo);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User clicked "No" on UAC prompt
            return false;
        }
    }

    public static ProcessStartInfo GetElevatedStartInfo(string fileName, string arguments = "")
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            Verb = "runas",
            Arguments = arguments,
            UseShellExecute = true
        };
    }
}