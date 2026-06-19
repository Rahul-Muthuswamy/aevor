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
using Aevor.UI.Services;

namespace Aevor.UI.ViewModels;

public class CloneWizardViewModel : BaseViewModel
{

    private readonly ICloneEngine             _cloneEngine;
    private readonly IProfileDiscoveryService _profileDiscoveryService;
    private readonly ISecurityScanner         _securityScanner;
    private readonly IBackupService           _backupService;
    private readonly INavigationService       _navigationService;
    private readonly IBraveInstallationService _braveInstallationService;
    private readonly SettingsViewModel         _settingsViewModel;
    private readonly IToastService             _toastService;

    private List<BraveProfile>  _discoveredProfiles = new();
    private BraveProfile?       _selectedRawProfile;
    private SecurityScanResult? _lastScanResult;
    private BackupResult?       _lastBackupResult;
    private ClonePreview?       _lastClonePreview;
    private bool                _cloneHasRun;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(CanGoBack));
            }
        }
    }

    private string _loadingMessage = "Loading profiles…";
    public string LoadingMessage
    {
        get => _loadingMessage;
        set => SetProperty(ref _loadingMessage, value);
    }

    private string _wizardErrorMessage = string.Empty;
    public string WizardErrorMessage
    {
        get => _wizardErrorMessage;
        set => SetProperty(ref _wizardErrorMessage, value);
    }

    public ObservableCollection<CloneStep> Steps { get; } = new();

    private int _currentStepIndex;
    public int CurrentStepIndex
    {
        get => _currentStepIndex;
        set
        {
            if (SetProperty(ref _currentStepIndex, value))
            {
                UpdateStepStates();
                OnPropertyChanged(nameof(CurrentStepTitle));
                OnPropertyChanged(nameof(CurrentStepDescription));
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(IsLastStep));
                OnPropertyChanged(nameof(ShowNextButton));
            }
        }
    }

    public string CurrentStepTitle => CurrentStepIndex switch
    {
        0 => "Select Source Profile",
        1 => "Security Review",
        2 => "Create Backup",
        3 => "Configure Clone",
        4 => "Execute Clone",
        5 => "Clone Complete",
        _ => string.Empty
    };

    public string CurrentStepDescription => CurrentStepIndex switch
    {
        0 => "Choose the Brave profile you want to clone.",
        1 => "Review the security findings for the selected profile.",
        2 => "Protect your original profile before cloning.",
        3 => "Choose what to include in the cloned profile.",
        4 => "Review your settings and start the clone operation.",
        5 => "Your profile has been cloned successfully.",
        _ => string.Empty
    };

    public bool CanGoBack  => CurrentStepIndex > 0 && CurrentStepIndex < 5 && !IsBackingUp && !IsScanning && !IsLoading && !IsCloning;
    public bool CanGoNext  => CurrentStepIndex < 4 && !IsBackingUp && !IsScanning && !IsLoading && !IsCloning;
    public bool IsLastStep => CurrentStepIndex == 5;
    public bool ShowNextButton => CurrentStepIndex < 4;

    public ObservableCollection<string> AvailableProfiles { get; } = new();

    private string _selectedSourceProfile = string.Empty;
    public string SelectedSourceProfile
    {
        get => _selectedSourceProfile;
        set
        {
            if (SetProperty(ref _selectedSourceProfile, value))
                NewProfileName = string.IsNullOrWhiteSpace(value) ? string.Empty : $"{value} — Copy";
        }
    }

    public ObservableCollection<SecurityFindingItem> SecurityFindings { get; } = new();

    public bool HasWarnings => SecurityFindings.Any(f => f.Severity == "warning");

    private string _securitySummary = string.Empty;
    public string SecuritySummary
    {
        get => _securitySummary;
        set => SetProperty(ref _securitySummary, value);
    }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(CanGoBack));
            }
        }
    }

    private static readonly Brush _warningBannerBrush  = MakeFrozen(Color.FromRgb(255, 251, 235));
    private static readonly Brush _safeBannerBrush     = MakeFrozen(Color.FromRgb(240, 253, 244));
    private static Brush MakeFrozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    public Brush SecurityBannerBackground => HasWarnings ? _warningBannerBrush : _safeBannerBrush;

    private bool _createBackupBeforeClone = true;
    public bool CreateBackupBeforeClone
    {
        get => _createBackupBeforeClone;
        set => SetProperty(ref _createBackupBeforeClone, value);
    }

    private string _backupStatusMessage = string.Empty;
    public string BackupStatusMessage
    {
        get => _backupStatusMessage;
        set => SetProperty(ref _backupStatusMessage, value);
    }

    private string _backupLocation = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aevor", "Backups");
    public string BackupLocation
    {
        get => _backupLocation;
        set => SetProperty(ref _backupLocation, value);
    }

    private bool _isBackingUp;
    public bool IsBackingUp
    {
        get => _isBackingUp;
        set
        {
            if (SetProperty(ref _isBackingUp, value))
            {
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(CanGoBack));
            }
        }
    }

    private bool _backupComplete;
    public bool BackupComplete
    {
        get => _backupComplete;
        set => SetProperty(ref _backupComplete, value);
    }

    private string _newProfileName = string.Empty;
    public string NewProfileName
    {
        get => _newProfileName;
        set => SetProperty(ref _newProfileName, value);
    }

    private bool _copyExtensions    = true;
    private bool _copyBookmarks     = true;
    private bool _copySettings      = true;
    private bool _copyThemes        = true;
    private bool _copySearchEngines = true;

    public bool CopyExtensions    { get => _copyExtensions;    set => SetProperty(ref _copyExtensions,    value); }
    public bool CopyBookmarks     { get => _copyBookmarks;     set => SetProperty(ref _copyBookmarks,     value); }
    public bool CopySettings      { get => _copySettings;      set => SetProperty(ref _copySettings,      value); }
    public bool CopyThemes        { get => _copyThemes;        set => SetProperty(ref _copyThemes,        value); }
    public bool CopySearchEngines { get => _copySearchEngines; set => SetProperty(ref _copySearchEngines, value); }

    public ObservableCollection<string> PreviewSettingsToCopy   { get; } = new();
    public ObservableCollection<string> PreviewExtensionsToCopy { get; } = new();
    public ObservableCollection<string> PreviewWarnings         { get; } = new();

    private double _cloneProgress;
    public double CloneProgress
    {
        get => _cloneProgress;
        set
        {
            if (SetProperty(ref _cloneProgress, value))
                OnPropertyChanged(nameof(CloneProgressText));
        }
    }

    public string CloneProgressText => $"{(int)CloneProgress}%";

    private string _cloneStatusMessage = "Ready to clone.";
    public string CloneStatusMessage
    {
        get => _cloneStatusMessage;
        set => SetProperty(ref _cloneStatusMessage, value);
    }

    private bool _isCloning;
    public bool IsCloning
    {
        get => _isCloning;
        set
        {
            if (SetProperty(ref _isCloning, value))
            {
                OnPropertyChanged(nameof(ShowStartButton));
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    public bool ShowStartButton => !IsCloning && !_cloneHasRun;

    private bool _cloneSuccessful;
    public bool CloneSuccessful
    {
        get => _cloneSuccessful;
        set => SetProperty(ref _cloneSuccessful, value);
    }

    private string _completionMessage = string.Empty;
    public string CompletionMessage
    {
        get => _completionMessage;
        set => SetProperty(ref _completionMessage, value);
    }

    public ICommand NextStepCommand            { get; }
    public ICommand PreviousStepCommand        { get; }
    public ICommand StartCloneCommand          { get; }
    public ICommand FinishCommand              { get; }
    public ICommand CancelCommand              { get; }
    public ICommand SelectSourceProfileCommand { get; }

    public CloneWizardViewModel(
        ICloneEngine             cloneEngine,
        IProfileDiscoveryService profileDiscoveryService,
        ISecurityScanner         securityScanner,
        IBackupService           backupService,
        INavigationService       navigationService,
        IBraveInstallationService braveInstallationService,
        SettingsViewModel         settingsViewModel,
        IToastService             toastService)
    {
        _cloneEngine             = cloneEngine;
        _profileDiscoveryService = profileDiscoveryService;
        _securityScanner         = securityScanner;
        _backupService           = backupService;
        _navigationService       = navigationService;
        _braveInstallationService = braveInstallationService;
        _settingsViewModel       = settingsViewModel;
        _toastService             = toastService;

        NextStepCommand            = new RelayCommand(OnNextStep,     () => CanGoNext && !IsLastStep);
        PreviousStepCommand        = new RelayCommand(OnPreviousStep, () => CanGoBack);
        StartCloneCommand          = new RelayCommand(OnStartClone,   () => !IsCloning && !_cloneHasRun);
        FinishCommand              = new RelayCommand(OnFinish);
        CancelCommand              = new RelayCommand(OnCancel);
        SelectSourceProfileCommand = new RelayCommand<string>(OnSelectSourceProfile);

        InitialiseSteps();

        Task.Run(async () => await LoadWizardDataAsync());
    }

    private void InitialiseSteps()
    {
        var titles = new[] { "Source", "Security", "Backup", "Configure", "Execute", "Complete" };
        for (int i = 0; i < titles.Length; i++)
        {
            Steps.Add(new CloneStep
            {
                StepNumber  = i + 1,
                Title       = titles[i],
                IsActive    = i == 0,
                IsCompleted = false
            });
        }
    }

    private void UpdateStepStates()
    {
        for (int i = 0; i < Steps.Count; i++)
        {
            Steps[i].IsCompleted = i < _currentStepIndex;
            Steps[i].IsActive    = i == _currentStepIndex;
        }

        var tmp = Steps.ToArray();
        Steps.Clear();
        foreach (var s in tmp) Steps.Add(s);
    }

    private async Task LoadWizardDataAsync()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;

        if (dispatcher != null)
            await dispatcher.InvokeAsync(() =>
            {
                IsLoading      = true;
                LoadingMessage = "Discovering Brave profiles…";
            });

        try
        {

            var profiles = await _profileDiscoveryService.GetProfilesAsync();

            if (dispatcher != null)
                await dispatcher.InvokeAsync(() =>
                {
                    _discoveredProfiles = profiles;
                    AvailableProfiles.Clear();

                    foreach (var p in profiles)
                        AvailableProfiles.Add(p.DisplayName);

                    if (_preselectedProfileName != null)
                    {
                        ApplyPreselection();
                    }
                    else if (AvailableProfiles.Count > 0)
                    {
                        SelectedSourceProfile = AvailableProfiles[0];
                        NewProfileName        = $"{SelectedSourceProfile} — Copy";
                    }
                    else
                    {
                        WizardErrorMessage = "No Brave profiles found.";
                    }

                    IsLoading = false;
                });
        }
        catch (Exception ex)
        {
            if (dispatcher != null)
                await dispatcher.InvokeAsync(() =>
                {
                    WizardErrorMessage = $"Could not load profiles: {ex.Message}";
                    IsLoading          = false;
                });
        }
    }

    private string? _preselectedProfileName;

    public void PreselectSourceProfileAndAdvance(string profileName)
    {
        _preselectedProfileName = profileName;

        if (AvailableProfiles.Count > 0)
        {
            ApplyPreselection();
        }
    }

    private async void ApplyPreselection()
    {
        if (_preselectedProfileName == null) return;

        var matchingProfileName = AvailableProfiles.FirstOrDefault(p => p.Equals(_preselectedProfileName, StringComparison.OrdinalIgnoreCase));
        if (matchingProfileName != null)
        {
            SelectedSourceProfile = matchingProfileName;
            NewProfileName = $"{SelectedSourceProfile} — Copy";

            _selectedRawProfile = _discoveredProfiles
                .FirstOrDefault(p => p.DisplayName == SelectedSourceProfile);

            if (_selectedRawProfile is not null)
            {

                if (_braveInstallationService.IsBraveRunning())
                {
                    WizardErrorMessage = "Brave Browser is running. Please close all Brave windows before proceeding.";
                }
                else
                {
                    WizardErrorMessage = string.Empty;
                }

                CurrentStepIndex = 1;
                await RunSecurityScanAsync(_selectedRawProfile);
            }
        }
        _preselectedProfileName = null;
    }

    private async void OnNextStep()
    {
        if (!CanGoNext || IsLastStep) return;

        if (_braveInstallationService.IsBraveRunning())
        {
            WizardErrorMessage = "Brave Browser is running. Please close all Brave windows before proceeding.";
            return;
        }
        else
        {
            WizardErrorMessage = string.Empty;
        }

        try
        {

            if (CurrentStepIndex == 0)
            {
                _selectedRawProfile = _discoveredProfiles
                    .FirstOrDefault(p => p.DisplayName == SelectedSourceProfile);

                if (_selectedRawProfile is null) return;
                await RunSecurityScanAsync(_selectedRawProfile);
            }

            else if (CurrentStepIndex == 2 && CreateBackupBeforeClone)
            {
                if (_selectedRawProfile is null) return;
                await RunBackupAsync(_selectedRawProfile);
                return;
            }

            else if (CurrentStepIndex == 3)
            {
                await BuildClonePreviewAsync();
            }

            CurrentStepIndex++;
        }
        catch (Exception ex)
        {

            WizardErrorMessage = $"Step error: {ex.Message}";
        }
    }

    private void OnPreviousStep()
    {
        if (CurrentStepIndex > 0)
            CurrentStepIndex--;
    }

    private void OnSelectSourceProfile(string? profile)
    {
        if (profile != null)
            SelectedSourceProfile = profile;
    }

    private async Task RunSecurityScanAsync(BraveProfile profile)
    {

        IsScanning = true;
        SecurityFindings.Clear();
        SecuritySummary = "Scanning profile…";

        try
        {
            var result = await _securityScanner.ScanAsync(profile);
            _lastScanResult = result;

            SecurityFindings.Clear();

            if (result.HasPasswords)
                SecurityFindings.Add(new SecurityFindingItem
                {
                    Title    = "Saved Passwords Detected",
                    Detail   = "Login credentials stored — will NOT be copied to the clone.",
                    Severity = "warning"
                });

            if (result.HasCookies || result.HasSessions)
                SecurityFindings.Add(new SecurityFindingItem
                {
                    Title    = "Cookies & Active Sessions",
                    Detail   = "Active sessions detected — will NOT be transferred to the clone.",
                    Severity = "warning"
                });

            if (result.HasWalletData)
                SecurityFindings.Add(new SecurityFindingItem
                {
                    Title    = "Crypto Wallet Data Found",
                    Detail   = "Wallet data detected — will NOT be copied for security.",
                    Severity = "warning"
                });

            if (result.HasAutofillData)
                SecurityFindings.Add(new SecurityFindingItem
                {
                    Title    = "Autofill Data Present",
                    Detail   = "Form data and addresses stored — will be excluded from clone.",
                    Severity = "info"
                });

            if (result.HasExtensionStorage)
                SecurityFindings.Add(new SecurityFindingItem
                {
                    Title    = "Extensions with Local Storage",
                    Detail   = "Extension data found — can be optionally copied based on your configuration.",
                    Severity = "info"
                });

            foreach (var finding in result.Findings.Take(5))
            {
                SecurityFindings.Add(new SecurityFindingItem
                {
                    Title    = finding.Name,
                    Detail   = finding.Description,
                    Severity = finding.Severity switch
                    {
                        SecuritySeverity.High     => "warning",
                        SecuritySeverity.Critical => "warning",
                        _                         => "info"
                    }
                });
            }

            int warnCount = SecurityFindings.Count(f => f.Severity == "warning");
            SecuritySummary = warnCount > 0
                ? $"{warnCount} warning{(warnCount > 1 ? "s" : "")} found — sensitive data will not be cloned."
                : "No critical warnings. Profile is safe to clone.";

            OnPropertyChanged(nameof(HasWarnings));
            OnPropertyChanged(nameof(SecurityBannerBackground));
            IsScanning = false;
        }
        catch (Exception ex)
        {
            SecurityFindings.Add(new SecurityFindingItem
            {
                Title    = "Scan Error",
                Detail   = $"Could not complete security scan: {ex.Message}",
                Severity = "warning"
            });
            SecuritySummary = "Security scan could not complete — proceed with caution.";
            IsScanning      = false;
        }
    }

    private async Task RunBackupAsync(BraveProfile profile)
    {

        IsBackingUp         = true;
        BackupComplete      = false;
        BackupStatusMessage = "Creating backup snapshot…";

        try
        {
            BackupResult? result = null;
            try
            {
                result = await Task.Run(async () => await _backupService.CreateBackupAsync(profile));
            }
            catch (Exception ex)
            {

                BackupStatusMessage = $"Backup skipped: {ex.Message}. Proceeding without backup.";
                BackupComplete      = false;
                IsBackingUp         = false;
                CurrentStepIndex++;
                return;
            }

            _lastBackupResult = result;

            if (result.IsSuccess)
            {
                BackupStatusMessage = result.Metadata is not null
                    ? $"Backup created — {FormatBytes(result.Metadata.BackupSize)} saved."
                    : "Backup created successfully.";
                BackupComplete   = true;
                IsBackingUp      = false;
                CurrentStepIndex++;
            }
            else
            {
                BackupStatusMessage = $"Backup failed: {result.ErrorMessage ?? "Unknown error"}. You may still proceed.";
                BackupComplete   = false;
                IsBackingUp      = false;
                CurrentStepIndex++;
            }
        }
        catch (Exception ex)
        {

            BackupStatusMessage = $"Backup error: {ex.Message}. Proceeding without backup.";
            IsBackingUp         = false;
            CurrentStepIndex++;
        }
    }

    private async Task BuildClonePreviewAsync()
    {
        if (_selectedRawProfile is null) return;

        var request = new CloneRequest(
            SourceProfileFolderName:      _selectedRawProfile.FolderName,
            DestinationProfileName:       NewProfileName,
            DestinationProfileFolderName: null,
            CopyExtensions:               CopyExtensions,
            CopyBookmarks:                CopyBookmarks,
            CopySettings:                 CopySettings,
            CopyThemes:                   CopyThemes,
            CopySearchEngines:            CopySearchEngines,
            CreateBackup:                 CreateBackupBeforeClone,
            IncludeExtensions:            CopyExtensions,
            IncludeBookmarks:             CopyBookmarks,
            IncludeSettings:              true,
            IncludeThemes:                true,
            IncludeSearchEngines:         true,
            BlockActiveCookies:           _settingsViewModel.BlockActiveCookiesOnClone,
            ExcludeHistory:               _settingsViewModel.AlwaysExcludeHistory,
            ExcludePasswords:             _settingsViewModel.AlwaysExcludePasswords
        );

        try
        {
            var preview = await _cloneEngine.PreviewCloneAsync(request);
            _lastClonePreview = preview;

            RunOnUi(() =>
            {
                PreviewSettingsToCopy.Clear();
                PreviewExtensionsToCopy.Clear();
                PreviewWarnings.Clear();

                foreach (var s in preview.SettingsToCopy)   PreviewSettingsToCopy.Add(s);
                foreach (var e in preview.ExtensionsToCopy) PreviewExtensionsToCopy.Add(e);
                foreach (var w in preview.Warnings)         PreviewWarnings.Add(w);
            });
        }
        catch
        {

        }
    }

    private void OnStartClone()
    {

        if (_selectedRawProfile is null || _cloneHasRun) return;

        if (_braveInstallationService.IsBraveRunning())
        {
            WizardErrorMessage = "Brave Browser is running. Please close all Brave windows before proceeding.";
            return;
        }
        else
        {
            WizardErrorMessage = string.Empty;
        }

        _cloneHasRun = true;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;

        dispatcher?.InvokeAsync(() =>
        {
            IsCloning     = true;
            CloneProgress = 0;
            OnPropertyChanged(nameof(ShowStartButton));
        });

        var rawProfile  = _selectedRawProfile;
        var profileName = NewProfileName;
        var engine      = _cloneEngine;
        var copyExtensions    = CopyExtensions;
        var copyBookmarks     = CopyBookmarks;
        var copySettings      = CopySettings;
        var copyThemes        = CopyThemes;
        var copySearchEngines = CopySearchEngines;

        var request = new CloneRequest(
            SourceProfileFolderName:      rawProfile.FolderName,
            DestinationProfileName:       profileName,
            DestinationProfileFolderName: null,
            CopyExtensions:               copyExtensions,
            CopyBookmarks:                copyBookmarks,
            CopySettings:                 copySettings,
            CopyThemes:                   copyThemes,
            CopySearchEngines:            copySearchEngines,
            CreateBackup:                 CreateBackupBeforeClone,
            IncludeExtensions:            copyExtensions,
            IncludeBookmarks:             copyBookmarks,
            IncludeSettings:              true,
            IncludeThemes:                true,
            IncludeSearchEngines:         true,
            BlockActiveCookies:           _settingsViewModel.BlockActiveCookiesOnClone,
            ExcludeHistory:               _settingsViewModel.AlwaysExcludeHistory,
            ExcludePasswords:             _settingsViewModel.AlwaysExcludePasswords
        );

        Task.Run(async () =>
        {
            try
            {

                await ReportProgressAsync(dispatcher, () =>
                {
                    CloneProgress      = 10;
                    CloneStatusMessage = "Verifying source profile…";
                });

                await ReportProgressAsync(dispatcher, () => { CloneProgress = 25; CloneStatusMessage = "Creating backup snapshot…"; });
                await ReportProgressAsync(dispatcher, () => { CloneProgress = 40; CloneStatusMessage = "Copying profile data…"; });
                await ReportProgressAsync(dispatcher, () => { CloneProgress = 60; CloneStatusMessage = "Applying extensions…"; });
                await ReportProgressAsync(dispatcher, () => { CloneProgress = 75; CloneStatusMessage = "Copying bookmarks and settings…"; });
                await ReportProgressAsync(dispatcher, () => { CloneProgress = 90; CloneStatusMessage = "Finalising clone…"; });

                CloneResult result;
                try
                {
                    result = await engine.CloneProfileAsync(request);
                    if (result.IsSuccess && result.Report != null && copyBookmarks)
                    {
                        var sourceBookmarks = System.IO.Path.Combine(rawProfile.ProfilePath, "Bookmarks");
                        var destBookmarks = System.IO.Path.Combine(result.Report.DestinationProfile.ProfilePath, "Bookmarks");
                        if (System.IO.File.Exists(sourceBookmarks))
                        {
                            try
                            {
                                System.IO.File.Copy(sourceBookmarks, destBookmarks, true);
                            }
                            catch (System.IO.IOException)
                            {

                                try
                                {
                                    using var sourceStream = new System.IO.FileStream(sourceBookmarks, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                                    using var destStream = new System.IO.FileStream(destBookmarks, System.IO.FileMode.Create, System.IO.FileAccess.Write);
                                    sourceStream.CopyTo(destStream);
                                }
                                catch
                                {

                                }
                            }
                            catch
                            {

                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (dispatcher != null)
                    {
                        await dispatcher.InvokeAsync(() =>
                        {
                            CloneStatusMessage = $"Clone failed: {ex.Message}";
                            IsCloning          = false;
                            CloneSuccessful    = false;
                            CompletionMessage  = $"An unexpected error occurred: {ex.Message}";
                            CurrentStepIndex   = 5;
                            UpdateStepStates();
                        });
                    }
                    else
                    {
                        CloneStatusMessage = $"Clone failed: {ex.Message}";
                        IsCloning          = false;
                        CloneSuccessful    = false;
                        CompletionMessage  = $"An unexpected error occurred: {ex.Message}";
                        CurrentStepIndex   = 5;
                        UpdateStepStates();
                    }
                    return;
                }

                await ReportProgressAsync(dispatcher, () =>
                {
                    CloneProgress      = 100;
                    CloneStatusMessage = result.IsSuccess ? "Clone complete!" : $"Clone failed: {result.ErrorMessage}";
                });

                await Task.Delay(400);

                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        IsCloning         = false;
                        CloneSuccessful   = result.IsSuccess;
                        CompletionMessage = result.IsSuccess
                            ? $"Your new profile \"{profileName}\" is ready and can be launched from Brave Browser."
                            : $"Clone failed: {result.ErrorMessage ?? "Unknown error."}";
                        if (result.IsSuccess)
                        {
                            CurrentStepIndex = 5;
                            UpdateStepStates();
                        }
                        else
                        {
                            CurrentStepIndex = 5;
                            UpdateStepStates();
                        }
                    });
                }
                else
                {
                    IsCloning         = false;
                    CloneSuccessful   = result.IsSuccess;
                    CompletionMessage = result.IsSuccess
                        ? $"Your new profile \"{profileName}\" is ready and can be launched from Brave Browser."
                        : $"Clone failed: {result.ErrorMessage ?? "Unknown error."}";
                    CurrentStepIndex  = 5;
                    UpdateStepStates();
                }
            }
            catch (Exception ex)
            {

                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        CloneStatusMessage = $"Unexpected error: {ex.Message}";
                        IsCloning          = false;
                        CloneSuccessful    = false;
                        CompletionMessage  = $"Clone aborted: {ex.Message}";
                        CurrentStepIndex   = 5;
                        UpdateStepStates();
                    });
                }
                else
                {
                    CloneStatusMessage = $"Unexpected error: {ex.Message}";
                    IsCloning          = false;
                    CloneSuccessful    = false;
                    CompletionMessage  = $"Clone aborted: {ex.Message}";
                    CurrentStepIndex   = 5;
                    UpdateStepStates();
                }
            }
        });
    }

    private void OnFinish()
    {
        ResetWizard();
        _navigationService.NavigateTo<DashboardViewModel>();
    }

    private void OnCancel()
    {
        if (IsCloning || IsBackingUp || IsScanning || IsLoading) return;
        ResetWizard();
        _navigationService.NavigateTo<DashboardViewModel>();
    }

    private void ResetWizard()
    {
        _cloneHasRun        = false;
        _lastScanResult     = null;
        _lastBackupResult   = null;
        _lastClonePreview   = null;
        _selectedRawProfile = null;

        CloneProgress       = 0;
        IsCloning           = false;
        CloneSuccessful     = false;
        BackupComplete      = false;
        BackupStatusMessage = string.Empty;
        WizardErrorMessage  = string.Empty;

        SecurityFindings.Clear();
        PreviewSettingsToCopy.Clear();
        PreviewExtensionsToCopy.Clear();
        PreviewWarnings.Clear();

        CurrentStepIndex = 0;
        OnPropertyChanged(nameof(ShowStartButton));
    }

    private static async Task ReportProgressAsync(
        System.Windows.Threading.Dispatcher? dispatcher,
        Action uiUpdate)
    {
        if (dispatcher != null)
            await dispatcher.InvokeAsync(uiUpdate);
        else
            uiUpdate();

        await Task.Delay(600);
    }

    private static void RunOnUi(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
            dispatcher.Invoke(action);
        else
            action();
    }

    private static string SanitizeFolderName(string name)
        => string.Concat(name.Split(System.IO.Path.GetInvalidFileNameChars()))
                 .Replace(" ", "_")
                 .Trim('_');

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }
}
