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

    /// <summary>Creates a policy starting in the current month or later (past months are closed, D54).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<PayrollPolicyResponse>> Create(PayrollPolicyRequest request, CancellationToken ct)
    {
        var created = await policies.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Only a policy starting in the current month or later can be changed; otherwise 409.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<PayrollPolicyResponse>> Update(int id, PayrollPolicyRequest request, CancellationToken ct) =>
        Ok(await policies.UpdateAsync(id, request, ct));

    /// <summary>Only a policy starting in the current month or later can be deleted; otherwise 409.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await policies.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>The policy of a tax scheme in force for a month (yyyy-MM, default: the current month).</summary>
    [HttpGet("effective")]
    public async Task<ActionResult<PayrollPolicyResponse>> GetEffective(
        [FromQuery] int taxSchemeId, [FromQuery] string? month, CancellationToken ct) =>
        Ok(await policies.GetEffectiveAsync(taxSchemeId, month, ct));
}
