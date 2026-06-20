namespace Aevor.Application.Models;

public record DiscoveredSetting(
    string Browser,
    string Category,
    string JsonPath,
    string ValueDescription,
    DateTime DiscoveredAt
);
