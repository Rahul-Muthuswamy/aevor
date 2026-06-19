using Aevor.Core.Models;

namespace Aevor.Application.Interfaces;

public interface ITemplateValidator
{
    TemplateValidationResult Validate(AevorTemplate? template);
}
