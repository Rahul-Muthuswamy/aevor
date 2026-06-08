using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Aevor.Application.Interfaces;
using Aevor.Infrastructure;

namespace Aevor.UI;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddInfrastructureServices();
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Aevor Application started.");

        try
        {
            var discoveryService = _serviceProvider.GetRequiredService<IProfileDiscoveryService>();
            var profiles = await discoveryService.GetProfilesAsync();
            logger.LogInformation("Successfully completed startup profile discovery test. Found {Count} profiles.", profiles.Count);

            if (profiles.Count > 0)
            {
                var analyzer = _serviceProvider.GetRequiredService<IProfileAnalyzer>();
                var analysisResult = await analyzer.AnalyzeAsync(profiles[0]);
                logger.LogInformation("Successfully completed startup profile analysis test for {ProfileName}. Extracted {Count} extensions. System theme: {ThemeMode}.", analysisResult.ProfileName, analysisResult.ExtensionCount, analysisResult.Theme.SystemThemeMode);

                var scanner = _serviceProvider.GetRequiredService<ISecurityScanner>();
                var scanResult = await scanner.ScanAsync(profiles[0]);
                logger.LogInformation("Successfully completed startup security scan test for {ProfileName}. Risk Score: {RiskScore} ({RiskLevel}). Export Safe: {ExportSafe}.", scanResult.ProfileName, scanResult.RiskScore, scanResult.RiskLevel, scanResult.ExportSafe);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred during startup profile discovery or analysis test.");
        }

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
