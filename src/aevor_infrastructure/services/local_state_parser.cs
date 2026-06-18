using System.Text.Json;
using Aevor.Application.Interfaces;
using Aevor.Application.Models;
using Aevor.Core.Exceptions;

namespace Aevor.Infrastructure.Services;

public class LocalStateParser : ILocalStateParser
{
    private readonly IFileSystem _fileSystem;

    public LocalStateParser(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async Task<LocalStateMetadata> ParseAsync(string localStatePath)
    {
        if (!_fileSystem.FileExists(localStatePath))
        {
            throw new MissingLocalStateFileException($"Local State file not found at: {localStatePath}");
        }

        try
        {
            var jsonContent = await _fileSystem.ReadAllTextAsync(localStatePath);
            var metadata = JsonSerializer.Deserialize<LocalStateMetadata>(jsonContent);
            if (metadata == null || metadata.Profile == null || metadata.Profile.InfoCache == null)
            {
                throw new InvalidLocalStateJsonException("Invalid Local State JSON structure.", null!);
            }
            return metadata;
        }
        catch (JsonException ex)
        {
            throw new InvalidLocalStateJsonException("Failed to parse Local State JSON.", ex);
        }
    }
}
