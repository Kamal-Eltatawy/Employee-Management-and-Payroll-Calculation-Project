namespace Payroll.Domain.Common;

/// <summary>Codes of the domain errors the API translates (D56). The single source; Application's ErrorCodes reuses them.</summary>
public static class DomainErrorCodes
{
    public const string EmployeeInactive = "Employee.Inactive";
    public const string HiredAfterMonth = "Employee.HiredAfterMonth";
    public const string PolicyCoversClosedMonths = "Policy.CoversClosedMonths";
}
