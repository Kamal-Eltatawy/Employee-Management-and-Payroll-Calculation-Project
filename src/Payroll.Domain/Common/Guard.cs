namespace Payroll.Domain.Common;

internal static class Guard
{
    public static string RequiredText(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException($"{field} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{field} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    public static decimal Positive(decimal value, string field) =>
        value > 0 ? value : throw new DomainException($"{field} must be greater than zero.");

    public static int PositiveId(int value, string field) =>
        value > 0 ? value : throw new DomainException($"{field} is required.");

    public static TEnum Defined<TEnum>(TEnum value, string field) where TEnum : struct, Enum =>
        Enum.IsDefined(value) ? value : throw new DomainException($"{field} is not a valid value.");
}
