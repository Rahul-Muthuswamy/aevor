using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Aevor.Application.Interfaces;
using Aevor.UI.Commands;
using Aevor.UI.Models;

namespace Aevor.UI.ViewModels;

public class BackupsViewModel : BaseViewModel
{
    private readonly IBackupService _backupService;

    // ── Collections ────────────────────────────────────────────────────
    public ObservableCollection<BackupCardItem> Backups         { get; } = new();
    public ObservableCollection<BackupCardItem> FilteredBackups { get; } = new();

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

    private BackupCardItem? _selectedBackup;
    public BackupCardItem? SelectedBackup
    {
        get => _selectedBackup;
        set => SetProperty(ref _selectedBackup, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
                OnPropertyChanged(nameof(HasBackups));
        }
    }

    private bool _isRestoring;
    public bool IsRestoring
    {
        get => _isRestoring;
        set => SetProperty(ref _isRestoring, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool? _isMessageSuccess;
    public bool? IsMessageSuccess
    {
        get => _isMessageSuccess;
        set => SetProperty(ref _isMessageSuccess, value);
    }

    public bool HasBackups => !IsLoading && FilteredBackups.Count > 0;

    // ── Commands ───────────────────────────────────────────────────────
    public ICommand RefreshCommand  { get; }
    public ICommand RestoreCommand  { get; }
    public ICommand DeleteCommand   { get; }
    public ICommand ValidateCommand { get; }

    // ── Constructor ────────────────────────────────────────────────────
    public BackupsViewModel(IBackupService backupService)
    {
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));

        RefreshCommand  = new RelayCommand(async () => await LoadBackupsAsync());
        RestoreCommand  = new RelayCommand<BackupCardItem>(async (b) => await OnRestoreBackup(b));
        DeleteCommand   = new RelayCommand<BackupCardItem>(async (b) => await OnDeleteBackup(b));
        ValidateCommand = new RelayCommand<BackupCardItem>(async (b) => await OnValidateBackup(b));

        // Load initially
        _ = LoadBackupsAsync();
    }

    // ── Load & Filter ──────────────────────────────────────────────────
    public async Task LoadBackupsAsync()
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        IsMessageSuccess = null;

        try
        {
            var rawBackups = await _backupService.GetBackupsAsync();
            
            Backups.Clear();
            foreach (var metadata in rawBackups.OrderByDescending(b => b.CreatedTimestamp))
            {
                Backups.Add(BackupCardItem.FromMetadata(metadata));
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load backups: {ex.Message}";
            IsMessageSuccess = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredBackups.Clear();
        var query = SearchQuery?.Trim() ?? string.Empty;

        foreach (var b in Backups)
        {
            if (string.IsNullOrEmpty(query) ||
                b.ProfileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                b.BackupId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredBackups.Add(b);
            }
        }

        OnPropertyChanged(nameof(HasBackups));
    }

    // ── Action Handlers ────────────────────────────────────────────────
    private async Task OnRestoreBackup(BackupCardItem? item)
    {
        if (item == null || IsRestoring) return;

        IsRestoring = true;
        StatusMessage = $"Restoring backup of profile '{item.ProfileName}'...";
        IsMessageSuccess = null;

        try
        {
            var result = await _backupService.RestoreBackupAsync(item.BackupId);
            if (result.IsSuccess)
            {
                StatusMessage = $"Successfully restored {result.FilesRestored} files ({result.TotalBytesRestored / 1024.0:F1} KB) for '{item.ProfileName}'.";
                IsMessageSuccess = true;
            }
            else
            {
                StatusMessage = $"Restore failed: {result.ErrorMessage}";
                IsMessageSuccess = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Restore error: {ex.Message}";
            IsMessageSuccess = false;
        }
        finally
        {
            IsRestoring = false;
        }
    }

    private async Task OnDeleteBackup(BackupCardItem? item)
    {
        if (item == null) return;

        StatusMessage = $"Deleting backup from {item.FormattedDate}...";
        IsMessageSuccess = null;

        try
        {
            var success = await _backupService.DeleteBackupAsync(item.BackupId);
            if (success)
            {
                Backups.Remove(item);
                ApplyFilter();
                StatusMessage = "Backup deleted successfully.";
                IsMessageSuccess = true;
            }
            else
            {
                StatusMessage = "Delete failed: could not locate backup directory.";
                IsMessageSuccess = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete error: {ex.Message}";
            IsMessageSuccess = false;
        }
    }

    private async Task OnValidateBackup(BackupCardItem? item)
    {
        if (item == null) return;

        StatusMessage = "Validating backup integrity...";
        IsMessageSuccess = null;

        try
        {
            var result = await _backupService.ValidateBackupAsync(item.BackupId);
            if (result.IsValid)
            {
                StatusMessage = "Backup is valid! Integrity check passed.";
                IsMessageSuccess = true;
            }
            else
            {
                var errors = string.Join(", ", result.Errors);
                StatusMessage = $"Validation failed: {errors}";
                IsMessageSuccess = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Validation error: {ex.Message}";
            IsMessageSuccess = false;
        }
    }
}
