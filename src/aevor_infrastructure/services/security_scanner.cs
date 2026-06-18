using Microsoft.Extensions.Logging;
using Aevor.Application.Interfaces;
using Aevor.Application.Models;
using Aevor.Core.Exceptions;
using Aevor.Core.Models;

namespace Aevor.Infrastructure.Services;

public class SecurityScanner : ISecurityScanner
{
    private readonly IFileSystem _fileSystem;
    private readonly ExportSafetyEvaluator _safetyEvaluator;
    private readonly SecurityScannerOptions _options;
    private readonly IBraveInstallationService _installationService;
    private readonly ILogger<SecurityScanner> _logger;

    public SecurityScanner(
        IFileSystem fileSystem,
        ExportSafetyEvaluator safetyEvaluator,
        SecurityScannerOptions options,
        IBraveInstallationService installationService,
        ILogger<SecurityScanner> logger)
    {
        _fileSystem = fileSystem;
        _safetyEvaluator = safetyEvaluator;
        _options = options;
        _installationService = installationService;
        _logger = logger;
    }

    public async Task<SecurityScanResult> ScanAsync(BraveProfile profile)
    {
        _logger.LogInformation("Security scan started for profile: {ProfileName}", profile.DisplayName);

        if (!_fileSystem.DirectoryExists(profile.ProfilePath))
        {
            _logger.LogError("Profile directory does not exist: {ProfilePath}", profile.ProfilePath);
            throw new SecurityScanException($"Profile directory does not exist: {profile.ProfilePath}");
        }

        var findings = new List<SecurityFinding>();
        int cumulativeWeight = 0;

        var loginDataPath = Path.Combine(profile.ProfilePath, "Login Data");
        var loginDataForAccountPath = Path.Combine(profile.ProfilePath, "Login Data For Account");
        var hasPasswords = await IsValidSqliteDatabaseAsync(loginDataPath) || await IsValidSqliteDatabaseAsync(loginDataForAccountPath);

        if (hasPasswords)
        {
            cumulativeWeight += _options.PasswordWeight;
            findings.Add(new SecurityFinding(
                "Saved Passwords",
                "Credentials",
                SecuritySeverity.Low,
                "Saved passwords database containing credentials detected.",
                loginDataPath
            ));
            _logger.LogWarning("Passwords detected in profile: {ProfileName}", profile.DisplayName);
        }

        var cookiesPath = Path.Combine(profile.ProfilePath, "Network", "Cookies");
        var legacyCookiesPath = Path.Combine(profile.ProfilePath, "Cookies");
        var hasCookies = await IsValidSqliteDatabaseAsync(cookiesPath) || await IsValidSqliteDatabaseAsync(legacyCookiesPath);

        if (hasCookies)
        {
            cumulativeWeight += _options.CookieWeight;
            findings.Add(new SecurityFinding(
                "Session Cookies",
                "Cookies",
                SecuritySeverity.Low,
                "Active session cookies database detected.",
                cookiesPath
            ));
            _logger.LogWarning("Cookies detected in profile: {ProfileName}", profile.DisplayName);
        }

        var walletPath = Path.Combine(profile.ProfilePath, "BraveWallet");
        var hasWalletData = _fileSystem.DirectoryExists(walletPath);

        if (hasWalletData)
        {
            cumulativeWeight += _options.WalletWeight;
            findings.Add(new SecurityFinding(
                "Brave Wallet Data",
                "Cryptocurrency Wallet",
                SecuritySeverity.High,
                "Brave Cryptocurrency Wallet local configuration folder detected.",
                walletPath
            ));
            _logger.LogWarning("Brave Wallet detected in profile: {ProfileName}", profile.DisplayName);
        }

        var webDataPath = Path.Combine(profile.ProfilePath, "Web Data");
        var hasAutofillData = await IsValidSqliteDatabaseAsync(webDataPath);

        if (hasAutofillData)
        {
            cumulativeWeight += _options.AutofillWeight;
            findings.Add(new SecurityFinding(
                "Autofill Profiles & Credit Cards",
                "Autofill Profile Data",
                SecuritySeverity.Medium,
                "Autofill profile, addresses, and saved payment metadata database detected.",
                webDataPath
            ));
            _logger.LogWarning("Autofill data detected in profile: {ProfileName}", profile.DisplayName);
        }

        var sessionPaths = new[]
        {
            Path.Combine(profile.ProfilePath, "Sessions"),
            Path.Combine(profile.ProfilePath, "Session Storage"),
            Path.Combine(profile.ProfilePath, "IndexedDB"),
            Path.Combine(profile.ProfilePath, "Local Storage")
        };
        var hasSessions = sessionPaths.Any(path => _fileSystem.DirectoryExists(path));

        if (hasSessions)
        {
            cumulativeWeight += _options.SessionWeight;
            findings.Add(new SecurityFinding(
                "Active Session & Local Storage States",
                "Session & Local Storage Data",
                SecuritySeverity.Low,
                "Active login sessions and local databases detected.",
                profile.ProfilePath
            ));
            _logger.LogWarning("Session storage folders detected in profile: {ProfileName}", profile.DisplayName);
        }

        var extStoragePaths = new[]
        {
            Path.Combine(profile.ProfilePath, "Local Extension Settings"),
            Path.Combine(profile.ProfilePath, "Extension State")
        };
        var hasExtensionStorage = extStoragePaths.Any(path => _fileSystem.DirectoryExists(path));

        if (hasExtensionStorage)
        {
            cumulativeWeight += _options.ExtensionStorageWeight;
            findings.Add(new SecurityFinding(
                "Extension Local Cache Databases",
                "Extension Local Caches",
                SecuritySeverity.Info,
                "Installed extension storage state folders detected.",
                profile.ProfilePath
            ));
            _logger.LogWarning("Extension storage detected in profile: {ProfileName}", profile.DisplayName);
        }

        var historyPath = Path.Combine(profile.ProfilePath, "History");
        var hasHistory = await IsValidSqliteDatabaseAsync(historyPath);

        if (hasHistory)
        {
            cumulativeWeight += _options.HistoryWeight;
            findings.Add(new SecurityFinding(
                "Browsing History Trails",
                "Browsing History Trails",
                SecuritySeverity.Low,
                "Visited URLs and downloads database detected.",
                historyPath
            ));
            _logger.LogWarning("Browsing history detected in profile: {ProfileName}", profile.DisplayName);
        }

        var cachePaths = new[]
        {
            Path.Combine(profile.ProfilePath, "Cache"),
            Path.Combine(profile.ProfilePath, "Code Cache")
        };
        var hasCache = cachePaths.Any(path => _fileSystem.DirectoryExists(path));

        if (hasCache)
        {
            cumulativeWeight += _options.CacheWeight;
            findings.Add(new SecurityFinding(
                "Temporary Local Cache",
                "Local Cache Footprint",
                SecuritySeverity.Info,
                "Temporary internet files and code cache folders detected.",
                profile.ProfilePath
            ));
            _logger.LogWarning("Local cache folders detected in profile: {ProfileName}", profile.DisplayName);
        }

        var isRunning = _installationService.IsBraveRunning();
        if (isRunning)
        {
            cumulativeWeight += _options.BrowserRunningWeight;
            findings.Add(new SecurityFinding(
                "Brave Browser is Running",
                "Process State",
                SecuritySeverity.High,
                "Brave Browser is currently running. Active session data and credentials may be unlocked and vulnerable to local access.",
                "Running Process"
            ));
            _logger.LogWarning("Brave Browser is currently running during scan.");
        }

        var maxPossibleWeight = _options.PasswordWeight + _options.CookieWeight + _options.WalletWeight + _options.AutofillWeight + _options.SessionWeight + _options.ExtensionStorageWeight + _options.HistoryWeight + _options.CacheWeight + _options.BrowserRunningWeight;
        var riskScore = (int)Math.Round((double)cumulativeWeight / maxPossibleWeight * 100.0, MidpointRounding.AwayFromZero);

        var riskLevel = riskScore switch
        {
            <= 20 => RiskLevel.Low,
            <= 50 => RiskLevel.Medium,
            <= 80 => RiskLevel.High,
            _ => RiskLevel.Critical
        };

        var exportSafe = _safetyEvaluator.Evaluate(hasPasswords, hasCookies, hasWalletData);

        _logger.LogInformation("Security scan completed for profile: {ProfileName}. Risk score: {RiskScore} ({RiskLevel}). Export safe: {ExportSafe}.", profile.DisplayName, riskScore, riskLevel, exportSafe);

        return new SecurityScanResult(
            profile.DisplayName,
            profile.ProfilePath,
            DateTime.UtcNow,
            riskScore,
            riskLevel,
            findings,
            hasPasswords,
            hasCookies,
            hasWalletData,
            hasAutofillData,
            hasSessions,
            hasExtensionStorage,
            exportSafe
        );
    }

    private async Task<bool> IsValidSqliteDatabaseAsync(string path)
    {
        if (!_fileSystem.FileExists(path))
        {
            return false;
        }

        try
        {
            var bytes = await _fileSystem.ReadBytesAsync(path, 16);
            if (bytes.Length < 16)
            {
                return false;
            }

            var sqliteHeader = new byte[] { 0x53, 0x51, 0x4C, 0x69, 0x74, 0x65, 0x20, 0x66, 0x6F, 0x72, 0x6D, 0x61, 0x74, 0x20, 0x33, 0x00 };
            for (int i = 0; i < 16; i++)
            {
                if (bytes[i] != sqliteHeader[i])
                {
                    return false;
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
