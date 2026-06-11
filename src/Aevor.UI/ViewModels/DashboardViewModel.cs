using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using Aevor.UI.Commands;

namespace Aevor.UI.ViewModels;

// -------------------------
// Model: ActivityItem
// -------------------------
public class ActivityItem
{
    public string Icon { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TimeAgo { get; set; } = string.Empty;

    /// <summary>
    /// "success" | "warning" | "info"
    /// </summary>
    public string ActivityType { get; set; } = "info";

    public Brush DotColor => ActivityType switch
    {
        "success" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),   // #10B981
        "warning" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),   // #F59E0B
        _         => new SolidColorBrush(Color.FromRgb(99, 102, 241)),   // indigo
    };
}

// -------------------------
// ViewModel: DashboardViewModel
// -------------------------
public class DashboardViewModel : BaseViewModel
{
    // ── Stats ──────────────────────────────────────────────────────────
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
        "Warning"  => new SolidColorBrush(Color.FromRgb(245, 158, 11)),  // #F59E0B
        "At Risk"  => new SolidColorBrush(Color.FromRgb(239, 68, 68)),   // #EF4444
        _          => new SolidColorBrush(Color.FromRgb(16, 185, 129)),  // #10B981
    };

    // ── Recent Activity ────────────────────────────────────────────────
    public ObservableCollection<ActivityItem> RecentActivities { get; } = new();

    // ── Commands ───────────────────────────────────────────────────────
    public ICommand AnalyzeProfilesCommand { get; }
    public ICommand CreateTemplateCommand  { get; }
    public ICommand CreateBackupCommand    { get; }
    public ICommand ViewSecurityCommand    { get; }

    // ── Constructor ────────────────────────────────────────────────────
    public DashboardViewModel()
    {
        AnalyzeProfilesCommand = new RelayCommand(OnAnalyzeProfiles);
        CreateTemplateCommand  = new RelayCommand(OnCreateTemplate);
        CreateBackupCommand    = new RelayCommand(OnCreateBackup);
        ViewSecurityCommand    = new RelayCommand(OnViewSecurity);

        LoadSampleData();
    }

    // ── Sample Data ────────────────────────────────────────────────────
    private void LoadSampleData()
    {
        TotalProfiles  = 6;
        TotalTemplates = 4;
        TotalBackups   = 12;
        SecurityStatus = "Secure";

        RecentActivities.Add(new ActivityItem
        {
            Icon         = "✓",
            Title        = "Backup Created",
            Description  = "Profile \"Work\" was backed up successfully",
            TimeAgo      = "2 min ago",
            ActivityType = "success"
        });
        RecentActivities.Add(new ActivityItem
        {
            Icon         = "⚠",
            Title        = "Security Warning",
            Description  = "Profile \"Research\" contains stored login data",
            TimeAgo      = "18 min ago",
            ActivityType = "warning"
        });
        RecentActivities.Add(new ActivityItem
        {
            Icon         = "i",
            Title        = "Template Applied",
            Description  = "\"Dev Setup\" template applied to Profile \"Dev\"",
            TimeAgo      = "1 hr ago",
            ActivityType = "info"
        });
        RecentActivities.Add(new ActivityItem
        {
            Icon         = "✓",
            Title        = "Profile Cloned",
            Description  = "\"Personal\" cloned into \"Personal — Backup\"",
            TimeAgo      = "3 hr ago",
            ActivityType = "success"
        });
        RecentActivities.Add(new ActivityItem
        {
            Icon         = "i",
            Title        = "Profile Discovered",
            Description  = "6 Brave profiles detected on startup scan",
            TimeAgo      = "Yesterday",
            ActivityType = "info"
        });
    }

    // ── Command Handlers ───────────────────────────────────────────────
    private void OnAnalyzeProfiles() { /* wired to NavigationService in future */ }
    private void OnCreateTemplate()  { }
    private void OnCreateBackup()    { }
    private void OnViewSecurity()    { }
}
