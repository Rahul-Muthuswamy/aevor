using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aevor.Core.Models;

namespace Aevor.Application.Interfaces;

public interface IBackupService
{
    Task<BackupResult> CreateBackupAsync(BraveProfile profile);
    Task<RestoreResult> RestoreBackupAsync(Guid backupId);
    Task<List<BackupMetadata>> GetBackupsAsync();
    Task<bool> DeleteBackupAsync(Guid backupId);
    Task<BackupValidationResult> ValidateBackupAsync(Guid backupId);
}
