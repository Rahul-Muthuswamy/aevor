namespace Aevor.Core.Models;

public record ProfileCreationResult(
    bool IsSuccess,
    BraveProfile? Profile,
    string? ErrorMessage,
    ProfileRegistrationInfo? Registration = null
);
