namespace Payroll.Application.Common;

/// <summary>Failed login. The message is always the same generic text (D25).</summary>
public sealed class UnauthorizedException() : Exception(InvalidCredentials)
{
    public const string InvalidCredentials = "Invalid credentials.";
}
