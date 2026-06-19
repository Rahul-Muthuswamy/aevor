using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Aevor.Application.Interfaces;
using Aevor.Core.Models;
using Aevor.UI.Commands;
using Aevor.UI.Models;
using Aevor.UI.Services;

namespace Aevor.UI.ViewModels;

public class ProfilesViewModel : BaseViewModel
{

    private readonly IProfileDiscoveryService _profileDiscoveryService;
    private readonly ISecurityScanner _securityScanner;
    private readonly IProfileAnalyzer _profileAnalyzer;
    private readonly INavigationService _navigationService;
    private readonly ITemplateBuilder _templateBuilder;
    private readonly ITemplateSerializer _templateSerializer;
    private readonly IToastService _toastService;

    private List<BraveProfile> _rawProfiles = new();

    public ObservableCollection<ProfileCardItem> Profiles         { get; } = new();
    public ObservableCollection<ProfileCardItem> FilteredProfiles { get; } = new();

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

    private ProfileCardItem? _selectedProfileDetail;
    public ProfileCardItem? SelectedProfileDetail
    {
        get => _selectedProfileDetail;
        set => SetProperty(ref _selectedProfileDetail, value);
    }

    private bool _isDetailPanelOpen;
    public bool IsDetailPanelOpen
    {
        get => _isDetailPanelOpen;
        set => SetProperty(ref _isDetailPanelOpen, value);
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

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool HasProfiles => !IsLoading && FilteredProfiles.Count > 0;

    public ICommand CloneCommand          { get; }
    public ICommand CreateTemplateCommand { get; }
    public ICommand RefreshCommand        { get; }
    public ICommand SearchCommand          { get; }
    public ICommand OpenDetailCommand     { get; }
    public ICommand CloseDetailCommand    { get; }

    public ProfilesViewModel(
        IProfileDiscoveryService profileDiscoveryService,
        ISecurityScanner securityScanner,
        IProfileAnalyzer profileAnalyzer,
        INavigationService navigationService,
        ITemplateBuilder templateBuilder,
        ITemplateSerializer templateSerializer,
        IToastService toastService)
    {
        _profileDiscoveryService = profileDiscoveryService ?? throw new ArgumentNullException(nameof(profileDiscoveryService));
        _securityScanner = securityScanner ?? throw new ArgumentNullException(nameof(securityScanner));
        _profileAnalyzer = profileAnalyzer ?? throw new ArgumentNullException(nameof(profileAnalyzer));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _templateBuilder = templateBuilder ?? throw new ArgumentNullException(nameof(templateBuilder));
        _templateSerializer = templateSerializer ?? throw new ArgumentNullException(nameof(templateSerializer));
        _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));

        CloneCommand          = new RelayCommand<ProfileCardItem>(OnClone);
        CreateTemplateCommand = new RelayCommand<ProfileCardItem>(OnCreateTemplate);
        RefreshCommand        = new RelayCommand(() => Task.Run(async () => await LoadProfilesAsync()));
        SearchCommand          = new RelayCommand(ApplyFilter);
        OpenDetailCommand     = new RelayCommand<ProfileCardItem>(OnOpenDetail);
        CloseDetailCommand    = new RelayCommand(OnCloseDetail);

        Task.Run(async () => await LoadProfilesAsync());
    }

    private async Task LoadProfilesAsync()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;

        if (dispatcher != null)
        {
            await dispatcher.InvokeAsync(() =>
            {
                IsLoading = true;
                ErrorMessage = null;
            });
        }
        else
        {
            IsLoading = true;
            ErrorMessage = null;
        }

        try
        {

            var profiles = await _profileDiscoveryService.GetProfilesAsync();
            var rawProfiles = profiles ?? new List<BraveProfile>();

            var cardItems = new List<ProfileCardItem>();

            foreach (var profile in rawProfiles)
            {

                var card = new ProfileCardItem
                {
                    ProfileName    = profile.DisplayName,
                    Browser        = "Brave Browser",
                    LastUsed       = GetLastUsedTime(profile.ProfilePath),
                    SourceProfile  = profile
                };

                try
                {
                    var scanResult = await _securityScanner.ScanAsync(profile);
                    MapSecurityResult(card, scanResult);
                }
                catch
                {

                    card.RiskScore = 0;
                    card.RiskLabel = "Low";
                }

                var (extCount, extList) = await GetExtensionsAsync(profile);
                card.ExtensionCount = extCount;
                card.Extensions = extList;

                cardItems.Add(card);
            }

            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    _rawProfiles = rawProfiles;
                    Profiles.Clear();
                    foreach (var card in cardItems)
                    {
                        Profiles.Add(card);
                    }
                    ApplyFilter();
                });
            }
            else
            {
                _rawProfiles = rawProfiles;
                Profiles.Clear();
                foreach (var card in cardItems)
                {
                    Profiles.Add(card);
                }
                ApplyFilter();
            }
        }
        catch (Exception ex)
        {

            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    ErrorMessage = ex.Message;
                    Profiles.Clear();
                    FilteredProfiles.Clear();
                    OnPropertyChanged(nameof(HasProfiles));
                });
            }
            else
            {
                ErrorMessage = ex.Message;
                Profiles.Clear();
                FilteredProfiles.Clear();
                OnPropertyChanged(nameof(HasProfiles));
            }
        }
        finally
        {
            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() => IsLoading = false);
            }
            else
            {
                IsLoading = false;
            }
        }
    }

    private async Task<(int Count, List<string> EnabledExtensions)> GetExtensionsAsync(BraveProfile profile)
    {
        try
        {
            var analysisResult = await _profileAnalyzer.AnalyzeAsync(profile);
            var enabledList = analysisResult.InstalledExtensions?
                .Where(e => e.IsEnabled)
                .Select(e => e.Name)
                .ToList() ?? new List<string>();
            return (analysisResult.ExtensionCount, enabledList);
        }
        catch
        {
            int diskCount = CountExtensionsFromDisk(profile.ProfilePath);
            return (diskCount, new List<string>());
        }
    }

    private async Task<int> GetExtensionCountAsync(BraveProfile profile)
    {

        try
        {
            var analysisResult = await _profileAnalyzer.AnalyzeAsync(profile);
            if (analysisResult.ExtensionCount > 0)
                return analysisResult.ExtensionCount;
        }
        catch
        {

        }

        return CountExtensionsFromDisk(profile.ProfilePath);
    }

    private static int CountExtensionsFromDisk(string profilePath)
    {
        try
        {
            var extensionsPath = Path.Combine(profilePath, "Extensions");
            if (Directory.Exists(extensionsPath))
            {

                var extensionDirs = Directory.GetDirectories(extensionsPath)
                    .Where(d => !Path.GetFileName(d).Equals("Temp", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                return extensionDirs.Length;
            }
        }
        catch
        {

        }
        return 0;
    }

    private static void MapSecurityResult(ProfileCardItem card, SecurityScanResult result)
    {
        card.RiskScore = result.RiskScore;

        bool hasCritical = result.Findings.Any(f =>
            f.Severity == SecuritySeverity.Critical || f.Severity == SecuritySeverity.High);
        bool hasWarnings = result.Findings.Any(f =>
            f.Severity == SecuritySeverity.Medium || f.Severity == SecuritySeverity.Low);

        if (hasCritical)
        {
            card.RiskLabel = "High";
        }
        else if (hasWarnings || result.Findings.Count > 0)
        {
            card.RiskLabel = "Medium";
        }
        else
        {
            card.RiskLabel = "Low";
        }

        card.SecurityFindings.Clear();
        foreach (var finding in result.Findings)
        {
            card.SecurityFindings.Add(new SecurityFindingItem
            {
                Title = finding.Name,
                Detail = finding.Description,
                Severity = finding.Severity switch
                {
                    SecuritySeverity.Critical => "warning",
                    SecuritySeverity.High     => "warning",
                    SecuritySeverity.Medium   => "warning",
                    _ => "info"
                }
            });
        }
    }

    private static string GetLastUsedTime(string profilePath)
    {
        try
        {
            if (Directory.Exists(profilePath))
            {
                var lastWrite = Directory.GetLastWriteTime(profilePath);
                return FormatRelativeTime(lastWrite);
            }
        }
        catch
        {

        }
        return "Unknown";
    }

    private static string FormatRelativeTime(DateTime timestamp)
    {
        var diff = DateTime.Now - timestamp;

        if (diff.TotalSeconds < 60)
            return "Just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} min ago";
        if (diff.TotalHours < 24)
            return diff.TotalHours < 2 ? "1 hr ago" : $"{(int)diff.TotalHours} hrs ago";
        if (diff.TotalDays < 1.5)
            return "Yesterday";
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays} days ago";
        if (diff.TotalDays < 14)
            return "1 week ago";
        if (diff.TotalDays < 30)
            return $"{(int)(diff.TotalDays / 7)} weeks ago";

        return timestamp.ToString("MMM d, yyyy");
    }

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

    private void OnOpenDetail(ProfileCardItem? p)
    {
        if (p == null) return;
        SelectedProfileDetail = p;
        IsDetailPanelOpen = true;
    }

    private void OnCloseDetail()
    {
        IsDetailPanelOpen = false;
        SelectedProfileDetail = null;
    }

    private void OnClone(ProfileCardItem? p)
    {
        if (p == null) return;
        SelectedProfile = p;

        _navigationService.NavigateTo<CloneWizardViewModel>(vm =>
        {
            vm.PreselectSourceProfileAndAdvance(p.ProfileName);
        });
    }

    private void OnCreateTemplate(ProfileCardItem? p)
    {
        if (p?.SourceProfile == null) return;
        SelectedProfile = p;

        Task.Run(async () =>
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    IsLoading = true;
                    ErrorMessage = null;
                });
            }
            else
            {
                IsLoading = true;
                ErrorMessage = null;
            }

            try
            {

                var analysisResult = await _profileAnalyzer.AnalyzeAsync(p.SourceProfile);
                var scanResult = await _securityScanner.ScanAsync(p.SourceProfile);

                var template = _templateBuilder.Build(
                    analysisResult,
                    scanResult,
                    templateName: $"{p.ProfileName} Template",
                    templateDescription: $"Exported from profile \"{p.ProfileName}\"");

                var outputDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Aevor", "Templates");
                Directory.CreateDirectory(outputDir);

                var safeFileName = string.Join("_", p.ProfileName.Split(Path.GetInvalidFileNameChars()));
                var outputPath = Path.Combine(outputDir, $"{safeFileName}_template.json");

                await _templateSerializer.SaveToFileAsync(outputPath, template);
                _toastService.Show("Template created successfully.", ToastType.Success);

                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        ErrorMessage = null;
                        _navigationService.NavigateTo<TemplatesViewModel>();
                    });
                }
                else
                {
                    ErrorMessage = null;
                    _navigationService.NavigateTo<TemplatesViewModel>();
                }
            }
            catch (Exception ex)
            {
                _toastService.Show($"Create Template failed: {ex.Message}", ToastType.Error);
            }
            finally
            {
                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() => IsLoading = false);
                }
                else
                {
                    IsLoading = false;
                }
            }
        });
    }
}
