using Microsoft.AspNetCore.Mvc;
using Payroll.Application.Payroll;

namespace Payroll.Api.Controllers;

[ApiController]
[Route("api/payroll-policies")]
public sealed class PayrollPoliciesController(IPayrollPolicyService policies) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PayrollPolicyResponse>>> List(CancellationToken ct) =>
        Ok(await policies.ListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PayrollPolicyResponse>> Get(int id, CancellationToken ct) =>
        Ok(await policies.GetAsync(id, ct));

    /// <summary>The policy of a tax scheme in force for a month (yyyy-MM, default: the current month).</summary>
    [HttpGet("effective")]
    public async Task<ActionResult<PayrollPolicyResponse>> GetEffective(
        [FromQuery] int taxSchemeId, [FromQuery] string? month, CancellationToken ct) =>
        Ok(await policies.GetEffectiveAsync(taxSchemeId, month, ct));
}
