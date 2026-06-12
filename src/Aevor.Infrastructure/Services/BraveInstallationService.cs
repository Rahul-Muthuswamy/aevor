using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Aevor.Application.Interfaces;

namespace Aevor.Infrastructure.Services;

public class BraveInstallationService : IBraveInstallationService
{
    private readonly IFileSystem _fileSystem;

    public BraveInstallationService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public bool IsInstalled()
    {
        EnsureWindowsPlatform();
        var userDataPath = GetUserDataPath();
        return _fileSystem.DirectoryExists(userDataPath);
    }

    public string GetUserDataPath()
    {
        EnsureWindowsPlatform();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data");
    }

    public bool IsBraveRunning()
    {
        EnsureWindowsPlatform();
        return Process.GetProcessesByName("brave").Any();
    }

    private void EnsureWindowsPlatform()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Aevor is only supported on Windows.");
        }
    }
}
