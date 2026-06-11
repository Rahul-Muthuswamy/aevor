using System;
using System.Windows.Input;
using Aevor.UI.Commands;
using Aevor.UI.Services;

namespace Aevor.UI.ViewModels;

public class MainWindowViewModel : BaseViewModel
{
    private readonly INavigationService _navigationService;
    private string _currentPageTitle = "Dashboard";

    public MainWindowViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _navigationService.NavigationChanged += OnNavigationChanged;

        NavigateDashboardCommand = new RelayCommand(() => _navigationService.NavigateTo<DashboardViewModel>());
        NavigateProfilesCommand = new RelayCommand(() => _navigationService.NavigateTo<ProfilesViewModel>());
        NavigateTemplatesCommand = new RelayCommand(() => _navigationService.NavigateTo<TemplatesViewModel>());
        NavigateCloneCommand = new RelayCommand(() => _navigationService.NavigateTo<CloneWizardViewModel>());
        NavigateBackupsCommand = new RelayCommand(() => _navigationService.NavigateTo<BackupsViewModel>());
        NavigateSecurityCommand = new RelayCommand(() => _navigationService.NavigateTo<SecurityViewModel>());
        NavigateSettingsCommand = new RelayCommand(() => _navigationService.NavigateTo<SettingsViewModel>());
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

    // Active tracking properties
    public bool IsDashboardActive => CurrentView is DashboardViewModel;
    public bool IsProfilesActive => CurrentView is ProfilesViewModel;
    public bool IsTemplatesActive => CurrentView is TemplatesViewModel;
    public bool IsCloneActive => CurrentView is CloneWizardViewModel;
    public bool IsBackupsActive => CurrentView is BackupsViewModel;
    public bool IsSecurityActive => CurrentView is SecurityViewModel;
    public bool IsSettingsActive => CurrentView is SettingsViewModel;

    private void OnNavigationChanged()
    {
        OnPropertyChanged(nameof(CurrentView));
        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsProfilesActive));
        OnPropertyChanged(nameof(IsTemplatesActive));
        OnPropertyChanged(nameof(IsCloneActive));
        OnPropertyChanged(nameof(IsBackupsActive));
        OnPropertyChanged(nameof(IsSecurityActive));
        OnPropertyChanged(nameof(IsSettingsActive));

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
