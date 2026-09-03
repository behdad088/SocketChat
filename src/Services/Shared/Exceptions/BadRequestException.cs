namespace Shared.Exceptions;

public class BadRequestException : Exception
{
    protected BadRequestException(string message) : base(message)
    {
    }

    protected BadRequestException(string message, string details) : base(message)
    {
        Details = details;
    }

    public string? Details { get; }
}

public record InvalidPropertyResponse(string PropertyName, string Reason);