using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Aevor.Application.Interfaces;
using Aevor.Core.Models;
using Aevor.UI.Commands;
using Aevor.UI.Models;
using SecurityFinding = Aevor.UI.Models.SecurityFinding;

namespace Aevor.UI.ViewModels;

public class SecurityViewModel : BaseViewModel
{
    private readonly ISecurityScanner _securityScanner;
    private readonly IProfileDiscoveryService _profileDiscoveryService;
    private readonly IPdfReportService _pdfReportService;
    private readonly IToastService _toastService;
    private List<SecurityScanResult> _scanResults = new();
    private List<BraveProfile> _profiles = new();

    public ObservableCollection<SecurityProfileSummary> ProfileSummaries { get; } = new();
    public ObservableCollection<SecurityFinding>        Findings         { get; } = new();
    public ObservableCollection<SecurityFinding>        FilteredFindings { get; } = new();

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

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
                OnPropertyChanged(nameof(BannerBackground));
            }
        }
    }

    public string OverallRiskLabel
    {
        get
        {

            if (OverallRiskScore <= 40) return "Secure";
            if (OverallRiskScore <= 65) return "Low Risk";
            if (OverallRiskScore <= 85) return "High Risk";
            return "Critical";
        }
    }

    public Brush OverallRiskColor
    {
        get
        {
            if (OverallRiskScore <= 40) return new SolidColorBrush(Color.FromRgb( 16, 185, 129));
            if (OverallRiskScore <= 65) return new SolidColorBrush(Color.FromRgb(245, 158,  11));
            if (OverallRiskScore <= 85) return new SolidColorBrush(Color.FromRgb(239,  68,  68));
            return new SolidColorBrush(Color.FromRgb(153, 27, 27));
        }
    }

    public Brush OverallRiskBackground
    {
        get
        {
            if (OverallRiskScore <= 40) return new SolidColorBrush(Color.FromRgb(240, 253, 244));
            if (OverallRiskScore <= 65) return new SolidColorBrush(Color.FromRgb(255, 251, 235));
            if (OverallRiskScore <= 85) return new SolidColorBrush(Color.FromRgb(255, 241, 242));
            return new SolidColorBrush(Color.FromRgb(254, 226, 226));
        }
    }

    public Brush BannerBackground => OverallRiskBackground;

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
                ApplyFindingsFilter();

                OnPropertyChanged(nameof(IsFilterAllActive));
                OnPropertyChanged(nameof(IsFilterCriticalActive));
                OnPropertyChanged(nameof(IsFilterWarningActive));
                OnPropertyChanged(nameof(IsFilterInfoActive));
            }
        }
    }

    public bool IsFilterAllActive      => SelectedSeverityFilter == "All";
    public bool IsFilterCriticalActive => SelectedSeverityFilter == "Critical";
    public bool IsFilterWarningActive  => SelectedSeverityFilter == "Warning";
    public bool IsFilterInfoActive     => SelectedSeverityFilter == "Info";

    private string _statusMessage = string.Empty;
    private int _statusMessageResetCounter;
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    var currentCounter = System.Threading.Interlocked.Increment(ref _statusMessageResetCounter);
                    Task.Delay(4000).ContinueWith(t =>
                    {
                        if (currentCounter == _statusMessageResetCounter)
                        {
                            _ = RunOnUIAsync(() => StatusMessage = string.Empty);
                        }
                    });
                }
            }
        }
    }

    public ICommand RunScanCommand          { get; }
    public ICommand FilterBySeverityCommand { get; }
    public ICommand ExportReportCommand     { get; }

    public SecurityViewModel(
        ISecurityScanner securityScanner,
        IProfileDiscoveryService profileDiscoveryService,
        IPdfReportService pdfReportService,
        IToastService toastService)
    {
        _securityScanner = securityScanner;
        _profileDiscoveryService = profileDiscoveryService;
        _pdfReportService = pdfReportService;
        _toastService = toastService;

        RunScanCommand          = new RelayCommand(OnRunScan);
        FilterBySeverityCommand = new RelayCommand<string>(OnFilterBySeverity);
        ExportReportCommand     = new RelayCommand(OnExportReport);

        Task.Run(async () => await LoadSecurityDataAsync());
    }

    private async Task LoadSecurityDataAsync()
    {
        await RunOnUIAsync(() => IsLoading = true);

        try
        {
            var discoveredProfiles = await _profileDiscoveryService.GetProfilesAsync();
            _profiles = discoveredProfiles ?? new List<BraveProfile>();

            var summaries = new List<SecurityProfileSummary>();
            var newScanResults = new List<SecurityScanResult>();

            foreach (var profile in _profiles)
            {
                try
                {
                    var result = await _securityScanner.ScanAsync(profile);
                    if (result != null)
                    {
                        newScanResults.Add(result);

                        string riskLabel = result.RiskLevel switch
                        {
                            RiskLevel.Low => "Low",
                            RiskLevel.Medium => "Medium",
                            RiskLevel.High => "High",
                            RiskLevel.Critical => "Critical",
                            _ => "Low"
                        };

                        summaries.Add(new SecurityProfileSummary
                        {
                            ProfileName = result.ProfileName,
                            RiskScore = result.RiskScore,
                            RiskLabel = riskLabel,
                            FindingCount = result.Findings.Count,
                            LastScanned = GetRelativeTimeString(result.ScanTimestamp)
                        });
                    }
                }
                catch
                {

                }
            }

            _scanResults = newScanResults;

            if (_profiles.Count > 0 && _scanResults.Count == 0)
            {
                throw new Exception("All profile scans failed.");
            }

            var overallScore = _scanResults.Any() ? (int)Math.Round(_scanResults.Average(r => r.RiskScore), MidpointRounding.AwayFromZero) : 0;
            var totalFindings = _scanResults.Sum(r => r.Findings.Count);

            var findingsList = _scanResults.SelectMany(result =>
            {
                return result.Findings.Select(finding =>
                {
                    string severityLabel = finding.Severity switch
                    {
                        SecuritySeverity.Critical => "Critical",
                        SecuritySeverity.High     => "Critical",
                        SecuritySeverity.Medium   => "Warning",
                        SecuritySeverity.Low      => "Warning",
                        SecuritySeverity.Info     => "Info",
                        _                         => "Info"
                    };

                    return new SecurityFinding
                    {
                        FindingTitle = finding.Name,
                        FindingDetail = finding.Description,
                        AffectedProfile = result.ProfileName,
                        Severity = severityLabel
                    };
                });
            }).ToList();

            var criticalCount = findingsList.Count(f => f.Severity == "Critical");
            var warningCount = findingsList.Count(f => f.Severity == "Warning");
            var excludedCount = _scanResults.Where(r => r.ExportSafe).Sum(r => r.Findings.Count);

            await RunOnUIAsync(() =>
            {
                ProfileSummaries.Clear();
                foreach (var s in summaries)
                {
                    ProfileSummaries.Add(s);
                }

                Findings.Clear();
                foreach (var f in findingsList)
                {
                    Findings.Add(f);
                }

                OverallRiskScore = overallScore;
                TotalFindings = totalFindings;
                CriticalCount = criticalCount;
                WarningCount = warningCount;
                ExcludedCount = excludedCount;

                ApplyFindingsFilter();
            });
        }
        catch (Exception ex)
        {
            await RunOnUIAsync(() =>
            {
                ProfileSummaries.Clear();
                Findings.Clear();
                FilteredFindings.Clear();
                OverallRiskScore = 0;
                TotalFindings = 0;
                CriticalCount = 0;
                WarningCount = 0;
                ExcludedCount = 0;
            });
            _toastService.Show("Scan failed: " + ex.Message, ToastType.Error);
        }
        finally
        {
            await RunOnUIAsync(() => IsLoading = false);
        }
    }

    private void ApplyFindingsFilter()
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

    private string GetRelativeTimeString(DateTime scanTime)
    {
        var elapsed = DateTime.UtcNow - scanTime.ToUniversalTime();
        if (elapsed.TotalSeconds < 0)
        {
            elapsed = TimeSpan.Zero;
        }
        if (elapsed.TotalSeconds < 60)
        {
            return "Scanned just now";
        }
        if (elapsed.TotalMinutes < 60)
        {
            return $"Scanned {(int)elapsed.TotalMinutes}m ago";
        }
        if (elapsed.TotalHours < 24)
        {
            return $"Scanned {(int)elapsed.TotalHours}h ago";
        }
        return $"Scanned {(int)elapsed.TotalDays}d ago";
    }

    private async Task RunOnUIAsync(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null)
        {
            await dispatcher.InvokeAsync(action);
        }
        else
        {
            action();
        }
    }

    private async void OnRunScan()
    {
        await RunOnUIAsync(() =>
        {
            ProfileSummaries.Clear();
            Findings.Clear();
            FilteredFindings.Clear();
        });
        _toastService.Show("Scanning all profiles...", ToastType.Info);
        await Task.Run(async () => await LoadSecurityDataAsync());
        _toastService.Show($"Scan complete — {TotalFindings} findings across {_profiles.Count} profiles", ToastType.Success);
    }

    private void OnFilterBySeverity(string? severity)
    {
        if (string.IsNullOrEmpty(severity)) return;
        SelectedSeverityFilter = severity;
        ApplyFindingsFilter();
    }

    private async void OnExportReport()
    {
        try
        {
            var lines = new List<string>();
            lines.Add("=========================================");
            lines.Add("         Aevor Security Report           ");
            lines.Add("=========================================");
            lines.Add($"Date: {DateTime.Now:F}");
            lines.Add($"Overall Score: {100 - OverallRiskScore}/100");
            lines.Add($"Overall Risk: {OverallRiskLabel}");
            lines.Add($"Total Findings: {TotalFindings}");
            lines.Add($"Critical Findings: {CriticalCount}");
            lines.Add($"Warning Findings: {WarningCount}");
            lines.Add($"Excluded Findings: {ExcludedCount}");
            lines.Add("");
            lines.Add("-----------------------------------------");
            lines.Add(" Profile Summaries                       ");
            lines.Add("-----------------------------------------");
            foreach (var p in ProfileSummaries)
            {
                lines.Add($"- {p.ProfileName}: Score: {p.RiskScore} ({p.RiskLabel}), {p.FindingCount} findings, Last Scanned: {p.LastScanned}");
            }
            lines.Add("");
            lines.Add("-----------------------------------------");
            lines.Add(" Detailed Findings                       ");
            lines.Add("-----------------------------------------");
            foreach (var f in Findings)
            {
                lines.Add($"[{f.Severity}] {f.FindingTitle}");
                lines.Add($"  Affected Profile: {f.AffectedProfile}");
                lines.Add($"  Details: {f.FindingDetail}");
                lines.Add("");
            }

            await RunOnUIAsync(() =>
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "AevorSecurityReport.pdf",
                    DefaultExt = ".pdf",
                    Filter = "PDF Files (*.pdf)|*.pdf"
                };

                bool? showResult = dialog.ShowDialog();
                if (showResult == true)
                {
                    var pdfBytes = _pdfReportService.GenerateReport("Aevor Security Report", lines);
                    System.IO.File.WriteAllBytes(dialog.FileName, pdfBytes);
                    _toastService.Show("Report exported to " + dialog.FileName, ToastType.Success);
                }
            });
        }
        catch (Exception ex)
        {
            _toastService.Show("Export failed: " + ex.Message, ToastType.Error);
        }
    }
}
