using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Aevor.Application.Interfaces;
using Aevor.Core.Models;
using Aevor.UI.Commands;
using Aevor.UI.Models;

namespace Aevor.UI.ViewModels;

public class BackupsViewModel : BaseViewModel
{
    private readonly IBackupService _backupService;
    private readonly IProfileDiscoveryService _profileDiscoveryService;
    private readonly IBraveInstallationService _braveInstallationService;
    private List<BackupMetadata> _rawBackups = new();

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

    private string _statusMessage = string.Empty;
    private int _statusMessageResetCounter;
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    var currentCounter = System.Threading.Interlocked.Increment(ref _statusMessageResetCounter);
                    Task.Delay(4000).ContinueWith(t =>
                    {
                        if (currentCounter == _statusMessageResetCounter)
                        {
                            _ = RunOnUIAsync(() => StatusMessage = string.Empty);
                        }
                    });
                }
            }
        }
    }

    // ── Commands ───────────────────────────────────────────────────────
    public ICommand RestoreCommand      { get; }
    public ICommand DeleteCommand       { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand RefreshCommand      { get; }

    // ── Constructor ────────────────────────────────────────────────────
    public BackupsViewModel(IBackupService backupService, IProfileDiscoveryService profileDiscoveryService, IBraveInstallationService braveInstallationService)
    {
        _backupService = backupService;
        _profileDiscoveryService = profileDiscoveryService;
        _braveInstallationService = braveInstallationService;

        RestoreCommand      = new RelayCommand<BackupItem>(OnRestore);
        DeleteCommand       = new RelayCommand<BackupItem>(OnDelete);
        CreateBackupCommand = new RelayCommand(OnCreateBackup);
        RefreshCommand      = new RelayCommand(OnRefresh);

        Task.Run(async () => await LoadBackupsAsync());
    }

    // ── Load Backups ───────────────────────────────────────────────────
    private async Task LoadBackupsAsync()
    {
        await RunOnUIAsync(() => IsLoading = true);

        try
        {
            var raw = await _backupService.GetBackupsAsync();
            _rawBackups = raw ?? new List<BackupMetadata>();

            var mapped = _rawBackups.Select(metadata =>
            {
                string validationStatus = metadata.Status switch
                {
                    BackupStatus.Completed => "Valid",
                    BackupStatus.Corrupted => "Warning",
                    BackupStatus.Failed => "Invalid",
                    _ => "Unknown"
                };

                return new BackupItem
                {
                    BackupId = metadata.BackupId,
                    BackupName = metadata.ProfileName + " Backup",
                    ProfileName = metadata.ProfileName,
                    CreatedDate = metadata.CreatedTimestamp.ToString("MMM d, yyyy hh:mm tt"),
                    Size = FormatBytes(metadata.BackupSize),
                    SizeBytes = metadata.BackupSize,
                    ValidationStatus = validationStatus,
                    Notes = "Version " + metadata.BackupVersion
                };
            }).ToList();

            var totalCount = _rawBackups.Count;
            var totalBytesSum = _rawBackups.Sum(b => b.BackupSize);
            var formattedTotalSize = FormatBytes(totalBytesSum);

            await RunOnUIAsync(() =>
            {
                Backups.Clear();
                foreach (var item in mapped)
                {
                    Backups.Add(item);
                }
                TotalBackups = totalCount;
                TotalSize = formattedTotalSize;
                ApplyFilter();
            });
        }
        catch (Exception ex)
        {
            await RunOnUIAsync(() =>
            {
                Backups.Clear();
                FilteredBackups.Clear();
                TotalBackups = 0;
                TotalSize = FormatBytes(0);
                StatusMessage = "Load failed: " + ex.Message;
                OnPropertyChanged(nameof(HasBackups));
            });
        }
        finally
        {
            await RunOnUIAsync(() => IsLoading = false);
        }
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

    // ── Helpers ────────────────────────────────────────────────────────
    private string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes + " B";
        }
        else if (bytes < 1048576)
        {
            return (bytes / 1024.0).ToString("F1") + " KB";
        }
        else if (bytes < 1073741824)
        {
            return (bytes / 1048576.0).ToString("F1") + " MB";
        }
        else
        {
            return (bytes / 1073741824.0).ToString("F2") + " GB";
        }
    }

    private async Task RunOnUIAsync(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null)
        {
            await dispatcher.InvokeAsync(action);
        }
        else
        {
            action();
        }
    }

    // ── Command Handlers ───────────────────────────────────────────────
    private async void OnRestore(BackupItem? item)
    {
        if (item == null) return;

        // Safety check: Is Brave running?
        if (_braveInstallationService.IsBraveRunning())
        {
            await RunOnUIAsync(() =>
            {
                StatusMessage = "Brave Browser is running. Please close all Brave windows before restoring.";
            });
            return;
        }

        // Confirm restore
        bool confirmed = false;
        await RunOnUIAsync(() =>
        {
            var dialog = new Aevor.UI.Views.ConfirmRestoreWindow(
                $"Are you sure you want to restore the backup for '{item.ProfileName}'? This will overwrite your current profile data.")
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            confirmed = (dialog.ShowDialog() == true);
        });

        if (!confirmed) return;

        StatusMessage = "Restoring backup...";
        try
        {
            var result = await _backupService.RestoreBackupAsync(item.BackupId);
            await RunOnUIAsync(() =>
            {
                if (result.IsSuccess)
                {
                    StatusMessage = "Restored " + item.ProfileName + " — " + result.FilesRestored + " files restored";
                }
                else
                {
                    StatusMessage = "Restore failed: " + (result.ErrorMessage ?? "Unknown error");
                }
            });
        }
        catch (Exception ex)
        {
            await RunOnUIAsync(() =>
            {
                StatusMessage = "Restore failed: " + ex.Message;
            });
        }
    }

    private async void OnDelete(BackupItem? item)
    {
        if (item == null) return;

        bool confirmed = false;
        await RunOnUIAsync(() =>
        {
            var dialog = new Aevor.UI.Views.ConfirmDeleteWindow(
                $"Are you sure you want to delete the backup for '{item.ProfileName}' created on {item.CreatedDate}?")
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            confirmed = (dialog.ShowDialog() == true);
        });

        if (!confirmed) return;

        try
        {
            var success = await _backupService.DeleteBackupAsync(item.BackupId);
            if (success)
            {
                var rawItem = _rawBackups.FirstOrDefault(b => b.BackupId == item.BackupId);
                if (rawItem != null)
                {
                    _rawBackups.Remove(rawItem);
                }

                var totalCount = _rawBackups.Count;
                var totalBytesSum = _rawBackups.Sum(b => b.BackupSize);
                var formattedTotalSize = FormatBytes(totalBytesSum);

                await RunOnUIAsync(() =>
                {
                    Backups.Remove(item);
                    FilteredBackups.Remove(item);
                    TotalBackups = totalCount;
                    TotalSize = formattedTotalSize;
                    StatusMessage = "Backup deleted";
                    ApplyFilter();
                });
            }
            else
            {
                await RunOnUIAsync(() =>
                {
                    StatusMessage = "Delete failed";
                });
            }
        }
        catch (Exception ex)
        {
            await RunOnUIAsync(() =>
            {
                StatusMessage = "Delete failed: " + ex.Message;
            });
        }
    }

    private async void OnCreateBackup()
    {
        try
        {
            var profiles = await _profileDiscoveryService.GetProfilesAsync();
            if (profiles == null || profiles.Count == 0)
            {
                await RunOnUIAsync(() =>
                {
                    StatusMessage = "No profiles found";
                });
                return;
            }

            // Show a simple profile picker dialog
            BraveProfile? selectedProfile = null;
            await RunOnUIAsync(() =>
            {
                var dialog = new Aevor.UI.Views.CreateBackupWindow(profiles)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };
                if (dialog.ShowDialog() == true)
                {
                    selectedProfile = dialog.SelectedProfile;
                }
            });

            if (selectedProfile == null)
            {
                return;
            }

            var profile = selectedProfile;

            var result = await _backupService.CreateBackupAsync(profile);
            if (result.IsSuccess && result.Metadata != null)
            {
                _rawBackups.Insert(0, result.Metadata);

                var totalCount = _rawBackups.Count;
                var totalBytesSum = _rawBackups.Sum(b => b.BackupSize);
                var formattedTotalSize = FormatBytes(totalBytesSum);

                string validationStatus = result.Metadata.Status switch
                {
                    BackupStatus.Completed => "Valid",
                    BackupStatus.Corrupted => "Warning",
                    BackupStatus.Failed => "Invalid",
                    _ => "Unknown"
                };

                var newItem = new BackupItem
                {
                    BackupId = result.Metadata.BackupId,
                    BackupName = result.Metadata.ProfileName + " Backup",
                    ProfileName = result.Metadata.ProfileName,
                    CreatedDate = result.Metadata.CreatedTimestamp.ToString("MMM d, yyyy hh:mm tt"),
                    Size = FormatBytes(result.Metadata.BackupSize),
                    SizeBytes = result.Metadata.BackupSize,
                    ValidationStatus = validationStatus,
                    Notes = "Version " + result.Metadata.BackupVersion
                };

                await RunOnUIAsync(() =>
                {
                    Backups.Insert(0, newItem);
                    TotalBackups = totalCount;
                    TotalSize = formattedTotalSize;
                    StatusMessage = "Backup created — " + FormatBytes(result.Metadata.BackupSize);
                    ApplyFilter();
                });
            }
            else
            {
                await RunOnUIAsync(() =>
                {
                    StatusMessage = "Backup failed: " + (result.ErrorMessage ?? "Unknown error");
                });
            }
        }
        catch (Exception ex)
        {
            await RunOnUIAsync(() =>
            {
                StatusMessage = "Backup failed: " + ex.Message;
            });
        }
    }

    private async void OnRefresh()
    {
        SearchQuery = string.Empty;
        await RunOnUIAsync(() =>
        {
            Backups.Clear();
            FilteredBackups.Clear();
        });
        await LoadBackupsAsync();
    }
}
