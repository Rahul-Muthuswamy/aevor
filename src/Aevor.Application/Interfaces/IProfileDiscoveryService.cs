using Aevor.Core.Models;

namespace Aevor.Application.Interfaces;

public interface IProfileDiscoveryService
{
    Task<List<BraveProfile>> GetProfilesAsync();
}
