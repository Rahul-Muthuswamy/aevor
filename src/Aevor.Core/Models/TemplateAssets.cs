using System.Collections.Generic;

namespace Aevor.Core.Models;

public record TemplateAssets(
    string? Wallpaper = null,
    string? Icon = null,
    IReadOnlyDictionary<string, string>? FutureAssets = null
);
