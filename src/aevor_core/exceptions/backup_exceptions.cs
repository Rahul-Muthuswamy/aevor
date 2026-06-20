using System;

namespace Aevor.Core.Exceptions;

public class BackupException : AevorException
{
    public BackupException(string message) : base(message) { }
    public BackupException(string message, Exception innerException) : base(message, innerException) { }
}

public class BackupValidationException : BackupException
{
    public BackupValidationException(string message) : base(message) { }
    public BackupValidationException(string message, Exception innerException) : base(message, innerException) { }
}

public class BackupRestoreException : BackupException
{
    public BackupRestoreException(string message) : base(message) { }
    public BackupRestoreException(string message, Exception innerException) : base(message, innerException) { }
}

public class BackupCorruptionException : BackupException
{
    public BackupCorruptionException(string message) : base(message) { }
    public BackupCorruptionException(string message, Exception innerException) : base(message, innerException) { }
}
