using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Aevor.Application.Interfaces;
using Aevor.Core.Models;
using Aevor.UI.Commands;
using Aevor.UI.Models;
using Aevor.UI.Services;
using Microsoft.Win32;

namespace Aevor.UI.ViewModels;

public class TemplatesViewModel : BaseViewModel
{
    // ── Injected Services ──────────────────────────────────────────────
    private readonly ITemplateSerializer _templateSerializer;
    private readonly ITemplateApplier _templateApplier;
    private readonly IProfileDiscoveryService _profileDiscoveryService;
    private readonly INavigationService _navigationService;

    // ── Template Storage ───────────────────────────────────────────────
    private readonly string _templatesDirectory;

    // ── Collections ────────────────────────────────────────────────────
    public ObservableCollection<TemplateCardItem> Templates         { get; } = new();
    public ObservableCollection<TemplateCardItem> FilteredTemplates { get; } = new();

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

    private TemplateCardItem? _selectedTemplate;
    public TemplateCardItem? SelectedTemplate
    {
        get => _selectedTemplate;
        set => SetProperty(ref _selectedTemplate, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
                OnPropertyChanged(nameof(HasTemplates));
        }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool HasTemplates => !IsLoading && FilteredTemplates.Count > 0;

    // ── Commands ───────────────────────────────────────────────────────
    public ICommand ApplyCommand          { get; }
    public ICommand ExportCommand         { get; }
    public ICommand DeleteCommand         { get; }
    public ICommand ImportTemplateCommand { get; }
    public ICommand RefreshCommand        { get; }

    // ── Constructor ────────────────────────────────────────────────────
    public TemplatesViewModel(
        ITemplateSerializer templateSerializer,
        ITemplateApplier templateApplier,
        IProfileDiscoveryService profileDiscoveryService,
        INavigationService navigationService)
    {
        _templateSerializer = templateSerializer ?? throw new ArgumentNullException(nameof(templateSerializer));
        _templateApplier = templateApplier ?? throw new ArgumentNullException(nameof(templateApplier));
        _profileDiscoveryService = profileDiscoveryService ?? throw new ArgumentNullException(nameof(profileDiscoveryService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

        // Set up templates directory
        _templatesDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Aevor", "Templates");
        Directory.CreateDirectory(_templatesDirectory);

        ApplyCommand          = new RelayCommand<TemplateCardItem>(OnApply);
        ExportCommand         = new RelayCommand<TemplateCardItem>(OnExport);
        DeleteCommand         = new RelayCommand<TemplateCardItem>(OnDelete);
        ImportTemplateCommand = new RelayCommand(OnImport);
        RefreshCommand        = new RelayCommand(() => Task.Run(async () => await LoadTemplatesAsync()));

        // Fire-and-forget initial load on a background thread
        Task.Run(async () => await LoadTemplatesAsync());
    }

    // ── Data Loading ──────────────────────────────────────────────────
    private async Task LoadTemplatesAsync()
    {
        IsLoading = true;

        try
        {
            // ── Step A — Scan templates directory ─────────────────────
            Directory.CreateDirectory(_templatesDirectory);
            var jsonFiles = Directory.GetFiles(_templatesDirectory, "*.json");

            if (jsonFiles.Length == 0)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Templates.Clear();
                    FilteredTemplates.Clear();
                    OnPropertyChanged(nameof(HasTemplates));
                });
                return;
            }

            // ── Step B — Load each template file ──────────────────────
            var cardItems = new List<TemplateCardItem>();

            foreach (var filePath in jsonFiles)
            {
                try
                {
                    var template = await _templateSerializer.LoadFromFileAsync(filePath);
                    var card = MapTemplateToCard(template, filePath);
                    cardItems.Add(card);
                }
                catch (Exception ex)
                {
                    // Skip corrupted/invalid files silently
                    Debug.WriteLine($"[TemplatesVM] Failed to load template '{filePath}': {ex.Message}");
                }
            }

            // ── Populate collections on UI thread ─────────────────────
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Templates.Clear();
                foreach (var card in cardItems)
                {
                    Templates.Add(card);
                }
                ApplyFilter();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TemplatesVM] LoadTemplatesAsync failed: {ex.Message}");
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Templates.Clear();
                FilteredTemplates.Clear();
                OnPropertyChanged(nameof(HasTemplates));
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Template → Card Mapping ───────────────────────────────────────
    private static TemplateCardItem MapTemplateToCard(AevorTemplate template, string filePath)
    {
        var name = template.Metadata?.Name ?? Path.GetFileNameWithoutExtension(filePath);
        var (tagLabel, bgHex, fgHex) = DeriveTag(name);

        var bgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex));
        bgBrush.Freeze();
        var fgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgHex));
        fgBrush.Freeze();

        return new TemplateCardItem
        {
            TemplateName       = name,
            Browser            = template.Metadata?.SourceBrowser ?? "Brave Browser",
            Version            = template.Metadata?.TemplateVersion?.ToString() ?? "1.0.0",
            Description        = template.Metadata?.Description ?? "No description",
            CreatedDate        = FormatCreatedDate(filePath),
            ExtensionCount     = template.Extensions?.Count ?? 0,
            TagLabel           = tagLabel,
            TagBackgroundBrush = bgBrush,
            TagTextBrush       = fgBrush,
            SourceTemplate     = template,
            FilePath           = filePath
        };
    }

    private static string FormatCreatedDate(string filePath)
    {
        try
        {
            var created = File.GetCreationTime(filePath);
            return created.ToString("MMM d, yyyy");
        }
        catch
        {
            return "Unknown";
        }
    }

    // ── Step C — Tag Derivation ───────────────────────────────────────
    private static (string Tag, string BgHex, string FgHex) DeriveTag(string name)
    {
        var lower = name.ToLowerInvariant();

        if (lower.Contains("work") || lower.Contains("office"))
            return ("Work", "#DBEAFE", "#1E40AF");

        if (lower.Contains("security") || lower.Contains("bounty"))
            return ("Security", "#FEE2E2", "#991B1B");

        if (lower.Contains("research"))
            return ("Research", "#E0E7FF", "#3730A3");

        if (lower.Contains("dev") || lower.Contains("code") || lower.Contains("program"))
            return ("Dev", "#D1FAE5", "#065F46");

        if (lower.Contains("student") || lower.Contains("lab") || lower.Contains("study"))
            return ("Study", "#FEF3C7", "#92400E");

        if (lower.Contains("privacy") || lower.Contains("private"))
            return ("Privacy", "#EDE9FE", "#5B21B6");

        if (lower.Contains("personal"))
            return ("Personal", "#FCE7F3", "#9D174D");

        return ("Custom", "#F3F4F6", "#374151");
    }

    // ── Filter ─────────────────────────────────────────────────────────
    private void ApplyFilter()
    {
        FilteredTemplates.Clear();
        var query = SearchQuery?.Trim() ?? string.Empty;

        foreach (var t in Templates)
        {
            if (string.IsNullOrEmpty(query) ||
                t.TemplateName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredTemplates.Add(t);
            }
        }

        OnPropertyChanged(nameof(HasTemplates));
    }

    // ── Status Message Helper ─────────────────────────────────────────
    private void SetStatusMessage(string message)
    {
        StatusMessage = message;
        // Auto-clear after 4 seconds
        Task.Delay(4000).ContinueWith(_ =>
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                StatusMessage = string.Empty));
    }

    // ── Command Handlers ───────────────────────────────────────────────

    /// <summary>
    /// Apply: validates and applies a template to the first available profile.
    /// </summary>
    private void OnApply(TemplateCardItem? t)
    {
        if (t?.SourceTemplate == null) return;
        SelectedTemplate = t;

        Task.Run(async () =>
        {
            IsLoading = true;
            try
            {
                // Step 1: Get available profiles
                var profiles = await _profileDiscoveryService.GetProfilesAsync();
                if (profiles == null || profiles.Count == 0)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        SetStatusMessage("No profiles found. Please create a Brave profile first."));
                    return;
                }

                // Step 2: Select target profile
                // TODO: show profile picker dialog
                var targetProfile = profiles[0];

                // Step 3: Validate before applying
                var validation = await _templateApplier.ValidateApplicationAsync(
                    t.SourceTemplate, targetProfile);

                if (!validation.IsValid)
                {
                    var errorDetail = validation.Errors.Count > 0
                        ? string.Join("; ", validation.Errors.Take(3))
                        : "Template is not compatible with target profile.";

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        SetStatusMessage($"Validation failed: {errorDetail}"));
                    return;
                }

                // Step 4: Apply the template
                var result = await _templateApplier.ApplyTemplateAsync(
                    t.SourceTemplate, targetProfile);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (result.IsSuccess)
                    {
                        SetStatusMessage($"Template applied successfully to \"{targetProfile.DisplayName}\"");
                    }
                    else
                    {
                        SetStatusMessage($"Apply failed: {result.ErrorMessage ?? "Unknown error"}");
                    }
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    SetStatusMessage($"Apply failed: {ex.Message}"));
            }
            finally
            {
                IsLoading = false;
            }
        });
    }

    /// <summary>
    /// Export: saves the template to a user-chosen location via SaveFileDialog.
    /// </summary>
    private void OnExport(TemplateCardItem? t)
    {
        if (t?.SourceTemplate == null) return;
        SelectedTemplate = t;

        // SaveFileDialog must run on the UI thread
        var safeFileName = string.Join("_", t.TemplateName.Split(Path.GetInvalidFileNameChars()));
        var dialog = new SaveFileDialog
        {
            FileName         = safeFileName + ".json",
            Filter           = "Aevor Template (*.json)|*.json",
            InitialDirectory = _templatesDirectory,
            Title            = "Export Template"
        };

        if (dialog.ShowDialog() == true)
        {
            var selectedPath = dialog.FileName;
            Task.Run(async () =>
            {
                try
                {
                    await _templateSerializer.SaveToFileAsync(selectedPath, t.SourceTemplate);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        SetStatusMessage($"Template exported to {Path.GetFileName(selectedPath)}"));
                }
                catch (Exception ex)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        SetStatusMessage($"Export failed: {ex.Message}"));
                }
            });
        }
    }

    /// <summary>
    /// Delete: removes the template file from disk and the card from collections.
    /// </summary>
    private void OnDelete(TemplateCardItem? t)
    {
        if (t == null) return;

        try
        {
            // Delete the file from disk if we know its path
            if (!string.IsNullOrEmpty(t.FilePath) && File.Exists(t.FilePath))
            {
                File.Delete(t.FilePath);
            }

            // Remove from collections
            Templates.Remove(t);
            FilteredTemplates.Remove(t);
            OnPropertyChanged(nameof(HasTemplates));

            SetStatusMessage($"\"{t.TemplateName}\" deleted");
        }
        catch (Exception ex)
        {
            SetStatusMessage($"Delete failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Import: opens an OpenFileDialog, loads the template, copies it to
    /// the templates directory, and adds it to the collections.
    /// </summary>
    private void OnImport()
    {
        var dialog = new OpenFileDialog
        {
            Filter           = "Aevor Template (*.json)|*.json|All Files (*.*)|*.*",
            Multiselect      = false,
            Title            = "Import Template",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            var selectedPath = dialog.FileName;
            Task.Run(async () =>
            {
                IsLoading = true;
                try
                {
                    // Load and validate the template
                    var template = await _templateSerializer.LoadFromFileAsync(selectedPath);

                    // Copy file to templates directory
                    var destFileName = Path.GetFileName(selectedPath);
                    var destPath = Path.Combine(_templatesDirectory, destFileName);

                    // Avoid overwriting — append number if file exists
                    if (File.Exists(destPath))
                    {
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(destFileName);
                        var ext = Path.GetExtension(destFileName);
                        var counter = 1;
                        do
                        {
                            destPath = Path.Combine(_templatesDirectory, $"{nameWithoutExt}_{counter}{ext}");
                            counter++;
                        } while (File.Exists(destPath));
                    }

                    File.Copy(selectedPath, destPath);

                    // Map to card and add to collections
                    var card = MapTemplateToCard(template, destPath);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Templates.Add(card);
                        ApplyFilter();
                        SetStatusMessage("Template imported successfully");
                    });
                }
                catch (Exception ex)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        SetStatusMessage($"Import failed: {ex.Message}"));
                }
                finally
                {
                    IsLoading = false;
                }
            });
        }
    }
}
