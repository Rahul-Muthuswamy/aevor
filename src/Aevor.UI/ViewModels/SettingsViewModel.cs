using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Aevor.Application.Interfaces;
using Aevor.UI.Commands;

namespace Aevor.UI.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly IBraveInstallationService _braveInstallation;

    // ── Settings Properties ────────────────────────────────────────────
    private string _braveUserDataPath = string.Empty;
    public string BraveUserDataPath
    {
        get => _braveUserDataPath;
        set => SetProperty(ref _braveUserDataPath, value);
    }

    private string _backupsPath = string.Empty;
    public string BackupsPath
    {
        get => _backupsPath;
        set => SetProperty(ref _backupsPath, value);
    }

    private string _appTheme = "System";
    public string AppTheme
    {
        get => _appTheme;
        set => SetProperty(ref _appTheme, value);
    }

    private bool _autoScanOnStartup = true;
    public bool AutoScanOnStartup
    {
        get => _autoScanOnStartup;
        set => SetProperty(ref _autoScanOnStartup, value);
    }

    private bool _safeBackupBeforeTemplate = true;
    public bool SafeBackupBeforeTemplate
    {
        get => _safeBackupBeforeTemplate;
        set => SetProperty(ref _safeBackupBeforeTemplate, value);
    }

    private bool _blockActiveCookiesOnClone = true;
    public bool BlockActiveCookiesOnClone
    {
        get => _blockActiveCookiesOnClone;
        set => SetProperty(ref _blockActiveCookiesOnClone, value);
    }

    private bool _alwaysExcludeHistory = true;
    public bool AlwaysExcludeHistory
    {
        get => _alwaysExcludeHistory;
        set => SetProperty(ref _alwaysExcludeHistory, value);
    }

    private bool _alwaysExcludePasswords = true;
    public bool AlwaysExcludePasswords
    {
        get => _alwaysExcludePasswords;
        set => SetProperty(ref _alwaysExcludePasswords, value);
    }

    // ── Status Message ─────────────────────────────────────────────────
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

    // ── Commands ───────────────────────────────────────────────────────
    public ICommand SaveSettingsCommand  { get; }
    public ICommand ResetSettingsCommand { get; }

    // ── Constructor ────────────────────────────────────────────────────
    public SettingsViewModel(IBraveInstallationService braveInstallation)
    {
        _braveInstallation = braveInstallation ?? throw new ArgumentNullException(nameof(braveInstallation));

        SaveSettingsCommand  = new RelayCommand(async () => await SaveSettingsAsync());
        ResetSettingsCommand = new RelayCommand(ResetSettings);

        LoadDefaultSettings();
    }

    // ── Methods ────────────────────────────────────────────────────────
    private void LoadDefaultSettings()
    {
        try
        {
            BraveUserDataPath = _braveInstallation.GetUserDataPath();
        }
        catch
        {
            BraveUserDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BraveSoftware", "Brave-Browser", "User Data"
            );
        }

        BackupsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aevor", "Backups"
        );

        AppTheme                  = "System";
        AutoScanOnStartup        = true;
        SafeBackupBeforeTemplate = true;
        BlockActiveCookiesOnClone = true;
        AlwaysExcludeHistory      = true;
        AlwaysExcludePasswords    = true;
    }

    private async Task SaveSettingsAsync()
    {
        StatusMessage = "Saving settings...";
        IsMessageSuccess = null;

        await Task.Delay(500); // Simulate local state persistence

        StatusMessage = "Settings saved successfully.";
        IsMessageSuccess = true;
    }

    private void ResetSettings()
    {
        LoadDefaultSettings();
        StatusMessage = "Settings reset to defaults.";
        IsMessageSuccess = true;
    }
}
