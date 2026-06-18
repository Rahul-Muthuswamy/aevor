using System.Text.Json.Serialization;

namespace Aevor.Application.Models;

public record LocalStateProfileMetadata(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("avatar_icon")] string? AvatarIcon
);

public record LocalStateProfileData(
    [property: JsonPropertyName("info_cache")] Dictionary<string, LocalStateProfileMetadata> InfoCache,
    [property: JsonPropertyName("last_used")] string? LastUsed
);

public record LocalStateMetadata(
    [property: JsonPropertyName("profile")] LocalStateProfileData Profile
);
