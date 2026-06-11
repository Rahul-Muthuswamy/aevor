using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Aevor.UI.Commands;
using Aevor.UI.Models;

namespace Aevor.UI.ViewModels;

public class TemplatesViewModel : BaseViewModel
{
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

    public bool HasTemplates => !IsLoading && FilteredTemplates.Count > 0;

    // ── Commands ───────────────────────────────────────────────────────
    public ICommand ApplyCommand          { get; }
    public ICommand ExportCommand         { get; }
    public ICommand DeleteCommand         { get; }
    public ICommand ImportTemplateCommand { get; }
    public ICommand RefreshCommand        { get; }

    // ── Constructor ────────────────────────────────────────────────────
    public TemplatesViewModel()
    {
        ApplyCommand          = new RelayCommand<TemplateCardItem>(OnApply);
        ExportCommand         = new RelayCommand<TemplateCardItem>(OnExport);
        DeleteCommand         = new RelayCommand<TemplateCardItem>(OnDelete);
        ImportTemplateCommand = new RelayCommand(OnImport);
        RefreshCommand        = new RelayCommand(OnRefresh);

        LoadSampleData();
    }

    // ── Sample Data ────────────────────────────────────────────────────
    private void LoadSampleData()
    {
        var samples = new[]
        {
            TemplateCardItem.Create(
                name:        "Developer Setup",
                browser:     "Brave Browser",
                version:     "1.2",
                created:     "Jun 8, 2025",
                extensions:  14,
                description: "Curated dev environment with GitHub Copilot, REST Client, JSON Formatter, and ad-blocking essentials.",
                tag:         "Dev",
                bgHex:       "#DBEAFE",   // blue-100
                fgHex:       "#1E40AF"    // blue-800
            ),
            TemplateCardItem.Create(
                name:        "Security Research",
                browser:     "Brave Browser",
                version:     "2.0",
                created:     "May 31, 2025",
                extensions:  8,
                description: "Hardened research profile with Wappalyzer, Cookie Editor, network inspection tools, and no credentials stored.",
                tag:         "Security",
                bgHex:       "#FEE2E2",   // red-100
                fgHex:       "#991B1B"    // red-800
            ),
            TemplateCardItem.Create(
                name:        "Work Productivity",
                browser:     "Brave Browser",
                version:     "1.0",
                created:     "Jun 1, 2025",
                extensions:  11,
                description: "Office-optimized profile with Grammarly, LastPass, Google Workspace utilities, and Slack notifier.",
                tag:         "Work",
                bgHex:       "#D1FAE5",   // green-100
                fgHex:       "#065F46"    // green-800
            ),
            TemplateCardItem.Create(
                name:        "Bug Bounty",
                browser:     "Brave Browser",
                version:     "1.5",
                created:     "May 15, 2025",
                extensions:  5,
                description: "Minimal, isolated bug bounty profile with Burp Suite proxy, FoxyProxy, and no personal data.",
                tag:         "Bounty",
                bgHex:       "#FEF3C7",   // yellow-100
                fgHex:       "#92400E"    // yellow-800
            ),
            TemplateCardItem.Create(
                name:        "Student Lab",
                browser:     "Brave Browser",
                version:     "1.0",
                created:     "Jun 3, 2025",
                extensions:  6,
                description: "Clean student environment with citation tools, ad blocker, and Google Scholar utilities pre-configured.",
                tag:         "Study",
                bgHex:       "#EDE9FE",   // purple-100
                fgHex:       "#5B21B6"    // purple-800
            ),
            TemplateCardItem.Create(
                name:        "Privacy Mode",
                browser:     "Brave Browser",
                version:     "1.1",
                created:     "Jun 5, 2025",
                extensions:  3,
                description: "Ultra-private browsing template with uBlock Origin, Privacy Badger, and anti-fingerprinting activated.",
                tag:         "Privacy",
                bgHex:       "#F3F4F6",   // gray-100
                fgHex:       "#374151"    // gray-700
            ),
        };

        foreach (var t in samples)
            Templates.Add(t);

        ApplyFilter();
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

    // ── Command Handlers ───────────────────────────────────────────────
    private void OnApply(TemplateCardItem? t)  { SelectedTemplate = t; }
    private void OnExport(TemplateCardItem? t) { SelectedTemplate = t; }
    private void OnDelete(TemplateCardItem? t)
    {
        if (t == null) return;
        Templates.Remove(t);
        ApplyFilter();
    }
    private void OnImport()  { /* future: open file dialog */ }
    private void OnRefresh() { SearchQuery = string.Empty; ApplyFilter(); }
}
