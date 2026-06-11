using System.Threading.Tasks;
using Aevor.Core.Models;

namespace Aevor.Application.Interfaces;

public interface ITemplateApplier
{
    Task<TemplateApplicationResult> ApplyTemplateAsync(AevorTemplate template, BraveProfile profile);
    Task<TemplateApplicationValidationResult> ValidateApplicationAsync(AevorTemplate template, BraveProfile profile);
    Task<TemplateApplicationPreview> PreviewChangesAsync(AevorTemplate template, BraveProfile profile);
}
