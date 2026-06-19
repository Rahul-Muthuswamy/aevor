using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Aevor.Application.Interfaces;
using Aevor.UI.Commands;
using Aevor.UI.Services;

namespace Aevor.UI.ViewModels;

public class MainWindowViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private string _currentPageTitle = "Dashboard";

    // ── Version / update ──────────────────────────────────────────────────
    private string _latestVersion  = string.Empty;
    private bool   _updateAvailable;

    private const string GitHubOwner = "Rahul-Muthuswamy";
    private const string GitHubRepo  = "aevor";
    private const string ReleasesUrl =
        $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases";
    private const string ApiUrl =
        $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

    // ── Nav active-state fields ───────────────────────────────────────────
    private bool _isDashboardActive;
    private bool _isProfilesActive;
    private bool _isTemplatesActive;
    private bool _isCloneActive;
    private bool _isBackupsActive;
    private bool _isSecurityActive;
    private bool _isSettingsActive;

    // ── Version properties ────────────────────────────────────────────────

    /// <summary>Assembly version formatted as "v1.0.0".</summary>
    public string CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            if (v is null) return "v0.0.0";
            return $"v{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    public string LatestVersion
    {
        get => _latestVersion;
        private set => SetProperty(ref _latestVersion, value);
    }

    public bool UpdateAvailable
    {
        get => _updateAvailable;
        private set => SetProperty(ref _updateAvailable, value);
    }

    public ICommand OpenReleasesPageCommand { get; }

    // ── Nav active-state properties ───────────────────────────────────────

    public bool IsDashboardActive
    {
        get => _isDashboardActive;
        set => SetProperty(ref _isDashboardActive, value);
    }

    public bool IsProfilesActive
    {
        get => _isProfilesActive;
        set => SetProperty(ref _isProfilesActive, value);
    }

    public bool IsTemplatesActive
    {
        get => _isTemplatesActive;
        set => SetProperty(ref _isTemplatesActive, value);
    }

    public bool IsCloneActive
    {
        get => _isCloneActive;
        set => SetProperty(ref _isCloneActive, value);
    }

    public bool IsBackupsActive
    {
        get => _isBackupsActive;
        set => SetProperty(ref _isBackupsActive, value);
    }

    public bool IsSecurityActive
    {
        get => _isSecurityActive;
        set => SetProperty(ref _isSecurityActive, value);
    }

    public bool IsSettingsActive
    {
        get => _isSettingsActive;
        set => SetProperty(ref _isSettingsActive, value);
    }

    private readonly IToastService _toastService;

    public MainWindowViewModel(INavigationService navigationService, IToastService toastService)
    {
        _navigationService = navigationService;
        _toastService = toastService;
        _navigationService.NavigationChanged += OnNavigationChanged;

        OpenReleasesPageCommand = new RelayCommand(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(ReleasesUrl) { UseShellExecute = true });
            }
            catch { /* ignore — browser may not be available */ }
        });

        NavigateDashboardCommand = new RelayCommand(() => 
        {
            SetAllActiveFalse();
            IsDashboardActive = true;
            _navigationService.NavigateTo<DashboardViewModel>();
        });
        NavigateProfilesCommand = new RelayCommand(() => 
        {
            SetAllActiveFalse();
            IsProfilesActive = true;
            _navigationService.NavigateTo<ProfilesViewModel>();
        });
        NavigateTemplatesCommand = new RelayCommand(() => 
        {
            SetAllActiveFalse();
            IsTemplatesActive = true;
            _navigationService.NavigateTo<TemplatesViewModel>();
        });
        NavigateCloneCommand = new RelayCommand(() => 
        {
            SetAllActiveFalse();
            IsCloneActive = true;
            _navigationService.NavigateTo<CloneWizardViewModel>();
        });
        NavigateBackupsCommand = new RelayCommand(() => 
        {
            SetAllActiveFalse();
            IsBackupsActive = true;
            _navigationService.NavigateTo<BackupsViewModel>();
        });
        NavigateSecurityCommand = new RelayCommand(() => 
        {
            SetAllActiveFalse();
            IsSecurityActive = true;
            _navigationService.NavigateTo<SecurityViewModel>();
        });
        NavigateSettingsCommand = new RelayCommand(() => 
        {
            SetAllActiveFalse();
            IsSettingsActive = true;
            _navigationService.NavigateTo<SettingsViewModel>();
        });

        OnNavigationChanged();

        // Kick off silent background update check
        Task.Run(CheckForUpdatesAsync);
    }

    public BaseViewModel? CurrentView => _navigationService.CurrentView;

    public string CurrentPageTitle
    {
        get => _currentPageTitle;
        set => SetProperty(ref _currentPageTitle, value);
    }

    public ICommand NavigateDashboardCommand { get; }
    public ICommand NavigateProfilesCommand  { get; }
    public ICommand NavigateTemplatesCommand { get; }
    public ICommand NavigateCloneCommand     { get; }
    public ICommand NavigateBackupsCommand   { get; }
    public ICommand NavigateSecurityCommand  { get; }
    public ICommand NavigateSettingsCommand  { get; }

    // ── Update check ──────────────────────────────────────────────────────

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Aevor-App", CurrentVersion.TrimStart('v')));
            client.Timeout = TimeSpan.FromSeconds(10);

            var json = await client.GetStringAsync(ApiUrl).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("tag_name", out var tagElement))
                return;

            var tag = tagElement.GetString();
            if (string.IsNullOrWhiteSpace(tag))
                return;

            // Normalise both to "vX.Y.Z" for comparison
            var remote  = tag.StartsWith('v') ? tag : $"v{tag}";
            var current = CurrentVersion;

            if (!string.Equals(remote, current, StringComparison.OrdinalIgnoreCase))
            {
                // Marshal property updates back to the UI thread
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    LatestVersion   = remote;
                    UpdateAvailable = true;
                });
            }
        }
        catch
        {
            // Fail silently — no internet or API error must never crash the app
        }
    }

    private void SetAllActiveFalse()
    {
        IsDashboardActive = false;
        IsProfilesActive = false;
        IsTemplatesActive = false;
        IsCloneActive = false;
        IsBackupsActive = false;
        IsSecurityActive = false;
        IsSettingsActive = false;
    }

    private void OnNavigationChanged()
    {
        OnPropertyChanged(nameof(CurrentView));
        
        IsDashboardActive = CurrentView is DashboardViewModel;
        IsProfilesActive = CurrentView is ProfilesViewModel;
        IsTemplatesActive = CurrentView is TemplatesViewModel;
        IsCloneActive = CurrentView is CloneWizardViewModel;
        IsBackupsActive = CurrentView is BackupsViewModel;
        IsSecurityActive = CurrentView is SecurityViewModel;
        IsSettingsActive = CurrentView is SettingsViewModel;

        CurrentPageTitle = CurrentView switch
        {
            DashboardViewModel => "Dashboard",
            ProfilesViewModel => "Profiles",
            TemplatesViewModel => "Templates",
            CloneWizardViewModel => "Clone Wizard",
            BackupsViewModel => "Backups",
            SecurityViewModel => "Security",
            SettingsViewModel => "Settings",
            _ => "Aevor"
        };
    }
}
