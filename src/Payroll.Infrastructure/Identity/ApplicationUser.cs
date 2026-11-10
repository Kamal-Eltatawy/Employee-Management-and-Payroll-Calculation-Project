using Microsoft.AspNetCore.Identity;

namespace Payroll.Infrastructure.Identity;

/// <summary>Identity user (D24). Kept in Infrastructure so the Domain stays free of Identity.</summary>
public sealed class ApplicationUser : IdentityUser<int>
{
    /// <summary>Required for users in the Employee role: the employee whose payslip they may read.</summary>
    public int? EmployeeId { get; set; }
}
