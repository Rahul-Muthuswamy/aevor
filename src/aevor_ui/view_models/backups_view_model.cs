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
    private readonly IToastService _toastService;
    private List<BackupMetadata> _rawBackups = new();
    private bool _isUpdatingSelection;
    private bool? _isAllSelected = false;

    // ── Collections ────────────────────────────────────────────────────
    public ObservableCollection<BackupItem> Backups         { get; } = new();
    public ObservableCollection<BackupItem> FilteredBackups { get; } = new();

    public bool? IsAllSelected
    {
        get => _isAllSelected;
        set
        {
            if (SetProperty(ref _isAllSelected, value))
            {
                if (value.HasValue && !_isUpdatingSelection)
                {
                    _isUpdatingSelection = true;
                    foreach (var b in FilteredBackups)
                    {
                        b.IsSelected = value.Value;
                    }
                    _isUpdatingSelection = false;
                    OnPropertyChanged(nameof(HasSelectedBackups));
                }
            }
        }
    }

    public bool HasSelectedBackups => FilteredBackups.Any(b => b.IsSelected);

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
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // ── Commands ───────────────────────────────────────────────────────
    public ICommand RestoreCommand      { get; }
    public ICommand DeleteCommand       { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand RefreshCommand      { get; }

    // ── Constructor ────────────────────────────────────────────────────
    public BackupsViewModel(
        IBackupService backupService,
        IProfileDiscoveryService profileDiscoveryService,
        IBraveInstallationService braveInstallationService,
        IToastService toastService)
    {
        _backupService = backupService;
        _profileDiscoveryService = profileDiscoveryService;
        _braveInstallationService = braveInstallationService;
        _toastService = toastService;

        RestoreCommand      = new RelayCommand<BackupItem>(OnRestore);
        DeleteCommand       = new RelayCommand<BackupItem>(OnDelete);
        DeleteSelectedCommand = new RelayCommand(OnDeleteSelected, () => HasSelectedBackups);
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
            var raw = await Task.Run(() => _backupService.GetBackupsAsync());
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

                var item = new BackupItem
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
                item.PropertyChanged += BackupItem_PropertyChanged;
                return item;
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

        if (!_isUpdatingSelection)
        {
            _isUpdatingSelection = true;
            if (FilteredBackups.Count == 0)
            {
                IsAllSelected = false;
            }
            else if (FilteredBackups.All(b => b.IsSelected))
            {
                IsAllSelected = true;
            }
            else if (FilteredBackups.All(b => !b.IsSelected))
            {
                IsAllSelected = false;
            }
            else
            {
                IsAllSelected = null;
            }
            _isUpdatingSelection = false;
        }

        OnPropertyChanged(nameof(HasBackups));
        OnPropertyChanged(nameof(HasSelectedBackups));
    }

    private void BackupItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BackupItem.IsSelected))
        {
            OnPropertyChanged(nameof(HasSelectedBackups));

            if (!_isUpdatingSelection)
            {
                _isUpdatingSelection = true;
                if (FilteredBackups.Count == 0)
                {
                    IsAllSelected = false;
                }
                else if (FilteredBackups.All(b => b.IsSelected))
                {
                    IsAllSelected = true;
                }
                else if (FilteredBackups.All(b => !b.IsSelected))
                {
                    IsAllSelected = false;
                }
                else
                {
                    IsAllSelected = null;
                }
                _isUpdatingSelection = false;
            }
        }
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
            _toastService.Show("Brave Browser is running. Please close all Brave windows before restoring.", ToastType.Warning);
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

        _toastService.Show("Restoring backup...", ToastType.Info);
        try
        {
            var result = await _backupService.RestoreBackupAsync(item.BackupId);
            if (result.IsSuccess)
            {
                _toastService.Show("Restored " + item.ProfileName + " — " + result.FilesRestored + " files restored", ToastType.Success);
            }
            else
            {
                _toastService.Show("Restore failed: " + (result.ErrorMessage ?? "Unknown error"), ToastType.Error);
            }
        }
        catch (Exception ex)
        {
            _toastService.Show("Restore failed: " + ex.Message, ToastType.Error);
        }
    }

    private async void OnDelete(BackupItem? item)
    {
        if (item == null) return;

        bool success = false;
        await RunOnUIAsync(() =>
        {
            var dialog = new Aevor.UI.Views.ConfirmDeleteWindow(
                $"Are you sure you want to delete the backup for '{item.ProfileName}' created on {item.CreatedDate}?",
                async () =>
                {
                    return await Task.Run(() => _backupService.DeleteBackupAsync(item.BackupId));
                })
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            success = (dialog.ShowDialog() == true);
        });

        if (!success) return;

        try
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
                ApplyFilter();
            });
            _toastService.Show("Backup deleted", ToastType.Success);
        }
        catch (Exception ex)
        {
            _toastService.Show("Delete failed: " + ex.Message, ToastType.Error);
        }
    }

    private async void OnDeleteSelected()
    {
        var selectedItems = FilteredBackups.Where(b => b.IsSelected).ToList();
        if (selectedItems.Count == 0) return;

        int deletedCount = 0;
        int failedCount = 0;

        bool success = false;
        await RunOnUIAsync(() =>
        {
            var dialog = new Aevor.UI.Views.ConfirmDeleteWindow(
                $"Are you sure you want to delete the selected {selectedItems.Count} backup(s)?",
                async () =>
                {
                    deletedCount = 0;
                    failedCount = 0;
                    foreach (var item in selectedItems)
                    {
                        try
                        {
                            var itemSuccess = await Task.Run(() => _backupService.DeleteBackupAsync(item.BackupId));
                            if (itemSuccess)
                            {
                                var rawItem = _rawBackups.FirstOrDefault(b => b.BackupId == item.BackupId);
                                if (rawItem != null)
                                {
                                    _rawBackups.Remove(rawItem);
                                }
                                await RunOnUIAsync(() =>
                                {
                                    Backups.Remove(item);
                                    FilteredBackups.Remove(item);
                                });
                                deletedCount++;
                            }
                            else
                            {
                                failedCount++;
                            }
                        }
                        catch (Exception)
                        {
                            failedCount++;
                        }
                    }
                    return deletedCount > 0;
                })
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            success = (dialog.ShowDialog() == true);
        });

        if (!success) return;

        var totalCount = _rawBackups.Count;
        var totalBytesSum = _rawBackups.Sum(b => b.BackupSize);
        var formattedTotalSize = FormatBytes(totalBytesSum);

        await RunOnUIAsync(() =>
        {
            TotalBackups = totalCount;
            TotalSize = formattedTotalSize;
            ApplyFilter();
        });

        if (failedCount == 0)
        {
            _toastService.Show($"{deletedCount} backup(s) deleted", ToastType.Success);
        }
        else
        {
            _toastService.Show($"{deletedCount} deleted, {failedCount} failed", ToastType.Warning);
        }
    }

    private async void OnCreateBackup()
    {
        try
        {
            var profiles = await _profileDiscoveryService.GetProfilesAsync();
            if (profiles == null || profiles.Count == 0)
            {
                _toastService.Show("No profiles found", ToastType.Error);
                return;
            }

            // Show simple profile picker dialog with async creation callback
            await RunOnUIAsync(() =>
            {
                var dialog = new Aevor.UI.Views.CreateBackupWindow(profiles, async (profile) =>
                {
                    var result = await Task.Run(() => _backupService.CreateBackupAsync(profile));
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
                        newItem.PropertyChanged += BackupItem_PropertyChanged;

                        await RunOnUIAsync(() =>
                        {
                            Backups.Insert(0, newItem);
                            TotalBackups = totalCount;
                            TotalSize = formattedTotalSize;
                            ApplyFilter();
                        });
                        _toastService.Show("Backup created — " + FormatBytes(result.Metadata.BackupSize), ToastType.Success);
                        return true;
                    }
                    else
                    {
                        _toastService.Show("Backup failed: " + (result.ErrorMessage ?? "Unknown error"), ToastType.Error);
                        return false;
                    }
                })
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };
                dialog.ShowDialog();
            });
        }
        catch (Exception ex)
        {
            _toastService.Show("Backup failed: " + ex.Message, ToastType.Error);
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
