namespace Aevor.Infrastructure.Services;

public class ExportSafetyEvaluator
{
    public bool Evaluate(bool hasPasswords, bool hasCookies, bool hasWalletData)
    {
        return !hasPasswords && !hasCookies && !hasWalletData;
    }
}
