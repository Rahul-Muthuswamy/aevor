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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── CRITICAL: prevent WPF from shutting down when the auth window
        // closes (before MainWindow has been shown yet).
        // LaunchMainApplication() will flip this back once MainWindow is shown.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // ── Step 1: Build the service container ──────────────────────────
        var services = new ServiceCollection();
        services.AddInfrastructureServices();

        // Navigation
        services.AddSingleton<INavigationService, NavigationService>();

        // Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<DashboardView>();
        services.AddTransient<ProfilesView>();
        services.AddTransient<TemplatesView>();
        services.AddTransient<CloneWizardView>();
        services.AddTransient<BackupsView>();
        services.AddTransient<SecurityView>();
        services.AddTransient<SettingsView>();

        // Auth windows
        services.AddTransient<SetupPasswordWindow>();
        services.AddTransient<UnlockWindow>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProfilesViewModel>();
        services.AddTransient<TemplatesViewModel>();
        services.AddTransient<CloneWizardViewModel>();
        services.AddTransient<BackupsViewModel>();
        services.AddTransient<SecurityViewModel>();
        services.AddSingleton<SettingsViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        // ── Step 2: Gate entry behind master password ─────────────────────
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

        // MainWindow is shown by LaunchMainApplication(), called by the auth
        // windows on successful password entry.
    }

    /// <summary>
    /// Called by <see cref="SetupPasswordWindow"/> and <see cref="UnlockWindow"/>
    /// immediately after successful authentication.
    ///
    /// ORDERING GUARANTEE:
    ///   MainWindow is shown synchronously BEFORE this method returns, so
    ///   the auth window's subsequent Close() call will not trigger shutdown
    ///   (there will always be at least one open window at that point).
    ///
    ///   Background startup health-checks run after the window is visible.
    /// </summary>
    public async void LaunchMainApplication()
    {
        if (_serviceProvider is null)
            throw new InvalidOperationException("Service provider not initialised.");

        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Launching main application after successful authentication.");

        // ── Show MainWindow FIRST — before any await so the window is
        // visible before the auth window closes. ──────────────────────────
        var navigationService = _serviceProvider.GetRequiredService<INavigationService>();
        navigationService.NavigateTo<DashboardViewModel>();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>();

        // Register as the application's main window and restore normal
        // shutdown behaviour: app exits when MainWindow is closed.
        this.MainWindow = mainWindow;
        mainWindow.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // ── Background startup health-checks (non-fatal) ─────────────────
        // These run after the window is already visible so the user
        // is not blocked waiting for them.
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
