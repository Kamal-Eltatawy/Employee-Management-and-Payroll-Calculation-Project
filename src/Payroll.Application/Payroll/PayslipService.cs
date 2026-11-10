using Mapster;
using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.Common;
using Payroll.Domain.Employees;
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
            throw new ForbiddenException("You can only view your own payslip.", ErrorCodes.OwnPayslipOnly);
        }

        var employee = await employees.GetByIdAsync(employeeId, ct) ?? throw new NotFoundException("Employee", employeeId);
        employee.EnsureEligibleForPayslip(payrollMonth);

        var policy = await policies.GetEffectiveForAsync(employee.TaxSchemeId, payrollMonth, ct)
            ?? throw new DomainException(ErrorCodes.NoPolicyForMonth, $"No payroll policy is effective for {payrollMonth}.", payrollMonth.ToString());

        return Localize(calculator.Calculate(employee, policy, payrollMonth), employee).Adapt<PayslipResponse>(mapping);
    }

    /// <summary>Line and scheme names in the request language (D58), matched by pay component type id.</summary>
    private static Payslip Localize(Payslip payslip, Employee employee)
    {
        var names = employee.PayComponents
            .Where(c => c.PayComponentType is not null)
            .ToDictionary(c => c.PayComponentTypeId, c => LocalizedName.Pick(c.PayComponentType!.Name, c.PayComponentType.NameAr));
        IReadOnlyList<PayslipLine> Translate(IReadOnlyList<PayslipLine> lines) =>
            lines.Select(line => line with { Name = names.GetValueOrDefault(line.PayComponentTypeId, line.Name) }).ToList();

        return payslip with
        {
            TaxSchemeName = LocalizedName.Pick(employee.TaxScheme!.Name, employee.TaxScheme.NameAr),
            Allowances = Translate(payslip.Allowances),
            Deductions = Translate(payslip.Deductions),
        };
    }
}
