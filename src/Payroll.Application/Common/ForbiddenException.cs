using Payroll.Domain.Common;

namespace Payroll.Application.Common;

public sealed class ForbiddenException(string message, string? code = null) : Exception(message), ILocalizableError
{
    public string? Code { get; } = code;

    public IReadOnlyList<object> Arguments { get; } = [];
}
