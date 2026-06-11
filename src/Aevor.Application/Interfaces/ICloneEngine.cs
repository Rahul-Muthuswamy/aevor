using System.Threading.Tasks;
using Aevor.Core.Models;

namespace Aevor.Application.Interfaces;

public interface ICloneEngine
{
    Task<CloneResult> CloneProfileAsync(CloneRequest request);
    Task<CloneValidationResult> ValidateCloneAsync(string sourceFolderName, string destFolderName);
    Task<ClonePreview> PreviewCloneAsync(CloneRequest request);
}
