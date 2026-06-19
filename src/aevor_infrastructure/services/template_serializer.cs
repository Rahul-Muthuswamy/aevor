using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Aevor.Application.Interfaces;
using Aevor.Core.Exceptions;
using Aevor.Core.Models;

namespace Aevor.Infrastructure.Services;

public class TemplateSerializer : ITemplateSerializer
{
    private readonly IFileSystem _fileSystem;
    private readonly JsonSerializerOptions _jsonOptions;

    public TemplateSerializer(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }

    public string Serialize(AevorTemplate template, bool prettyPrint = true)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        try
        {
            var options = prettyPrint ? _jsonOptions : new JsonSerializerOptions(_jsonOptions) { WriteIndented = false };
            return JsonSerializer.Serialize(template, options);
        }
        catch (JsonException ex)
        {
            throw new TemplateSerializationException("Failed to serialize template to JSON string.", ex);
        }
    }

    public AevorTemplate Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new TemplateSerializationException("JSON string cannot be null or empty.");
        }

        try
        {

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Metadata", out var metadataProp) ||
                !metadataProp.TryGetProperty("TemplateVersion", out var versionProp))
            {
                throw new TemplateVersionException("Template version metadata is missing.");
            }

            var versionString = versionProp.GetString();
            if (string.IsNullOrEmpty(versionString))
            {
                throw new TemplateVersionException("Template version string is null or empty.");
            }

            TemplateVersion version;
            try
            {
                version = TemplateVersion.Parse(versionString);
            }
            catch (ArgumentException ex)
            {
                throw new TemplateVersionException($"Invalid template version format: {versionString}.", ex);
            }

            if (version.Major == 1 && version.Minor == 0)
            {
                var template = JsonSerializer.Deserialize<AevorTemplate>(json, _jsonOptions);
                if (template == null)
                {
                    throw new TemplateSerializationException("Deserialization returned null.");
                }
                return template;
            }

            throw new TemplateVersionException($"Unsupported template version: '{versionString}'. Currently only version 1.0 is supported.");
        }
        catch (JsonException ex)
        {
            throw new TemplateSerializationException("Failed to parse or deserialize template JSON.", ex);
        }
    }

    public async Task SaveToFileAsync(string filePath, AevorTemplate template, bool prettyPrint = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        var json = Serialize(template, prettyPrint);
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !_fileSystem.DirectoryExists(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }
            await _fileSystem.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            throw new TemplateSerializationException($"Failed to save template to file '{filePath}'.", ex);
        }
    }

    public async Task<AevorTemplate> LoadFromFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!_fileSystem.FileExists(filePath))
        {
            throw new TemplateSerializationException($"Template file not found at: {filePath}");
        }

        string json;
        try
        {
            json = await _fileSystem.ReadAllTextAsync(filePath);
        }
        catch (Exception ex)
        {
            throw new TemplateSerializationException($"Failed to read template file '{filePath}'.", ex);
        }

        return Deserialize(json);
    }
}
