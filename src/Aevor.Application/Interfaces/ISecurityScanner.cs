using Aevor.Core.Models;

namespace Aevor.Application.Interfaces;

public interface ISecurityScanner
{
    Task<SecurityScanResult> ScanAsync(BraveProfile profile);
}
