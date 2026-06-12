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
    // ── Injected Services ──────────────────────────────────────────────
    private readonly IProfileDiscoveryService _profileDiscoveryService;
    private readonly ISecurityScanner _securityScanner;
    private readonly IProfileAnalyzer _profileAnalyzer;
    private readonly INavigationService _navigationService;
    private readonly ITemplateBuilder _templateBuilder;
    private readonly ITemplateSerializer _templateSerializer;

    // ── Raw data for command use ───────────────────────────────────────
    private List<BraveProfile> _rawProfiles = new();

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

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool HasProfiles => !IsLoading && FilteredProfiles.Count > 0;

    // ── Commands ───────────────────────────────────────────────────────
    public ICommand AnalyzeCommand { get; }
    public ICommand CloneCommand   { get; }
    public ICommand ExportCommand  { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand  { get; }

    // ── Constructor ────────────────────────────────────────────────────
    public ProfilesViewModel(
        IProfileDiscoveryService profileDiscoveryService,
        ISecurityScanner securityScanner,
        IProfileAnalyzer profileAnalyzer,
        INavigationService navigationService,
        ITemplateBuilder templateBuilder,
        ITemplateSerializer templateSerializer)
    {
        _profileDiscoveryService = profileDiscoveryService ?? throw new ArgumentNullException(nameof(profileDiscoveryService));
        _securityScanner = securityScanner ?? throw new ArgumentNullException(nameof(securityScanner));
        _profileAnalyzer = profileAnalyzer ?? throw new ArgumentNullException(nameof(profileAnalyzer));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _templateBuilder = templateBuilder ?? throw new ArgumentNullException(nameof(templateBuilder));
        _templateSerializer = templateSerializer ?? throw new ArgumentNullException(nameof(templateSerializer));

        AnalyzeCommand = new RelayCommand<ProfileCardItem>(OnAnalyze);
        CloneCommand   = new RelayCommand<ProfileCardItem>(OnClone);
        ExportCommand  = new RelayCommand<ProfileCardItem>(OnExport);
        RefreshCommand = new RelayCommand(() => Task.Run(async () => await LoadProfilesAsync()));
        SearchCommand  = new RelayCommand(ApplyFilter);

        // Fire-and-forget initial load on a background thread
        Task.Run(async () => await LoadProfilesAsync());
    }

    // ── Data Loading ──────────────────────────────────────────────────
    private async Task LoadProfilesAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // ── Step A — Discover profiles ────────────────────────────
            var profiles = await _profileDiscoveryService.GetProfilesAsync();
            _rawProfiles = profiles ?? new List<BraveProfile>();

            var cardItems = new List<ProfileCardItem>();

            foreach (var profile in _rawProfiles)
            {
                // ── Step B — Build base ProfileCardItem ───────────────
                var card = new ProfileCardItem
                {
                    ProfileName    = profile.DisplayName,
                    Browser        = "Brave Browser",
                    LastUsed       = GetLastUsedTime(profile.ProfilePath),
                    SourceProfile  = profile
                };

                // ── Step C — Security scan (sequential) ───────────────
                try
                {
                    var scanResult = await _securityScanner.ScanAsync(profile);
                    MapSecurityResult(card, scanResult);
                }
                catch
                {
                    // If a single profile scan fails, skip gracefully
                    card.RiskScore = 0;
                    card.RiskLabel = "Low";
                }

                // ── Step D — Get extension count ──────────────────────
                card.ExtensionCount = await GetExtensionCountAsync(profile);

                cardItems.Add(card);
            }

            // ── Step E — Populate collections on UI thread ────────────
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Profiles.Clear();
                foreach (var card in cardItems)
                {
                    Profiles.Add(card);
                }
                ApplyFilter();
            });
        }
        catch (Exception ex)
        {
            // Discovery failed — show empty state with error message
            ErrorMessage = ex.Message;
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Profiles.Clear();
                FilteredProfiles.Clear();
                OnPropertyChanged(nameof(HasProfiles));
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Extension Count ───────────────────────────────────────────────
    /// <summary>
    /// Gets the extension count by first trying the ProfileAnalyzer,
    /// then falling back to counting subdirectories in the Extensions folder.
    /// </summary>
    private async Task<int> GetExtensionCountAsync(BraveProfile profile)
    {
        // Try the analyzer first
        try
        {
            var analysisResult = await _profileAnalyzer.AnalyzeAsync(profile);
            if (analysisResult.ExtensionCount > 0)
                return analysisResult.ExtensionCount;
        }
        catch
        {
            // Analyzer failed — fall through to filesystem count
        }

        // Fallback: count subdirectories in the Extensions folder
        return CountExtensionsFromDisk(profile.ProfilePath);
    }

    /// <summary>
    /// Counts installed extensions by reading the Extensions folder on disk.
    /// Each subdirectory represents one extension (by extension ID).
    /// </summary>
    private static int CountExtensionsFromDisk(string profilePath)
    {
        try
        {
            var extensionsPath = Path.Combine(profilePath, "Extensions");
            if (Directory.Exists(extensionsPath))
            {
                // Each subfolder is an extension ID; exclude Temp directory if present
                var extensionDirs = Directory.GetDirectories(extensionsPath)
                    .Where(d => !Path.GetFileName(d).Equals("Temp", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                return extensionDirs.Length;
            }
        }
        catch
        {
            // Ignore filesystem errors
        }
        return 0;
    }

    // ── Security Result Mapping ───────────────────────────────────────
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
    }

    // ── Last Used Helper ──────────────────────────────────────────────
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
            // Ignore file system errors
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

    /// <summary>
    /// Analyze: re-scans the profile for security + re-counts extensions,
    /// then updates the card in place.
    /// </summary>
    private void OnAnalyze(ProfileCardItem? p)
    {
        if (p?.SourceProfile == null) return;
        SelectedProfile = p;

        Task.Run(async () =>
        {
            IsLoading = true;
            try
            {
                // Re-run security scan
                var scanResult = await _securityScanner.ScanAsync(p.SourceProfile);

                // Re-count extensions
                int extCount = await GetExtensionCountAsync(p.SourceProfile);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MapSecurityResult(p, scanResult);
                    p.ExtensionCount = extCount;
                    p.LastUsed = GetLastUsedTime(p.SourceProfile.ProfilePath);
                });
            }
            catch
            {
                // Degrade gracefully — keep existing card values
            }
            finally
            {
                IsLoading = false;
            }
        });
    }

    /// <summary>
    /// Clone: navigates to Clone Wizard with selected profile context.
    /// </summary>
    private void OnClone(ProfileCardItem? p)
    {
        if (p == null) return;
        SelectedProfile = p;

        // Navigate to Clone Wizard page
        _navigationService.NavigateTo<CloneWizardViewModel>();
        // TODO: pass selected profile name via ViewModel messaging
        //       so Clone Wizard can pre-select this profile
    }

    /// <summary>
    /// Export: builds an AevorTemplate from the profile's analysis + scan,
    /// then saves it as a JSON file in the user's Documents folder.
    /// </summary>
    private void OnExport(ProfileCardItem? p)
    {
        if (p?.SourceProfile == null) return;
        SelectedProfile = p;

        Task.Run(async () =>
        {
            IsLoading = true;
            try
            {
                // Run analysis and security scan
                var analysisResult = await _profileAnalyzer.AnalyzeAsync(p.SourceProfile);
                var scanResult = await _securityScanner.ScanAsync(p.SourceProfile);

                // Build the template
                var template = _templateBuilder.Build(
                    analysisResult,
                    scanResult,
                    templateName: $"{p.ProfileName} Template",
                    templateDescription: $"Exported from profile \"{p.ProfileName}\"");

                // Save to Documents/Aevor/Templates
                var outputDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Aevor", "Templates");
                Directory.CreateDirectory(outputDir);

                var safeFileName = string.Join("_", p.ProfileName.Split(Path.GetInvalidFileNameChars()));
                var outputPath = Path.Combine(outputDir, $"{safeFileName}_template.json");

                await _templateSerializer.SaveToFileAsync(outputPath, template);

                // Update the UI with success feedback via a recent activity-style notification
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ErrorMessage = null;
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ErrorMessage = $"Export failed: {ex.Message}";
                });
            }
            finally
            {
                IsLoading = false;
            }
        });
    }
}
