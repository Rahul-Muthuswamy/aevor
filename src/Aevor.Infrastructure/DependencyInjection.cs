using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Aevor.Application.Interfaces;
using Aevor.Application.Models;
using Aevor.Infrastructure.Services;

namespace Aevor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aevor",
            "Logs"
        );

        var logPath = Path.Combine(logDirectory, "log-.txt");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(dispose: true);
        });

        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IBraveInstallationService, BraveInstallationService>();
        services.AddSingleton<ILocalStateParser, LocalStateParser>();
        services.AddSingleton<IProfileDiscoveryService, ProfileDiscoveryService>();
        services.AddSingleton<IDiscoveredSettingRegistry, DiscoveredSettingRegistry>();
        services.AddTransient<IPreferencesParser, PreferencesParser>();
        services.AddTransient<ISecurePreferencesParser, SecurePreferencesParser>();
        services.AddTransient<IProfileAnalyzer, ProfileAnalyzer>();
        services.AddSingleton(new SecurityScannerOptions());
        services.AddSingleton<ExportSafetyEvaluator>();
        services.AddTransient<ISecurityScanner, SecurityScanner>();

        return services;
    }
}
