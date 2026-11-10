using Payroll.Domain.Common;

namespace Payroll.Application.Common;

public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} {key} was not found."), ILocalizableError
{
    /// <summary>"Pay component type" → "NotFound.PayComponentType".</summary>
    public string? Code { get; } = "NotFound." + string.Concat(
        resource.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    public IReadOnlyList<object> Arguments { get; } = [key];
}
