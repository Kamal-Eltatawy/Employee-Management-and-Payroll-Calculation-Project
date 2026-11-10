namespace Payroll.Domain.Common;

/// <summary>An error the API can translate (D56): a stable code plus arguments; the message is the English fallback.</summary>
public interface ILocalizableError
{
    string? Code { get; }

    IReadOnlyList<object> Arguments { get; }
}

public class DomainException : Exception, ILocalizableError
{
    public DomainException(string message)
        : base(message)
    {
        Arguments = [];
    }

    public DomainException(string code, string message, params object[] arguments)
        : base(message)
    {
        Code = code;
        Arguments = arguments;
    }

    public string? Code { get; }

    public IReadOnlyList<object> Arguments { get; }
}
