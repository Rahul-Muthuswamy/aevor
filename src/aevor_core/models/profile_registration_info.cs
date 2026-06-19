using System;

namespace Aevor.Core.Models;

public record ProfileRegistrationInfo(
    string FolderName,
    string DisplayName,
    string ProfilePath,
    DateTime RegisteredAt
);
