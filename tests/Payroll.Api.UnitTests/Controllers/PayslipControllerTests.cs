using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Payroll.Api.Controllers;
using Payroll.Application.Payroll;
using Payroll.Domain.Payroll;

namespace Payroll.Api.UnitTests.Controllers;

public sealed class PayslipControllerTests
{
    [Fact]
    public async Task Returns_200_with_the_payslip()
    {
        var payslip = new PayslipResponse(7, "Ali Hassan", "2026-03", 1, new DateOnly(2025, 1, 1),
            "Standard", TaxMethod.Progressive, 1m, 31, 31, 17000m,
            [new("Transport", 1000m, false)], 20000m, 19000m, 1800m, 1500m, [new("Loan Repayment", 750m, false)],
            750m, 15950m, false);
        var service = Substitute.For<IPayslipService>();
        service.GetAsync(7, "2026-03", Arg.Any<CancellationToken>()).Returns(payslip);

        var result = await new PayslipController(service).Get(7, "2026-03", TestContext.Current.CancellationToken);

        Assert.Same(payslip, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task Effective_policy_returns_200()
    {
        var policy = new PayrollPolicyResponse(1, 1, new DateOnly(2025, 1, 1), 11m, 1500m, [new(null, 20m)], false);
        var service = Substitute.For<IPayrollPolicyService>();
        service.GetEffectiveAsync(1, "2026-03", Arg.Any<CancellationToken>()).Returns(policy);

        var result = await new PayrollPoliciesController(service).GetEffective(1, "2026-03", TestContext.Current.CancellationToken);

        Assert.Same(policy, Assert.IsType<OkObjectResult>(result.Result).Value);
    }
}
