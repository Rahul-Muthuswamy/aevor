namespace Aevor.Core.Exceptions;

public class SecurityScanException : AevorException
{
    public SecurityScanException(string message) : base(message) { }
    public SecurityScanException(string message, Exception innerException) : base(message, innerException) { }
}
