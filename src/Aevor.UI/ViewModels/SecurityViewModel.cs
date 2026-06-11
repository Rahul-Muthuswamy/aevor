using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Aevor.Application.Interfaces;
using Aevor.Core.Models;
using Aevor.UI.Commands;
using Aevor.UI.Models;

namespace Aevor.UI.ViewModels;

public class SecurityViewModel : BaseViewModel
{
    private readonly IProfileDiscoveryService _profileDiscoveryService;
    private readonly ISecurityScanner _securityScanner;

    // ── Collections ────────────────────────────────────────────────────
    public ObservableCollection<BraveProfile> Profiles                { get; } = new();
    public ObservableCollection<SecurityFindingUIItem> Findings       { get; } = new();

    // ── Properties ─────────────────────────────────────────────────────
    private BraveProfile? _selectedProfile;
    public BraveProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                if (value != null)
                {
                    _ = RunScanAsync(value);
                }
                else
                {
                    ScanResult = null;
                    Findings.Clear();
                }
            }
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
                OnPropertyChanged(nameof(HasScanResult));
        }
    }

    private SecurityScanResult? _scanResult;
    public SecurityScanResult? ScanResult
    {
        get => _scanResult;
        set
        {
            if (SetProperty(ref _scanResult, value))
            {
                OnPropertyChanged(nameof(HasScanResult));
                UpdateScanProperties();
            }
        }
    }

    public bool HasScanResult => ScanResult != null && !IsScanning;

    // ── Scan Properties ────────────────────────────────────────────────
    private int _riskScore;
    public int RiskScore
    {
        get => _riskScore;
        private set => SetProperty(ref _riskScore, value);
    }

    private string _riskLevel = string.Empty;
    public string RiskLevel
    {
        get => _riskLevel;
        private set => SetProperty(ref _riskLevel, value);
    }

    private bool _exportSafe;
    public bool ExportSafe
    {
        get => _exportSafe;
        private set => SetProperty(ref _exportSafe, value);
    }

    private bool _hasPasswords;
    public bool HasPasswords
    {
        get => _hasPasswords;
        private set => SetProperty(ref _hasPasswords, value);
    }

    private bool _hasCookies;
    public bool HasCookies
    {
        get => _hasCookies;
        private set => SetProperty(ref _hasCookies, value);
    }

    private bool _hasWalletData;
    public bool HasWalletData
    {
        get => _hasWalletData;
        private set => SetProperty(ref _hasWalletData, value);
    }

    private bool _hasAutofillData;
    public bool HasAutofillData
    {
        get => _hasAutofillData;
        private set => SetProperty(ref _hasAutofillData, value);
    }

    private bool _hasSessions;
    public bool HasSessions
    {
        get => _hasSessions;
        private set => SetProperty(ref _hasSessions, value);
    }

    private bool _hasExtensionStorage;
    public bool HasExtensionStorage
    {
        get => _hasExtensionStorage;
        private set => SetProperty(ref _hasExtensionStorage, value);
    }

    private string _scanTimestamp = string.Empty;
    public string ScanTimestamp
    {
        get => _scanTimestamp;
        private set => SetProperty(ref _scanTimestamp, value);
    }

    // ── Commands ───────────────────────────────────────────────────────
    public ICommand LoadProfilesCommand { get; }
    public ICommand ScanCommand         { get; }

    // ── Constructor ────────────────────────────────────────────────────
    public SecurityViewModel(IProfileDiscoveryService profileDiscoveryService, ISecurityScanner securityScanner)
    {
        _profileDiscoveryService = profileDiscoveryService ?? throw new ArgumentNullException(nameof(profileDiscoveryService));
        _securityScanner = securityScanner ?? throw new ArgumentNullException(nameof(securityScanner));

        LoadProfilesCommand = new RelayCommand(async () => await LoadProfilesAsync());
        ScanCommand         = new RelayCommand(async () => { if (SelectedProfile != null) await RunScanAsync(SelectedProfile); });

        // Initial load
        _ = LoadProfilesAsync();
    }

    // ── Methods ────────────────────────────────────────────────────────
    public async Task LoadProfilesAsync()
    {
        IsLoading = true;
        try
        {
            var discovered = await _profileDiscoveryService.GetProfilesAsync();
            Profiles.Clear();
            foreach (var p in discovered)
            {
                Profiles.Add(p);
            }

            // Auto-select first profile or last used
            var initial = Profiles.FirstOrDefault(p => p.IsLastUsed) ?? Profiles.FirstOrDefault();
            if (initial != null)
            {
                SelectedProfile = initial;
            }
        }
        catch
        {
            // Handle loading error (silent in UI placeholder)
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task RunScanAsync(BraveProfile profile)
    {
        if (profile == null) return;

        IsScanning = true;
        try
        {
            var result = await _securityScanner.ScanAsync(profile);
            ScanResult = result;
        }
        catch
        {
            ScanResult = null;
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void UpdateScanProperties()
    {
        if (ScanResult == null)
        {
            RiskScore           = 0;
            RiskLevel           = string.Empty;
            ExportSafe          = true;
            HasPasswords        = false;
            HasCookies          = false;
            HasWalletData       = false;
            HasAutofillData     = false;
            HasSessions         = false;
            HasExtensionStorage = false;
            ScanTimestamp       = string.Empty;
            Findings.Clear();
            return;
        }

        RiskScore           = ScanResult.RiskScore;
        RiskLevel           = ScanResult.RiskLevel.ToString();
        ExportSafe          = ScanResult.ExportSafe;
        HasPasswords        = ScanResult.HasPasswords;
        HasCookies          = ScanResult.HasCookies;
        HasWalletData       = ScanResult.HasWalletData;
        HasAutofillData     = ScanResult.HasAutofillData;
        HasSessions         = ScanResult.HasSessions;
        HasExtensionStorage = ScanResult.HasExtensionStorage;
        ScanTimestamp       = ScanResult.ScanTimestamp.ToLocalTime().ToString("MMM d, yyyy h:mm tt");

        Findings.Clear();
        foreach (var f in ScanResult.Findings.OrderByDescending(x => x.Severity))
        {
            Findings.Add(SecurityFindingUIItem.FromFinding(f));
        }
    }
}
