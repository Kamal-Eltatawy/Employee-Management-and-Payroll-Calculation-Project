using Payroll.Domain.Common;

namespace Payroll.Application.Common;

/// <summary>Failed login or refresh. The message is always the same generic text (D25).</summary>
public sealed class UnauthorizedException() : Exception(InvalidCredentials), ILocalizableError
{
    public const string InvalidCredentials = "Invalid credentials.";

    public string? Code => ErrorCodes.InvalidCredentials;

    public IReadOnlyList<object> Arguments { get; } = [];
}
