using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Aevor.Application.Interfaces;
using Aevor.Infrastructure;
using Aevor.UI.Services;
using Aevor.UI.ViewModels;
using Aevor.UI.Views;

namespace Aevor.UI;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;
    public IServiceProvider Services => _serviceProvider ?? throw new InvalidOperationException("Service provider not initialised.");

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
        {
            try
            {
                System.IO.File.WriteAllText("M:\\project\\aevor\\crash.txt", ev.ExceptionObject.ToString());
            }
            catch {}
        };
        DispatcherUnhandledException += (s, ev) =>
        {
            try
            {
                System.IO.File.WriteAllText("M:\\project\\aevor\\crash.txt", ev.Exception.ToString());
            }
            catch {}
        };

        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var services = new ServiceCollection();
        services.AddInfrastructureServices();

        services.AddSingleton<INavigationService, NavigationService>();

        services.AddSingleton<MainWindow>();
        services.AddTransient<DashboardView>();
        services.AddTransient<ProfilesView>();
        services.AddTransient<TemplatesView>();
        services.AddTransient<CloneWizardView>();
        services.AddTransient<BackupsView>();
        services.AddTransient<SecurityView>();
        services.AddTransient<SettingsView>();

        services.AddTransient<SetupPasswordWindow>();
        services.AddTransient<UnlockWindow>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProfilesViewModel>();
        services.AddTransient<TemplatesViewModel>();
        services.AddTransient<CloneWizardViewModel>();
        services.AddTransient<BackupsViewModel>();
        services.AddTransient<SecurityViewModel>();
        services.AddSingleton<SettingsViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        var masterPasswordService = _serviceProvider.GetRequiredService<IMasterPasswordService>();

        if (!masterPasswordService.IsPasswordConfigured())
        {
            var setupWindow = _serviceProvider.GetRequiredService<SetupPasswordWindow>();
            setupWindow.Show();
        }
        else
        {
            var unlockWindow = _serviceProvider.GetRequiredService<UnlockWindow>();
            unlockWindow.Show();
        }

    }

    public async void LaunchMainApplication()
    {
        if (_serviceProvider is null)
            throw new InvalidOperationException("Service provider not initialised.");

        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Launching main application after successful authentication.");

        try
        {

            var navigationService = _serviceProvider.GetRequiredService<INavigationService>();
            navigationService.NavigateTo<DashboardViewModel>();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>();

            this.MainWindow = mainWindow;
            mainWindow.Closed += (s, e) => System.Windows.Application.Current.Shutdown();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "FATAL ERROR during LaunchMainApplication UI creation.");
            try
            {
                System.IO.File.WriteAllText("M:\\project\\aevor\\crash.txt", ex.ToString());
            }
            catch {}
            MessageBox.Show(ex.ToString(), "Fatal Launch Error");
            throw;
        }

        try
        {
            var discoveryService = _serviceProvider.GetRequiredService<IProfileDiscoveryService>();
            var profiles = await discoveryService.GetProfilesAsync();
            logger.LogInformation("Startup profile discovery: {Count} profile(s) found.", profiles.Count);

            if (profiles.Count > 0)
            {
                var analyzer = _serviceProvider.GetRequiredService<IProfileAnalyzer>();
                var analysisResult = await analyzer.AnalyzeAsync(profiles[0]);
                logger.LogInformation(
                    "Startup analysis for '{Profile}': {Exts} extension(s), theme={Theme}.",
                    analysisResult.ProfileName,
                    analysisResult.ExtensionCount,
                    analysisResult.Theme.SystemThemeMode);

                var scanner = _serviceProvider.GetRequiredService<ISecurityScanner>();
                var scanResult = await scanner.ScanAsync(profiles[0]);
                logger.LogInformation(
                    "Startup scan for '{Profile}': risk={Score} ({Level}), exportSafe={Safe}.",
                    scanResult.ProfileName,
                    scanResult.RiskScore,
                    scanResult.RiskLevel,
                    scanResult.ExportSafe);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Non-fatal error during startup checks.");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
