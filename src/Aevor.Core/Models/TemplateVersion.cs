using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aevor.Core.Models;

[JsonConverter(typeof(TemplateVersionJsonConverter))]
public record TemplateVersion(int Major, int Minor) : IComparable<TemplateVersion>
{
    public static readonly TemplateVersion V1_0 = new(1, 0);

    public int CompareTo(TemplateVersion? other)
    {
        if (other is null) return 1;
        var majorCompare = Major.CompareTo(other.Major);
        return majorCompare != 0 ? majorCompare : Minor.CompareTo(other.Minor);
    }

    public override string ToString() => $"{Major}.{Minor}";

    public static TemplateVersion Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Version string cannot be empty.", nameof(version));
        }

        var parts = version.Split('.');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
        {
            throw new ArgumentException($"Invalid version format: '{version}'. Expected format 'Major.Minor'.", nameof(version));
        }

        return new TemplateVersion(major, minor);
    }
}

public class TemplateVersionJsonConverter : JsonConverter<TemplateVersion>
{
    public override TemplateVersion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value == null) return null;
        try
        {
            return TemplateVersion.Parse(value);
        }
        catch (Exception ex)
        {
            throw new JsonException("Failed to parse TemplateVersion", ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, TemplateVersion value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
