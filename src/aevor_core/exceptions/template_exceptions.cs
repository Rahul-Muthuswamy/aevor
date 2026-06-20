using System;

namespace Aevor.Core.Exceptions;

public class TemplateException : AevorException
{
    public TemplateException(string message) : base(message) { }
    public TemplateException(string message, Exception innerException) : base(message, innerException) { }
}

public class TemplateValidationException : TemplateException
{
    public TemplateValidationException(string message) : base(message) { }
    public TemplateValidationException(string message, Exception innerException) : base(message, innerException) { }
}

public class TemplateSerializationException : TemplateException
{
    public TemplateSerializationException(string message) : base(message) { }
    public TemplateSerializationException(string message, Exception innerException) : base(message, innerException) { }
}

public class TemplateVersionException : TemplateException
{
    public TemplateVersionException(string message) : base(message) { }
    public TemplateVersionException(string message, Exception innerException) : base(message, innerException) { }
}
