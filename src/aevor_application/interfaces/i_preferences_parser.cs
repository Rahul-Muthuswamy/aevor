using Aevor.Core.Models;

namespace Aevor.Application.Interfaces;

public interface IPreferencesParser
{
    Task<BrowserSettings> ParseAsync(string filePath);
}
