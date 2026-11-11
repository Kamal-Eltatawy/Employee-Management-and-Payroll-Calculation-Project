namespace Payroll.Domain.Common;

/// <summary>
/// Guard clauses. <c>field</c> is the request property name (e.g. "FullName"), so the API can translate it (D69).
/// </summary>
internal static class Guard
{
    /// <summary>Money and percentages are stored as decimal(18,2) / decimal(5,2); more decimals would be lost on save.</summary>
    public const int MaxMoneyDecimals = 2;

    public static string RequiredText(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException(DomainErrorCodes.Required, $"{field} is required.", field);
        }

        return trimmed.Length <= maxLength ? trimmed : throw TooLong(field, maxLength);
    }

    /// <summary>An optional text: trimmed, null when blank.</summary>
    public static string? OptionalText(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : throw TooLong(field, maxLength);
    }

    public static decimal Positive(decimal value, string field) =>
        value > 0
            ? MaxDecimals(value, field)
            : throw new DomainException(DomainErrorCodes.MustBePositive, $"{field} must be greater than zero.", field);

    public static int PositiveId(int value, string field) =>
        value > 0 ? value : throw new DomainException(DomainErrorCodes.Required, $"{field} is required.", field);

    public static TEnum Defined<TEnum>(TEnum value, string field) where TEnum : struct, Enum =>
        Enum.IsDefined(value) ? value : throw new DomainException(DomainErrorCodes.InvalidValue, $"{field} is not a valid value.", field);

    /// <summary>At most two decimal places, the precision the database keeps.</summary>
    public static decimal MaxDecimals(decimal value, string field) =>
        value == Math.Round(value, MaxMoneyDecimals)
            ? value
            : throw new DomainException(DomainErrorCodes.MaxDecimals,
                $"{field} can have at most {MaxMoneyDecimals} decimal places.", field, MaxMoneyDecimals);

    /// <summary>A percentage between 0 and 100 with at most two decimals.</summary>
    public static decimal Percent(decimal value, string field) =>
        value is >= 0 and <= 100
            ? MaxDecimals(value, field)
            : throw new DomainException(DomainErrorCodes.PercentRange, $"{field} must be between 0 and 100.", field);

    private static DomainException TooLong(string field, int maxLength) =>
        new(DomainErrorCodes.MaxLength, $"{field} must be at most {maxLength} characters.", field, maxLength);
}
