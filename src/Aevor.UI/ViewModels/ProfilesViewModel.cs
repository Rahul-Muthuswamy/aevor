using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Aevor.UI.Commands;
using Aevor.UI.Models;

namespace Aevor.UI.ViewModels;

public class ProfilesViewModel : BaseViewModel
{
    // ── Collections ────────────────────────────────────────────────────
    public ObservableCollection<ProfileCardItem> Profiles         { get; } = new();
    public ObservableCollection<ProfileCardItem> FilteredProfiles { get; } = new();

    // ── Properties ─────────────────────────────────────────────────────
    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
                ApplyFilter();
        }
    }

    private ProfileCardItem? _selectedProfile;
    public ProfileCardItem? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
                OnPropertyChanged(nameof(HasProfiles));
        }
    }

    public bool HasProfiles => !IsLoading && FilteredProfiles.Count > 0;

    // ── Commands ───────────────────────────────────────────────────────
    public ICommand AnalyzeCommand { get; }
    public ICommand CloneCommand   { get; }
    public ICommand ExportCommand  { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand  { get; }

    // ── Constructor ────────────────────────────────────────────────────
    public ProfilesViewModel()
    {
        AnalyzeCommand = new RelayCommand<ProfileCardItem>(OnAnalyze);
        CloneCommand   = new RelayCommand<ProfileCardItem>(OnClone);
        ExportCommand  = new RelayCommand<ProfileCardItem>(OnExport);
        RefreshCommand = new RelayCommand(OnRefresh);
        SearchCommand  = new RelayCommand(ApplyFilter);

        LoadSampleData();
    }

    // ── Sample Data ────────────────────────────────────────────────────
    private void LoadSampleData()
    {
        var samples = new[]
        {
            new ProfileCardItem
            {
                ProfileName    = "Personal",
                Browser        = "Brave Browser",
                RiskScore      = 18,
                RiskLabel      = "Low",
                ExtensionCount = 7,
                LastUsed       = "Today"
            },
            new ProfileCardItem
            {
                ProfileName    = "Work",
                Browser        = "Brave Browser",
                RiskScore      = 42,
                RiskLabel      = "Medium",
                ExtensionCount = 12,
                LastUsed       = "2 hrs ago"
            },
            new ProfileCardItem
            {
                ProfileName    = "Research",
                Browser        = "Brave Browser",
                RiskScore      = 75,
                RiskLabel      = "High",
                ExtensionCount = 21,
                LastUsed       = "Yesterday"
            },
            new ProfileCardItem
            {
                ProfileName    = "Bug Bounty",
                Browser        = "Brave Browser",
                RiskScore      = 88,
                RiskLabel      = "High",
                ExtensionCount = 5,
                LastUsed       = "3 days ago"
            },
            new ProfileCardItem
            {
                ProfileName    = "Development",
                Browser        = "Brave Browser",
                RiskScore      = 30,
                RiskLabel      = "Low",
                ExtensionCount = 9,
                LastUsed       = "1 hr ago"
            },
            new ProfileCardItem
            {
                ProfileName    = "Shopping",
                Browser        = "Brave Browser",
                RiskScore      = 55,
                RiskLabel      = "Medium",
                ExtensionCount = 4,
                LastUsed       = "4 days ago"
            },
        };

        foreach (var p in samples)
            Profiles.Add(p);

        ApplyFilter();
    }

    // ── Filter ─────────────────────────────────────────────────────────
    private void ApplyFilter()
    {
        FilteredProfiles.Clear();
        var query = SearchQuery?.Trim() ?? string.Empty;

        foreach (var p in Profiles)
        {
            if (string.IsNullOrEmpty(query) ||
                p.ProfileName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredProfiles.Add(p);
            }
        }

        OnPropertyChanged(nameof(HasProfiles));
    }

    // ── Command Handlers ───────────────────────────────────────────────
    private void OnAnalyze(ProfileCardItem? p) { SelectedProfile = p; }
    private void OnClone(ProfileCardItem? p)   { SelectedProfile = p; }
    private void OnExport(ProfileCardItem? p)  { SelectedProfile = p; }
    private void OnRefresh()
    {
        SearchQuery = string.Empty;
        ApplyFilter();
    }
}
