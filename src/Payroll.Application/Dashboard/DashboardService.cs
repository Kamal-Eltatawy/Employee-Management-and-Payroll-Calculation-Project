using Payroll.Application.Abstractions;
using Payroll.Application.Common;
using Payroll.Domain.Payroll;

namespace Payroll.Application.Dashboard;

public sealed record CountItem(string Name, int Count);

/// <summary>The current month's payroll for every eligible employee, at full precision (D32).</summary>
public sealed record PayrollTotals(
    int EmployeesPaid, decimal GrossPay, decimal IncomeTax, decimal SocialInsurance, decimal OtherDeductions,
    decimal NetPay, int NegativeNetCount, int WithoutPolicy);

public sealed record DashboardResponse(
    string Month, int TotalEmployees, int ActiveEmployees, int InactiveEmployees, int HiredThisMonth,
    IReadOnlyList<CountItem> ByDepartment, IReadOnlyList<CountItem> ByTaxScheme, PayrollTotals Payroll);

public interface IDashboardService
{
    Task<DashboardResponse> GetAsync(CancellationToken ct);
}

/// <summary>
/// HR home page figures (D59). Counts come from the database (GROUP BY); the payroll totals reuse the real payslip
/// calculation for every Active employee hired on or before the current month, read in batches so memory stays flat (D62).
/// </summary>
public sealed class DashboardService(
    IEmployeeRepository employees,
    IPayrollPolicyRepository policies,
    IPayrollCalculator calculator,
    TimeProvider clock) : IDashboardService
{
    public const int BatchSize = 500;

    public async Task<DashboardResponse> GetAsync(CancellationToken ct)
    {
        var month = clock.CurrentMonth();
        var headcount = await employees.GetHeadcountAsync(month, ct);

        return new DashboardResponse(
            month.ToString(),
            headcount.Total,
            headcount.Active,
            headcount.Total - headcount.Active,
            headcount.HiredInMonth,
            Localize(headcount.ActiveByDepartment),
            Localize(headcount.ActiveByTaxScheme),
            await PayrollAsync(month, ct));
    }

    private static List<CountItem> Localize(IEnumerable<NamedCount> counts) =>
        counts.Select(c => new CountItem(LocalizedName.Pick(c.Name, c.NameAr), c.Count))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.CurrentCulture)
            .ToList();

    private async Task<PayrollTotals> PayrollAsync(PayrollMonth month, CancellationToken ct)
    {
        var policyByScheme = new Dictionary<int, PayrollPolicy?>();
        var totals = new PayrollTotals(0, 0, 0, 0, 0, 0, 0, 0);

        for (var afterId = 0; ; )
        {
            var batch = await employees.ListPayableBatchAsync(month, afterId, BatchSize, ct);
            foreach (var employee in batch)
            {
                if (!policyByScheme.TryGetValue(employee.TaxSchemeId, out var policy))
                {
                    policy = await policies.GetEffectiveForAsync(employee.TaxSchemeId, month, ct);
                    policyByScheme[employee.TaxSchemeId] = policy;
                }

                totals = policy is null
                    ? totals with { WithoutPolicy = totals.WithoutPolicy + 1 }
                    : Add(totals, calculator.Calculate(employee, policy, month));
            }

            if (batch.Count < BatchSize)
            {
                return totals;
            }

            afterId = batch[^1].Id;
        }
    }

    private static PayrollTotals Add(PayrollTotals totals, Payslip slip) => totals with
    {
        EmployeesPaid = totals.EmployeesPaid + 1,
        GrossPay = totals.GrossPay + slip.GrossPay,
        IncomeTax = totals.IncomeTax + slip.IncomeTax,
        SocialInsurance = totals.SocialInsurance + slip.SocialInsurance,
        OtherDeductions = totals.OtherDeductions + slip.OtherDeductionsTotal,
        NetPay = totals.NetPay + slip.NetPay,
        NegativeNetCount = totals.NegativeNetCount + (slip.IsNetNegative ? 1 : 0),
    };
}
