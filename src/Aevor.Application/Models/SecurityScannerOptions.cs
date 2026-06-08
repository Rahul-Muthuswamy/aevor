namespace Aevor.Application.Models;

public class SecurityScannerOptions
{
    public int PasswordWeight { get; set; } = 30;
    public int CookieWeight { get; set; } = 25;
    public int WalletWeight { get; set; } = 30;
    public int AutofillWeight { get; set; } = 15;
    public int SessionWeight { get; set; } = 10;
    public int ExtensionStorageWeight { get; set; } = 10;
    public int HistoryWeight { get; set; } = 20;
    public int CacheWeight { get; set; } = 5;
}
