namespace Aevor.Core.Exceptions;

public class ProfileAnalysisException : AevorException
{
    public ProfileAnalysisException(string message) : base(message) { }
    public ProfileAnalysisException(string message, Exception innerException) : base(message, innerException) { }
}

public class PreferencesFileNotFoundException : ProfileAnalysisException
{
    public PreferencesFileNotFoundException(string message) : base(message) { }
}

public class SecurePreferencesFileNotFoundException : ProfileAnalysisException
{
    public SecurePreferencesFileNotFoundException(string message) : base(message) { }
}

public class InvalidPreferencesJsonException : ProfileAnalysisException
{
    public InvalidPreferencesJsonException(string message, Exception innerException) : base(message, innerException) { }
}

public class InvalidSecurePreferencesJsonException : ProfileAnalysisException
{
    public InvalidSecurePreferencesJsonException(string message, Exception innerException) : base(message, innerException) { }
}

public class ProfileAccessDeniedException : ProfileAnalysisException
{
    public ProfileAccessDeniedException(string message, Exception innerException) : base(message, innerException) { }
}

public class CorruptedProfileException : ProfileAnalysisException
{
    public CorruptedProfileException(string message) : base(message) { }
}
