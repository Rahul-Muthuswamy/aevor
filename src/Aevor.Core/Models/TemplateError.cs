namespace Aevor.Core.Models;

public record TemplateError(
    string Message,
    string? Code = null
);
