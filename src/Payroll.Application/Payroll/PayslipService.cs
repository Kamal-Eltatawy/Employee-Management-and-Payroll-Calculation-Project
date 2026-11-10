using Mapster;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.Common;
using Payroll.Domain.Payroll;

namespace Payroll.Application.Payroll;

public interface IPayslipService
{
    Task<PayslipResponse> GetAsync(int employeeId, string? month, CancellationToken ct);
}

public sealed class PayslipService(
    IEmployeeRepository employees,
    IPayrollPolicyRepository policies,
    IPayrollCalculator calculator,
    ICurrentUser currentUser,
    TimeProvider clock,
    TypeAdapterConfig mapping) : IPayslipService
{
    /// <summary>Checks run in the order of spec S11. Future months are allowed as previews (D34).</summary>
    public async Task<PayslipResponse> GetAsync(int employeeId, string? month, CancellationToken ct)
    {
        var payrollMonth = MonthParameter.ParseOrCurrent(month, clock);

        // S7: Hr sees any payslip; an Employee only their own. Checked before existence, so ids cannot be probed.
        if (!currentUser.IsInRole(Roles.Hr) && currentUser.EmployeeId != employeeId)
        {
            throw new ForbiddenException("You can only view your own payslip.");
        }

        var employee = await employees.GetByIdAsync(employeeId, ct) ?? throw new NotFoundException("Employee", employeeId);
        employee.EnsureEligibleForPayslip(payrollMonth);

        var policy = await policies.GetEffectiveForAsync(employee.TaxSchemeId, payrollMonth, ct)
            ?? throw new DomainException($"No payroll policy is effective for {payrollMonth}.");

        return calculator.Calculate(employee, policy, payrollMonth).Adapt<PayslipResponse>(mapping);
    }
}
