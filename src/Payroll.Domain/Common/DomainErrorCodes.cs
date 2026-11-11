namespace Payroll.Domain.Common;

/// <summary>
/// Codes of the domain errors the API translates (D56). The single source; Application's ErrorCodes reuses them.
/// Every rule a user can reach carries a code, so the Arabic UI never shows the English fallback (D69).
/// Arguments that name a field use the request property name (e.g. "FullName"); the API translates them.
/// </summary>
public static class DomainErrorCodes
{
    // Generic guards. Argument 0 is the field; MaxLength and MaxDecimals add the limit as argument 1.
    public const string Required = "Domain.Required";
    public const string MaxLength = "Domain.MaxLength";
    public const string MustBePositive = "Domain.MustBePositive";
    public const string InvalidValue = "Domain.InvalidValue";
    public const string MaxDecimals = "Domain.MaxDecimals";
    public const string PercentRange = "Domain.PercentRange";
    public const string InvalidMonth = "Domain.InvalidMonth";

    public const string EmployeeInactive = "Employee.Inactive";
    public const string HiredAfterMonth = "Employee.HiredAfterMonth";
    public const string EmployeeDeleted = "Employee.Deleted";
    public const string HireDateInFuture = "Validation.HireDateInFuture";
    public const string DuplicatePayComponentType = "Validation.DuplicatePayComponentType";

    public const string DeductionNotTaxable = "PayComponentType.DeductionNotTaxable";

    public const string PolicyCoversClosedMonths = "Policy.CoversClosedMonths";
    public const string PolicyFirstDay = "Policy.FirstDayOfMonth";
    public const string PolicyClosedMonth = "Policy.ClosedMonth";
    public const string PolicyCannotMoveScheme = "Policy.CannotMoveScheme";
    public const string InsuranceCapNegative = "Policy.InsuranceCapNegative";

    /// <summary>Suffixed with the tax method, e.g. "Policy.BracketShape.Flat".</summary>
    public const string BracketShape = "Policy.BracketShape";
    public const string BracketLastOpen = "Policy.BracketLastOpen";
    public const string BracketOnlyLastOpen = "Policy.BracketOnlyLastOpen";
    public const string BracketAscending = "Policy.BracketAscending";
}
