using Payroll.Domain.Common;

namespace Payroll.Application.Common;

/// <summary>
/// Stable codes for user-facing errors (D56). The API translates them from its resource files
/// (Payroll.Api/Resources/ErrorMessages*.resx); the English message in code is the fallback.
/// </summary>
public static class ErrorCodes
{
    public const string InvalidCredentials = "Auth.InvalidCredentials";
    public const string OwnPayslipOnly = "Auth.OwnPayslipOnly";

    public const string EmailInUse = "Employee.EmailInUse";
    public const string Concurrency = "Conflict.Concurrency";
    public const string UniqueConflict = "Conflict.Unique";
    public const string EmployeeInactive = DomainErrorCodes.EmployeeInactive;
    public const string HiredAfterMonth = DomainErrorCodes.HiredAfterMonth;

    public const string DepartmentNotFound = "Validation.DepartmentNotFound";
    public const string TaxSchemeNotFound = "Validation.TaxSchemeNotFound";
    public const string PayComponentTypeNotFound = "Validation.PayComponentTypeNotFound";
    public const string DepartmentRequired = "Validation.DepartmentRequired";
    public const string TaxSchemeRequired = "Validation.TaxSchemeRequired";
    public const string PayComponentTypeRequired = "Validation.PayComponentTypeRequired";
    public const string DuplicatePayComponentType = DomainErrorCodes.DuplicatePayComponentType;
    public const string HireDateInFuture = DomainErrorCodes.HireDateInFuture;
    public const string RowVersionInvalid = "Validation.RowVersionInvalid";
    public const string SortByInvalid = "Validation.SortByInvalid";
    public const string SortDirInvalid = "Validation.SortDirInvalid";
    public const string MonthFormat = "Validation.MonthFormat";

    public const string DeductionNotTaxable = DomainErrorCodes.DeductionNotTaxable;
    public const string PayComponentTypeNameTaken = "PayComponentType.NameTaken";
    public const string PayComponentTypeInUse = "PayComponentType.InUse";

    public const string NoPolicyForMonth = "Policy.NoneForMonth";
    public const string PolicyFirstDay = DomainErrorCodes.PolicyFirstDay;
    public const string PolicyClosedMonth = DomainErrorCodes.PolicyClosedMonth;
    public const string PolicyCoversClosedMonths = DomainErrorCodes.PolicyCoversClosedMonths;
    public const string PolicyDateTaken = "Policy.DateTaken";
    public const string PolicyCannotMoveScheme = DomainErrorCodes.PolicyCannotMoveScheme;
    public const string BracketShape = DomainErrorCodes.BracketShape;
    public const string BracketLastOpen = DomainErrorCodes.BracketLastOpen;
    public const string BracketOnlyLastOpen = DomainErrorCodes.BracketOnlyLastOpen;
    public const string BracketAscending = DomainErrorCodes.BracketAscending;
}
