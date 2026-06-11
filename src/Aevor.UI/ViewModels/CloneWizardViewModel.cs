using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using Aevor.UI.Commands;
using Aevor.UI.Models;

namespace Aevor.UI.ViewModels;

public class CloneWizardViewModel : BaseViewModel
{
    // ════════════════════════════════════════════════════════════════════
    // Step Indicator
    // ════════════════════════════════════════════════════════════════════
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

    public bool CanGoBack  => CurrentStepIndex > 0 && CurrentStepIndex < 5;
    public bool CanGoNext  => CurrentStepIndex < 5;
    public bool IsLastStep => CurrentStepIndex == 5;

    // ════════════════════════════════════════════════════════════════════
    // Step 1 — Source Profile
    // ════════════════════════════════════════════════════════════════════
    public ObservableCollection<string> AvailableProfiles { get; } = new();

    private string _selectedSourceProfile = string.Empty;
    public string SelectedSourceProfile
    {
        get => _selectedSourceProfile;
        set => SetProperty(ref _selectedSourceProfile, value);
    }

    // ════════════════════════════════════════════════════════════════════
    // Step 2 — Security Review
    // ════════════════════════════════════════════════════════════════════
    public ObservableCollection<SecurityFindingItem> SecurityFindings { get; } = new();

    public bool HasWarnings => SecurityFindings.Count > 0 &&
                               System.Linq.Enumerable.Any(SecurityFindings, f => f.Severity == "warning");

    private string _securitySummary = string.Empty;
    public string SecuritySummary
    {
        get => _securitySummary;
        set => SetProperty(ref _securitySummary, value);
    }

    public Brush SecurityBannerBackground =>
        HasWarnings
            ? new SolidColorBrush(Color.FromRgb(255, 251, 235))  // #FFFBEB
            : new SolidColorBrush(Color.FromRgb(240, 253, 244)); // #F0FDF4

    // ════════════════════════════════════════════════════════════════════
    // Step 3 — Backup
    // ════════════════════════════════════════════════════════════════════
    private bool _createBackupBeforeClone = true;
    public bool CreateBackupBeforeClone
    {
        get => _createBackupBeforeClone;
        set => SetProperty(ref _createBackupBeforeClone, value);
    }

    private string _backupLocation = @"C:\Users\YourName\AevorBackups\";
    public string BackupLocation
    {
        get => _backupLocation;
        set => SetProperty(ref _backupLocation, value);
    }

    // ════════════════════════════════════════════════════════════════════
    // Step 4 — Clone Configuration
    // ════════════════════════════════════════════════════════════════════
    private string _newProfileName = string.Empty;
    public string NewProfileName
    {
        get => _newProfileName;
        set => SetProperty(ref _newProfileName, value);
    }

    private bool _copyExtensions   = true;
    private bool _copyBookmarks    = true;
    private bool _copySettings     = true;
    private bool _copyThemes       = true;
    private bool _copySearchEngines = true;

    public bool CopyExtensions    { get => _copyExtensions;    set => SetProperty(ref _copyExtensions,    value); }
    public bool CopyBookmarks     { get => _copyBookmarks;     set => SetProperty(ref _copyBookmarks,     value); }
    public bool CopySettings      { get => _copySettings;      set => SetProperty(ref _copySettings,      value); }
    public bool CopyThemes        { get => _copyThemes;        set => SetProperty(ref _copyThemes,        value); }
    public bool CopySearchEngines { get => _copySearchEngines; set => SetProperty(ref _copySearchEngines, value); }

    // ════════════════════════════════════════════════════════════════════
    // Step 5 — Execute
    // ════════════════════════════════════════════════════════════════════
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
                OnPropertyChanged(nameof(ShowStartButton));
        }
    }

    public bool ShowStartButton => !IsCloning && CloneProgress == 0;

    // ════════════════════════════════════════════════════════════════════
    // Step 6 — Completion
    // ════════════════════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════════════════════
    // Commands
    // ════════════════════════════════════════════════════════════════════
    public ICommand NextStepCommand           { get; }
    public ICommand PreviousStepCommand       { get; }
    public ICommand StartCloneCommand         { get; }
    public ICommand FinishCommand             { get; }
    public ICommand CancelCommand             { get; }
    public ICommand SelectSourceProfileCommand { get; }

    // ════════════════════════════════════════════════════════════════════
    // Constructor
    // ════════════════════════════════════════════════════════════════════
    public CloneWizardViewModel()
    {
        NextStepCommand            = new RelayCommand(OnNextStep,     () => CanGoNext && !IsLastStep);
        PreviousStepCommand        = new RelayCommand(OnPreviousStep, () => CanGoBack);
        StartCloneCommand          = new RelayCommand(OnStartClone,   () => !IsCloning);
        FinishCommand              = new RelayCommand(OnFinish);
        CancelCommand              = new RelayCommand(OnCancel);
        SelectSourceProfileCommand = new RelayCommand<string>(OnSelectSourceProfile);

        InitialiseSteps();
        LoadSampleData();
    }

    private void OnSelectSourceProfile(string? profile)
    {
        if (profile != null)
        {
            SelectedSourceProfile = profile;
        }
    }

    // ── Step Setup ─────────────────────────────────────────────────────
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
        // Force re-evaluation of each step (INotifyPropertyChanged not on CloneStep)
        var tmp = new CloneStep[Steps.Count];
        Steps.CopyTo(tmp, 0);
        Steps.Clear();
        foreach (var s in tmp) Steps.Add(s);
    }

    // ── Sample Data ────────────────────────────────────────────────────
    private void LoadSampleData()
    {
        AvailableProfiles.Add("Personal");
        AvailableProfiles.Add("Work");
        AvailableProfiles.Add("Research");
        AvailableProfiles.Add("Bug Bounty");
        AvailableProfiles.Add("Development");
        SelectedSourceProfile = "Personal";

        SecurityFindings.Add(new SecurityFindingItem
        {
            Title    = "Saved Passwords Detected",
            Detail   = "23 login credentials stored in the browser — will NOT be copied",
            Severity = "warning"
        });
        SecurityFindings.Add(new SecurityFindingItem
        {
            Title    = "Extensions Present",
            Detail   = "7 extensions found — will be copied based on your configuration",
            Severity = "info"
        });
        SecurityFindings.Add(new SecurityFindingItem
        {
            Title    = "Browsing History",
            Detail   = "History will be excluded from the cloned profile",
            Severity = "excluded"
        });
        SecurityFindings.Add(new SecurityFindingItem
        {
            Title    = "Cookies & Sessions",
            Detail   = "Active sessions detected — will NOT be transferred to clone",
            Severity = "warning"
        });
        SecurityFindings.Add(new SecurityFindingItem
        {
            Title    = "Custom Search Engines",
            Detail   = "3 custom engines configured — can be optionally copied",
            Severity = "info"
        });

        SecuritySummary = "2 warnings found. Passwords and active sessions will not be cloned.";
        NewProfileName  = "Personal — Copy";
        CompletionMessage = "Your new profile is ready and can be launched from Brave Browser.";
    }

    // ── Command Handlers ───────────────────────────────────────────────
    private void OnNextStep()
    {
        if (CurrentStepIndex < 5)
            CurrentStepIndex++;
    }

    private void OnPreviousStep()
    {
        if (CurrentStepIndex > 0)
            CurrentStepIndex--;
    }

    private async void OnStartClone()
    {
        IsCloning = true;
        CloneProgress = 0;

        var stages = new[]
        {
            (10.0,  "Verifying source profile…"),
            (25.0,  "Creating backup snapshot…"),
            (45.0,  "Copying profile data…"),
            (65.0,  "Applying extensions…"),
            (80.0,  "Copying bookmarks and settings…"),
            (95.0,  "Finalising clone…"),
            (100.0, "Clone complete!"),
        };

        foreach (var (progress, message) in stages)
        {
            await System.Threading.Tasks.Task.Delay(600);
            CloneProgress     = progress;
            CloneStatusMessage = message;
            OnPropertyChanged(nameof(ShowStartButton));
        }

        await System.Threading.Tasks.Task.Delay(400);
        IsCloning       = false;
        CloneSuccessful = true;
        CurrentStepIndex = 5;
    }

    private void OnFinish()  { CurrentStepIndex = 0; CloneProgress = 0; IsCloning = false; }
    private void OnCancel()  { CurrentStepIndex = 0; CloneProgress = 0; IsCloning = false; }
}
