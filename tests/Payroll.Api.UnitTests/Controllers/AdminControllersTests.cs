using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Payroll.Api.Controllers;
using Payroll.Application.PayComponents;
using Payroll.Application.Payroll;
using Payroll.Domain.PayComponents;

namespace Payroll.Api.UnitTests.Controllers;

public sealed class AdminControllersTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Creating_a_pay_component_type_returns_201()
    {
        var request = new CreatePayComponentTypeRequest("Phone", PayComponentKind.Allowance, true);
        var created = new PayComponentTypeResponse(6, "Phone", PayComponentKind.Allowance, true);
        var service = Substitute.For<IPayComponentTypeService>();
        service.CreateAsync(request, Arg.Any<CancellationToken>()).Returns(created);

        var result = await new PayComponentTypesController(service).Create(request, Ct);

        var createdAt = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal((nameof(PayComponentTypesController.Get), 6), (createdAt.ActionName, createdAt.RouteValues!["id"]));
    }

    [Fact]
    public async Task Deleting_a_payroll_policy_returns_204()
    {
        var service = Substitute.For<IPayrollPolicyService>();

        Assert.IsType<NoContentResult>(await new PayrollPoliciesController(service).Delete(3, Ct));
    }

    [Fact]
    public async Task Creating_a_payroll_policy_returns_201()
    {
        var request = new PayrollPolicyRequest(1, new DateOnly(2026, 12, 1), 11m, 1500m, [new(null, 20m)]);
        var created = new PayrollPolicyResponse(5, 1, request.EffectiveFrom, 11m, 1500m, request.TaxBrackets, true);
        var service = Substitute.For<IPayrollPolicyService>();
        service.CreateAsync(request, Arg.Any<CancellationToken>()).Returns(created);

        var result = await new PayrollPoliciesController(service).Create(request, Ct);

        Assert.Same(created, Assert.IsType<CreatedAtActionResult>(result.Result).Value);
    }
}
