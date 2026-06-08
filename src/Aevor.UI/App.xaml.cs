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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred during startup profile discovery test.");
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
