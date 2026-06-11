using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Aevor.UI.Commands;
using Aevor.UI.Models;

namespace Aevor.UI.ViewModels;

public class BackupsViewModel : BaseViewModel
{
    // ── Collections ────────────────────────────────────────────────────
    public ObservableCollection<BackupItem> Backups         { get; } = new();
    public ObservableCollection<BackupItem> FilteredBackups { get; } = new();

    // ── Properties ─────────────────────────────────────────────────────
    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyFilter();
            }
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(HasBackups));
            }
        }
    }

    private int _totalBackups;
    public int TotalBackups
    {
        get => _totalBackups;
        private set => SetProperty(ref _totalBackups, value);
    }

    private string _totalSize = "0.0 MB";
    public string TotalSize
    {
        get => _totalSize;
        private set => SetProperty(ref _totalSize, value);
    }

    public bool HasBackups => !IsLoading && FilteredBackups.Count > 0;

    // ── Commands ───────────────────────────────────────────────────────
    public ICommand RestoreCommand      { get; }
    public ICommand DeleteCommand       { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand RefreshCommand      { get; }

    // ── Constructor ────────────────────────────────────────────────────
    public BackupsViewModel()
    {
        RestoreCommand      = new RelayCommand<BackupItem>(OnRestore);
        DeleteCommand       = new RelayCommand<BackupItem>(OnDelete);
        CreateBackupCommand = new RelayCommand(OnCreateBackup);
        RefreshCommand      = new RelayCommand(OnRefresh);

        LoadSampleData();
    }

    // ── Sample Data ────────────────────────────────────────────────────
    private void LoadSampleData()
    {
        IsLoading = true;

        Backups.Clear();
        Backups.Add(new BackupItem
        {
            BackupName       = "Pre-Update Snapshot",
            ProfileName      = "Personal",
            CreatedDate      = "Jun 10, 2026 10:45 AM",
            Size             = "45.2 MB",
            ValidationStatus = "Valid",
            Notes            = "Created automatically before Brave update"
        });
        Backups.Add(new BackupItem
        {
            BackupName       = "Work Secure Clean",
            ProfileName      = "Work",
            CreatedDate      = "Jun 9, 2026 04:20 PM",
            Size             = "12.8 MB",
            ValidationStatus = "Warning",
            Notes            = "Cookies present in backup folder"
        });
        Backups.Add(new BackupItem
        {
            BackupName       = "Dev Sandbox Draft",
            ProfileName      = "Development",
            CreatedDate      = "Jun 05, 2026 11:00 AM",
            Size             = "82.4 MB",
            ValidationStatus = "Invalid",
            Notes            = "Manifest hash mismatch detected"
        });
        Backups.Add(new BackupItem
        {
            BackupName       = "Research Safe Restore",
            ProfileName      = "Research",
            CreatedDate      = "May 28, 2026 08:15 PM",
            Size             = "3.1 MB",
            ValidationStatus = "Valid",
            Notes            = ""
        });
        Backups.Add(new BackupItem
        {
            BackupName       = "Bounty Isolation Backup",
            ProfileName      = "Bug Bounty",
            CreatedDate      = "May 15, 2026 02:30 PM",
            Size             = "14.2 MB",
            ValidationStatus = "Valid",
            Notes            = "Clean baseline backup"
        });

        ApplyFilter();
        UpdateStats();

        IsLoading = false;
    }

    // ── Filtering ──────────────────────────────────────────────────────
    private void ApplyFilter()
    {
        FilteredBackups.Clear();
        var query = SearchQuery?.Trim() ?? string.Empty;

        foreach (var b in Backups)
        {
            if (string.IsNullOrEmpty(query) ||
                b.BackupName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                b.ProfileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                b.Notes.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredBackups.Add(b);
            }
        }

        OnPropertyChanged(nameof(HasBackups));
    }

    // ── Helper ─────────────────────────────────────────────────────────
    private void UpdateStats()
    {
        TotalBackups = Backups.Count;
        double totalMb = 0;
        foreach (var b in Backups)
        {
            var parts = b.Size.Split(' ');
            if (parts.Length > 0 && double.TryParse(parts[0], out double val))
            {
                totalMb += val;
            }
        }
        TotalSize = $"{totalMb:F1} MB";
    }

    // ── Command Handlers ───────────────────────────────────────────────
    private void OnRestore(BackupItem? item)
    {
        if (item == null) return;
        // In real app, this would execute restoration.
        // We will mock updating the notes or status.
    }

    private void OnDelete(BackupItem? item)
    {
        if (item == null) return;
        Backups.Remove(item);
        ApplyFilter();
        UpdateStats();
    }

    private void OnCreateBackup()
    {
        // Add a mock new backup
        var now = DateTime.Now;
        var newBackup = new BackupItem
        {
            BackupName       = $"Manual Snapshot {now:HH:mm}",
            ProfileName      = "Personal",
            CreatedDate      = now.ToString("MMM dd, yyyy hh:mm tt"),
            Size             = "15.0 MB",
            ValidationStatus = "Valid",
            Notes            = "Manually initiated backup point"
        };
        Backups.Insert(0, newBackup);
        ApplyFilter();
        UpdateStats();
    }

    private void OnRefresh()
    {
        SearchQuery = string.Empty;
        LoadSampleData();
    }
}
