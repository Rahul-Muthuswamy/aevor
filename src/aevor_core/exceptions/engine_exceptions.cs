using System;

namespace Aevor.Core.Exceptions;

public class ProfileCreationException : AevorException
{
    public ProfileCreationException(string message) : base(message) { }
    public ProfileCreationException(string message, Exception innerException) : base(message, innerException) { }
}

public class TemplateApplicationException : TemplateException
{
    public TemplateApplicationException(string message) : base(message) { }
    public TemplateApplicationException(string message, Exception innerException) : base(message, innerException) { }
}

public class CloneException : AevorException
{
    public CloneException(string message) : base(message) { }
    public CloneException(string message, Exception innerException) : base(message, innerException) { }
}
