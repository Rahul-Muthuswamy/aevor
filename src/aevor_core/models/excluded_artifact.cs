namespace Aevor.Core.Models;

public record ExcludedArtifact(
    string Name,
    string Path,
    string Reason
);
