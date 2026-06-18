namespace Aevor.Application.Models;

public class SecurityScannerOptions
{
    // Weights reflect real-world sensitivity of each data type.
    // Kept deliberately LOW because every Brave profile ships with these
    // files as part of normal browser operation — their mere presence is
    // not itself an active threat.
    // Max possible = sum of all weights (35).
    public int PasswordWeight         { get; set; } = 5;   // Saved credentials
    public int CookieWeight           { get; set; } = 4;   // Session cookies
    public int WalletWeight           { get; set; } = 6;   // Crypto wallet
    public int AutofillWeight         { get; set; } = 3;   // Autofill / payment metadata
    public int SessionWeight          { get; set; } = 2;   // Live session tokens
    public int ExtensionStorageWeight { get; set; } = 1;   // Extension caches
    public int HistoryWeight          { get; set; } = 2;   // Browsing trail
    public int CacheWeight            { get; set; } = 1;   // Local cache footprint
    public int BrowserRunningWeight   { get; set; } = 11;  // Browser open = unlocked vault
}
