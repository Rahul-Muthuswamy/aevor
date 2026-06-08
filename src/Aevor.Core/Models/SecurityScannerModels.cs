namespace Aevor.Core.Models;

public enum SecuritySeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public record SecurityFinding(
    string Name,
    string Category,
    SecuritySeverity Severity,
    string Description,
    string Path
);

public record SecurityScanResult(
    string ProfileName,
    string ProfilePath,
    DateTime ScanTimestamp,
    int RiskScore,
    RiskLevel RiskLevel,
    IReadOnlyList<SecurityFinding> Findings,
    bool HasPasswords,
    bool HasCookies,
    bool HasWalletData,
    bool HasAutofillData,
    bool HasSessions,
    bool HasExtensionStorage,
    bool ExportSafe
);
