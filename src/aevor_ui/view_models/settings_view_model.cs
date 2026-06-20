using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using Aevor.Application.Interfaces;
using Aevor.UI.Commands;

namespace Aevor.UI.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly IBraveInstallationService _braveInstallation;

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

    private bool _hasCompletedOnboarding;
    public bool HasCompletedOnboarding
    {
        get => _hasCompletedOnboarding;
        set => SetProperty(ref _hasCompletedOnboarding, value);
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

    public ICommand SaveSettingsCommand  { get; }
    public ICommand ResetSettingsCommand { get; }
    public ICommand OpenRepoCommand      { get; }
    public ICommand OpenReleasesCommand  { get; }
    public ICommand OpenIssuesCommand    { get; }

    private const string GitHubOwner = "Rahul-Muthuswamy";
    private const string GitHubRepo  = "aevor";

    public string AppVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            if (v is null) return "v0.0.0";
            return $"v{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    public string CurrentVersion => AppVersion;

    public SettingsViewModel(IBraveInstallationService braveInstallation)
    {
        _braveInstallation = braveInstallation ?? throw new ArgumentNullException(nameof(braveInstallation));

        SaveSettingsCommand  = new RelayCommand(async () => await SaveSettingsAsync());
        ResetSettingsCommand = new RelayCommand(ResetSettings);
        OpenRepoCommand      = new RelayCommand(() => OpenUrl($"https://github.com/{GitHubOwner}/{GitHubRepo}"));
        OpenReleasesCommand  = new RelayCommand(() => OpenUrl($"https://github.com/{GitHubOwner}/{GitHubRepo}/releases"));
        OpenIssuesCommand    = new RelayCommand(() => OpenUrl($"https://github.com/{GitHubOwner}/{GitHubRepo}/issues"));

        LoadSettings();
    }

    private string GetSettingsFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aevor",
            "settings.json"
        );
    }

    private void LoadSettings()
    {
        LoadDefaultSettings();

        try
        {
            var path = GetSettingsFilePath();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var data = System.Text.Json.JsonSerializer.Deserialize<SettingsData>(json);
                if (data != null)
                {
                    if (!string.IsNullOrEmpty(data.BraveUserDataPath)) BraveUserDataPath = data.BraveUserDataPath;
                    if (!string.IsNullOrEmpty(data.BackupsPath)) BackupsPath = data.BackupsPath;
                    AutoScanOnStartup = data.AutoScanOnStartup;
                    SafeBackupBeforeTemplate = data.SafeBackupBeforeTemplate;
                    BlockActiveCookiesOnClone = data.BlockActiveCookiesOnClone;
                    AlwaysExcludeHistory = data.AlwaysExcludeHistory;
                    AlwaysExcludePasswords = data.AlwaysExcludePasswords;
                    HasCompletedOnboarding = data.hasCompletedOnboarding;
                }
            }
        }
        catch
        {

        }
    }

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
        AutoScanOnStartup        = true;
        SafeBackupBeforeTemplate = true;
        BlockActiveCookiesOnClone = true;
        AlwaysExcludeHistory      = true;
        AlwaysExcludePasswords    = true;
        HasCompletedOnboarding    = false;
    }

    private async Task SaveSettingsAsync()
    {
        StatusMessage = "Saving settings...";
        IsMessageSuccess = null;

        try
        {
            var data = new SettingsData
            {
                BraveUserDataPath = BraveUserDataPath,
                BackupsPath = BackupsPath,
                AutoScanOnStartup = AutoScanOnStartup,
                SafeBackupBeforeTemplate = SafeBackupBeforeTemplate,
                BlockActiveCookiesOnClone = BlockActiveCookiesOnClone,
                AlwaysExcludeHistory = AlwaysExcludeHistory,
                AlwaysExcludePasswords = AlwaysExcludePasswords,
                hasCompletedOnboarding = HasCompletedOnboarding
            };

            var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var path = GetSettingsFilePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllTextAsync(path, json);

            StatusMessage = "Settings saved successfully.";
            IsMessageSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to save settings: " + ex.Message;
            IsMessageSuccess = false;
        }
    }

    private void ResetSettings()
    {
        LoadDefaultSettings();
        try
        {
            var path = GetSettingsFilePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {

        }
        StatusMessage = "Settings reset to defaults.";
        IsMessageSuccess = true;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {

        }
    }
}

public class SettingsData
{
    public string BraveUserDataPath { get; set; } = string.Empty;
    public string BackupsPath { get; set; } = string.Empty;
    public bool AutoScanOnStartup { get; set; } = true;
    public bool SafeBackupBeforeTemplate { get; set; } = true;
    public bool BlockActiveCookiesOnClone { get; set; } = true;
    public bool AlwaysExcludeHistory { get; set; } = true;
    public bool AlwaysExcludePasswords { get; set; } = true;
    public bool hasCompletedOnboarding { get; set; } = false;
}
