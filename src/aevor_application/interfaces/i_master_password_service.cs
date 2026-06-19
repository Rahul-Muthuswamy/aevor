namespace Aevor.Application.Interfaces;

public interface IMasterPasswordService
{

    bool IsPasswordConfigured();

    Task SetupPasswordAsync(string password);

    Task<bool> VerifyPasswordAsync(string password);

    void ClearSession();

    bool IsSessionActive { get; }
}
