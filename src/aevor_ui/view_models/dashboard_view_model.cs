using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Aevor.Application.Interfaces;
using Aevor.Core.Models;
using Aevor.UI.Commands;
using Aevor.UI.Services;

namespace Aevor.UI.ViewModels;

public class ActivityItem
{
    public string Icon { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TimeAgo { get; set; } = string.Empty;

    public string ActivityType { get; set; } = "info";

    public Brush DotColor => ActivityType switch
    {
        "success" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
        "warning" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        _         => new SolidColorBrush(Color.FromRgb(99, 102, 241)),
    };
}

public class DashboardViewModel : BaseViewModel
{

    private readonly IProfileDiscoveryService _profileDiscoveryService;
    private readonly IBackupService _backupService;
    private readonly ISecurityScanner _securityScanner;
    private readonly INavigationService _navigationService;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly IToastService _toastService;

    private int _totalProfiles;
    public int TotalProfiles
    {
        get => _totalProfiles;
        set => SetProperty(ref _totalProfiles, value);
    }

    private int _totalTemplates;
    public int TotalTemplates
    {
        get => _totalTemplates;
        set => SetProperty(ref _totalTemplates, value);
    }

    private int _totalBackups;
    public int TotalBackups
    {
        get => _totalBackups;
        set => SetProperty(ref _totalBackups, value);
    }

    private string _securityStatus = "Secure";
    public string SecurityStatus
    {
        get => _securityStatus;
        set
        {
            if (SetProperty(ref _securityStatus, value))
            {
                OnPropertyChanged(nameof(SecurityStatusColor));
            }
        }
    }

    public Brush SecurityStatusColor => SecurityStatus switch
    {
        "Warning"  => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        "At Risk"  => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        "Unknown"  => new SolidColorBrush(Color.FromRgb(107, 114, 128)),
        _          => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
    };

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public ObservableCollection<ActivityItem> RecentActivities { get; } = new();

    public ICommand AnalyzeProfilesCommand { get; }
    public ICommand CreateTemplateCommand  { get; }
    public ICommand CreateBackupCommand    { get; }
    public ICommand ViewSecurityCommand    { get; }
    public ICommand RefreshCommand         { get; }

    public DashboardViewModel(
        IProfileDiscoveryService profileDiscoveryService,
        IBackupService backupService,
        ISecurityScanner securityScanner,
        INavigationService navigationService,
        SettingsViewModel settingsViewModel,
        IToastService toastService)
    {
        _profileDiscoveryService = profileDiscoveryService ?? throw new ArgumentNullException(nameof(profileDiscoveryService));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _securityScanner = securityScanner ?? throw new ArgumentNullException(nameof(securityScanner));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _settingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
        _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));

        AnalyzeProfilesCommand = new RelayCommand(OnAnalyzeProfiles);
        CreateTemplateCommand  = new RelayCommand(OnCreateTemplate);
        CreateBackupCommand    = new RelayCommand(OnCreateBackup);
        ViewSecurityCommand    = new RelayCommand(OnViewSecurity);
        RefreshCommand         = new RelayCommand(() => Task.Run(async () => await LoadDashboardDataAsync()));

        Task.Run(async () => await LoadDashboardDataAsync());
    }

    private async Task LoadDashboardDataAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {

            var profiles = await _profileDiscoveryService.GetProfilesAsync();
            TotalProfiles = profiles?.Count ?? 0;

            var backups = await _backupService.GetBackupsAsync();
            TotalBackups = backups?.Count ?? 0;

            SecurityScanResult? scanResult = null;
            BraveProfile? scannedProfile = null;

            if (_settingsViewModel.AutoScanOnStartup && profiles != null && profiles.Count > 0)
            {
                scannedProfile = profiles[0];
                scanResult = await _securityScanner.ScanAsync(scannedProfile);

                if (scanResult != null)
                {
                    bool hasCritical = scanResult.Findings.Any(f =>
                        f.Severity == SecuritySeverity.Critical || f.Severity == SecuritySeverity.High);
                    bool hasWarnings = scanResult.Findings.Any(f =>
                        f.Severity == SecuritySeverity.Medium || f.Severity == SecuritySeverity.Low);

                    if (hasCritical)
                    {
                        SecurityStatus = "At Risk";
                    }
                    else if (hasWarnings || scanResult.Findings.Count > 0)
                    {
                        SecurityStatus = "Warning";
                    }
                    else
                    {
                        SecurityStatus = "Secure";
                    }
                }
                else
                {
                    SecurityStatus = "Secure";
                }
            }
            else
            {
                SecurityStatus = "Secure";
            }

            TotalTemplates = 0;

            var activities = new List<ActivityItem>();

            if (profiles != null)
            {
                foreach (var profile in profiles)
                {
                    activities.Add(new ActivityItem
                    {
                        Icon         = "🔍",
                        Title        = "Profile Discovered",
                        Description  = profile.DisplayName + " detected",
                        ActivityType = "info",
                        TimeAgo      = "Just now"
                    });
                }
            }

            if (backups != null)
            {
                foreach (var backup in backups.Take(3))
                {
                    activities.Add(new ActivityItem
                    {
                        Icon         = "✓",
                        Title        = "Backup Available",
                        Description  = backup.ProfileName + " snapshot ready",
                        ActivityType = "success",
                        TimeAgo      = FormatRelativeTime(backup.CreatedTimestamp)
                    });
                }
            }

            if (scanResult != null && scanResult.Findings.Count > 0 && scannedProfile != null)
            {
                activities.Add(new ActivityItem
                {
                    Icon         = "⚠",
                    Title        = "Security Warning",
                    Description  = "Issues found in " + scannedProfile.DisplayName,
                    ActivityType = "warning",
                    TimeAgo      = "Just now"
                });
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                RecentActivities.Clear();
                foreach (var activity in activities)
                {
                    RecentActivities.Add(activity);
                }
            });
        }
        catch (Exception ex)
        {

            SecurityStatus = "Unknown";
            ErrorMessage = ex.Message;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (RecentActivities.Count == 0)
                {
                    RecentActivities.Add(new ActivityItem
                    {
                        Icon         = "⚠",
                        Title        = "Dashboard Load Error",
                        Description  = "Unable to load dashboard data. Try refreshing.",
                        ActivityType = "warning",
                        TimeAgo      = "Just now"
                    });
                }
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string FormatRelativeTime(DateTime timestamp)
    {
        var diff = DateTime.Now - timestamp;

        if (diff.TotalSeconds < 60)
            return "Just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} min ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} hr ago";
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays} day{((int)diff.TotalDays != 1 ? "s" : "")} ago";
        if (diff.TotalDays < 30)
            return $"{(int)(diff.TotalDays / 7)} week{((int)(diff.TotalDays / 7) != 1 ? "s" : "")} ago";

        return timestamp.ToString("MMM d, yyyy");
    }

    private void OnAnalyzeProfiles() => _navigationService.NavigateTo<ProfilesViewModel>();
    private void OnCreateTemplate()  => _navigationService.NavigateTo<TemplatesViewModel>();
    private void OnCreateBackup()    => _navigationService.NavigateTo<BackupsViewModel>();
    private void OnViewSecurity()    => _navigationService.NavigateTo<SecurityViewModel>();
}
