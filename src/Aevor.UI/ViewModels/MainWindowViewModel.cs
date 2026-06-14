using System;
using System.Windows.Input;
using Aevor.UI.Commands;
using Aevor.UI.Services;

namespace Aevor.UI.ViewModels;

public class MainWindowViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private string _currentPageTitle = "Dashboard";

    private bool _isDashboardActive;
    private bool _isProfilesActive;
    private bool _isTemplatesActive;
    private bool _isCloneActive;
    private bool _isBackupsActive;
    private bool _isSecurityActive;
    private bool _isSettingsActive;

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

    public MainWindowViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _navigationService.NavigationChanged += OnNavigationChanged;

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
    }

    public BaseViewModel? CurrentView => _navigationService.CurrentView;

    public string CurrentPageTitle
    {
        get => _currentPageTitle;
        set => SetProperty(ref _currentPageTitle, value);
    }

    public ICommand NavigateDashboardCommand { get; }
    public ICommand NavigateProfilesCommand { get; }
    public ICommand NavigateTemplatesCommand { get; }
    public ICommand NavigateCloneCommand { get; }
    public ICommand NavigateBackupsCommand { get; }
    public ICommand NavigateSecurityCommand { get; }
    public ICommand NavigateSettingsCommand { get; }

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
