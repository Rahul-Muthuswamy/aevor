namespace Aevor.Application.Models;

public class SecurityScannerOptions
{

    public int PasswordWeight         { get; set; } = 5;
    public int CookieWeight           { get; set; } = 4;
    public int WalletWeight           { get; set; } = 6;
    public int AutofillWeight         { get; set; } = 3;
    public int SessionWeight          { get; set; } = 2;
    public int ExtensionStorageWeight { get; set; } = 1;
    public int HistoryWeight          { get; set; } = 2;
    public int CacheWeight            { get; set; } = 1;
    public int BrowserRunningWeight   { get; set; } = 11;
}
