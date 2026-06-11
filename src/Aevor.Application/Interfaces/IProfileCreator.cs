using System.Threading.Tasks;
using Aevor.Core.Models;

namespace Aevor.Application.Interfaces;

public interface IProfileCreator
{
    Task<ProfileCreationResult> CreateProfileAsync(ProfileCreationRequest request);
    Task<bool> DeleteProfileAsync(string folderName);
    Task<ProfileValidationResult> ValidateProfileAsync(string folderName);
}
