using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using Aevor.UI.Commands;
using Aevor.UI.Models;

namespace Aevor.UI.ViewModels;

public class SecurityViewModel : BaseViewModel
{
    // ── Collections ────────────────────────────────────────────────────
    public ObservableCollection<SecurityProfileSummary> ProfileSummaries { get; } = new();
    public ObservableCollection<SecurityFinding>        Findings         { get; } = new();
    public ObservableCollection<SecurityFinding>        FilteredFindings { get; } = new();

    // ── Properties ─────────────────────────────────────────────────────
    private int _overallRiskScore;
    public int OverallRiskScore
    {
        get => _overallRiskScore;
        set
        {
            if (SetProperty(ref _overallRiskScore, value))
            {
                OnPropertyChanged(nameof(OverallRiskLabel));
                OnPropertyChanged(nameof(OverallRiskColor));
                OnPropertyChanged(nameof(OverallRiskBackground));
            }
        }
    }

    public string OverallRiskLabel
    {
        get
        {
            if (OverallRiskScore >= 75) return "High";
            if (OverallRiskScore >= 35) return "Medium";
            return "Low";
        }
    }

    public Brush OverallRiskColor => OverallRiskLabel switch
    {
        "High"   => new SolidColorBrush(Color.FromRgb(239, 68,  68)),  // #EF4444
        "Medium" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),  // #F59E0B
        _        => new SolidColorBrush(Color.FromRgb(16,  185, 129)), // #10B981 (Low)
    };

    public Brush OverallRiskBackground => OverallRiskLabel switch
    {
        "High"   => new SolidColorBrush(Color.FromRgb(255, 241, 242)), // #FFF1F2
        "Medium" => new SolidColorBrush(Color.FromRgb(255, 251, 235)), // #FFFBEB
        _        => new SolidColorBrush(Color.FromRgb(240, 253, 244)), // #F0FDF4 (Low)
    };

    private int _totalFindings;
    public int TotalFindings
    {
        get => _totalFindings;
        private set => SetProperty(ref _totalFindings, value);
    }

    private int _criticalCount;
    public int CriticalCount
    {
        get => _criticalCount;
        private set => SetProperty(ref _criticalCount, value);
    }

    private int _warningCount;
    public int WarningCount
    {
        get => _warningCount;
        private set => SetProperty(ref _warningCount, value);
    }

    private int _excludedCount;
    public int ExcludedCount
    {
        get => _excludedCount;
        private set => SetProperty(ref _excludedCount, value);
    }

    private string _selectedSeverityFilter = "All";
    public string SelectedSeverityFilter
    {
        get => _selectedSeverityFilter;
        set
        {
            if (SetProperty(ref _selectedSeverityFilter, value))
            {
                ApplyFilter();
                // trigger PropertyChanged for active state of filter pills
                OnPropertyChanged(nameof(IsFilterAllActive));
                OnPropertyChanged(nameof(IsFilterCriticalActive));
                OnPropertyChanged(nameof(IsFilterWarningActive));
                OnPropertyChanged(nameof(IsFilterInfoActive));
            }
        }
    }

    // Helper bool properties for pill active states
    public bool IsFilterAllActive      => SelectedSeverityFilter == "All";
    public bool IsFilterCriticalActive => SelectedSeverityFilter == "Critical";
    public bool IsFilterWarningActive  => SelectedSeverityFilter == "Warning";
    public bool IsFilterInfoActive     => SelectedSeverityFilter == "Info";

    // ── Commands ───────────────────────────────────────────────────────
    public ICommand RunScanCommand          { get; }
    public ICommand FilterBySeverityCommand { get; }
    public ICommand ExportReportCommand     { get; }

    // ── Constructor ────────────────────────────────────────────────────
    public SecurityViewModel()
    {
        RunScanCommand          = new RelayCommand(OnRunScan);
        FilterBySeverityCommand = new RelayCommand<string>(OnFilterBySeverity);
        ExportReportCommand     = new RelayCommand(OnExportReport);

        LoadSampleData();
    }

    // ── Sample Data ────────────────────────────────────────────────────
    private void LoadSampleData()
    {
        ProfileSummaries.Clear();
        ProfileSummaries.Add(new SecurityProfileSummary
        {
            ProfileName  = "Personal",
            RiskScore    = 15,
            RiskLabel    = "Low",
            FindingCount = 2,
            LastScanned  = "Scanned 10m ago"
        });
        ProfileSummaries.Add(new SecurityProfileSummary
        {
            ProfileName  = "Work",
            RiskScore    = 48,
            RiskLabel    = "Medium",
            FindingCount = 3,
            LastScanned  = "Scanned 2h ago"
        });
        ProfileSummaries.Add(new SecurityProfileSummary
        {
            ProfileName  = "Research",
            RiskScore    = 82,
            RiskLabel    = "High",
            FindingCount = 5,
            LastScanned  = "Scanned 1d ago"
        });

        Findings.Clear();
        Findings.Add(new SecurityFinding
        {
            FindingTitle    = "Saved Passwords Detected",
            FindingDetail   = "23 login credentials stored in plain local state database",
            AffectedProfile = "Research",
            Severity        = "Critical"
        });
        Findings.Add(new SecurityFinding
        {
            FindingTitle    = "Active Session Cookies",
            FindingDetail   = "Session cookies found for multiple financial websites",
            AffectedProfile = "Work",
            Severity        = "Warning"
        });
        Findings.Add(new SecurityFinding
        {
            FindingTitle    = "Autofill Forms Information",
            FindingDetail   = "Credit card profiles and forms stored in local autofill cache",
            AffectedProfile = "Personal",
            Severity        = "Warning"
        });
        Findings.Add(new SecurityFinding
        {
            FindingTitle    = "Untrusted Extensions Configured",
            FindingDetail   = "2 search provider extensions request wide browsing access",
            AffectedProfile = "Research",
            Severity        = "Warning"
        });
        Findings.Add(new SecurityFinding
        {
            FindingTitle    = "Sandbox Protection Activated",
            FindingDetail   = "Google Safe Browsing is properly set to Enhanced Protection Mode",
            AffectedProfile = "Personal",
            Severity        = "Info"
        });
        Findings.Add(new SecurityFinding
        {
            FindingTitle    = "Form History Audited",
            FindingDetail   = "Form history has been successfully excluded from clone configuration",
            AffectedProfile = "Work",
            Severity        = "Excluded"
        });
        Findings.Add(new SecurityFinding
        {
            FindingTitle    = "Third-Party Cookies Blocked",
            FindingDetail   = "Browser shield blocking setting successfully applied for cross-site scripts",
            AffectedProfile = "Research",
            Severity        = "Excluded"
        });

        // Compute Overall Risk Score: average score
        OverallRiskScore = (int)ProfileSummaries.Average(p => p.RiskScore);

        UpdateCounts();
        ApplyFilter();
    }

    // ── Helper ─────────────────────────────────────────────────────────
    private void UpdateCounts()
    {
        TotalFindings = Findings.Count;
        CriticalCount = Findings.Count(f => f.Severity == "Critical");
        WarningCount  = Findings.Count(f => f.Severity == "Warning");
        ExcludedCount = Findings.Count(f => f.Severity == "Excluded");
    }

    private void ApplyFilter()
    {
        FilteredFindings.Clear();
        foreach (var f in Findings)
        {
            if (SelectedSeverityFilter == "All" || f.Severity.Equals(SelectedSeverityFilter, StringComparison.OrdinalIgnoreCase))
            {
                FilteredFindings.Add(f);
            }
        }
    }

    // ── Command Handlers ───────────────────────────────────────────────
    private void OnRunScan()
    {
        LoadSampleData(); // Re-scan simulation reload
    }

    private void OnFilterBySeverity(string? severity)
    {
        if (string.IsNullOrEmpty(severity)) return;
        SelectedSeverityFilter = severity;
    }

    private void OnExportReport()
    {
        // Mock export trigger
    }
}
