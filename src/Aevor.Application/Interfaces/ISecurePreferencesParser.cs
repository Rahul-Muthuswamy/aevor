using Aevor.Core.Models;

namespace Aevor.Application.Interfaces;

public interface ISecurePreferencesParser
{
    Task<BrowserSettings> ParseAsync(string filePath);
}
