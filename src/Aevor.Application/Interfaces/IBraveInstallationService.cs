namespace Aevor.Application.Interfaces;

public interface IBraveInstallationService
{
    bool IsInstalled();
    string GetUserDataPath();
}
