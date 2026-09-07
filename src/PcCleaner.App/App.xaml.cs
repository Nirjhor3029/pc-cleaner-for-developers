using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PcCleaner.Application.Interfaces;
using PcCleaner.Application.Services;
using PcCleaner.Domain.Interfaces;
using PcCleaner.App.ViewModels;
using PcCleaner.Infrastructure.Browser;
using PcCleaner.Infrastructure.Cleaning;
using PcCleaner.Infrastructure.DiskAnalyzer;
using PcCleaner.Infrastructure.Health;
using PcCleaner.Infrastructure.History;
using PcCleaner.Infrastructure.Monitoring;
using PcCleaner.Infrastructure.Scanners;
using PcCleaner.Infrastructure.Scanners.Apps;
using PcCleaner.Infrastructure.Scanners.Developer;
using PcCleaner.Infrastructure.Startup;
using PcCleaner.Infrastructure.SystemInfo;
using Serilog;

namespace PcCleaner.App;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigureLogging();
        AttachGlobalExceptionHandlers();

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        Log.Information("PC Cleaner started — Performance & Health Manager");

        var shell = Services.GetRequiredService<ShellWindow>();
        shell.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("PC Cleaner closing. Exit code: {Code}", e.ApplicationExitCode);
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void AttachGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI exception");
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}",
                "PC Cleaner - Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled AppDomain exception");
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
    }

    private static void ConfigureLogging()
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"PC-Cleaner\logs\cleaner-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ICleaner, CleaningEngine>();
        services.AddSingleton<IScanService, ScanService>();
        services.AddSingleton<IDiskAnalyzer, FolderSizeScanner>();

        services.AddSingleton<IScanner, UserTempScanner>();
        services.AddSingleton<IScanner, WindowsTempScanner>();
        services.AddSingleton<IScanner, RecycleBinScanner>();
        services.AddSingleton<IScanner, ChromeCacheScanner>();
        services.AddSingleton<IScanner, EdgeCacheScanner>();
        services.AddSingleton<IScanner, ThumbnailCacheScanner>();
        services.AddSingleton<IScanner, CrashDumpScanner>();
        services.AddSingleton<IScanner, WindowsUpdateScanner>();
        services.AddSingleton<IScanner, DeliveryOptimizationScanner>();

        services.AddSingleton<IScanner, NpmCacheScanner>();
        services.AddSingleton<IScanner, YarnCacheScanner>();
        services.AddSingleton<IScanner, ComposerCacheScanner>();
        services.AddSingleton<IScanner, VSCodeCacheScanner>();
        services.AddSingleton<IScanner, PlaywrightCacheScanner>();
        services.AddSingleton<IScanner, GradleCacheScanner>();
        services.AddSingleton<IScanner, AndroidStudioCacheScanner>();
        services.AddSingleton<IScanner, NuGetCacheScanner>();
        services.AddSingleton<IScanner, HuggingFaceCacheScanner>();

        services.AddSingleton<IScanner, TeamsCacheScanner>();
        services.AddSingleton<IScanner, SlackCacheScanner>();
        services.AddSingleton<IScanner, DiscordCacheScanner>();
        services.AddSingleton<IScanner, PnpmCacheScanner>();
        services.AddSingleton<IScanner, BunCacheScanner>();
        services.AddSingleton<IScanner, MavenCacheScanner>();
        services.AddSingleton<IScanner, PipCacheScanner>();
        services.AddSingleton<IScanner, FlutterPubCacheScanner>();

        // New optimizer services
        services.AddSingleton<IStartupManager, StartupManager>();
        services.AddSingleton<IResourceMonitor, ResourceMonitor>();
        services.AddSingleton<ISystemScanner, SystemScanner>();
        services.AddSingleton<IHealthScoreService, HealthScoreService>();
        services.AddSingleton<IScanHistoryStore, JsonScanHistoryStore>();

        // ViewModels
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<StartupViewModel>();
        services.AddSingleton<ResourceViewModel>();

        services.AddSingleton<MainWindow>();
        services.AddSingleton<ShellWindow>();
    }
}