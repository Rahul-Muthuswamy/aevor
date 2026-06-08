namespace Aevor.Core.Exceptions;

public class AevorException : Exception
{
    public AevorException(string message) : base(message) { }
    public AevorException(string message, Exception innerException) : base(message, innerException) { }
}

public class BraveNotInstalledException : AevorException
{
    public BraveNotInstalledException(string message) : base(message) { }
}

public class MissingLocalStateFileException : AevorException
{
    public MissingLocalStateFileException(string message) : base(message) { }
}

public class InvalidLocalStateJsonException : AevorException
{
    public InvalidLocalStateJsonException(string message, Exception innerException) : base(message, innerException) { }
}

public class ProfileFolderNotFoundException : AevorException
{
    public ProfileFolderNotFoundException(string message) : base(message) { }
}
