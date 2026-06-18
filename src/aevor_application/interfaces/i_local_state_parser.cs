using Aevor.Application.Models;

namespace Aevor.Application.Interfaces;

public interface ILocalStateParser
{
    Task<LocalStateMetadata> ParseAsync(string localStatePath);
}
