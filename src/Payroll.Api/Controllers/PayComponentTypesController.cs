using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payroll.Api.Auth;
using Payroll.Application.PayComponents;

namespace Payroll.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.HrOnly)]
[Route("api/pay-component-types")]
public sealed class PayComponentTypesController(IPayComponentTypeService types) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PayComponentTypeResponse>>> List(CancellationToken ct) =>
        Ok(await types.ListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PayComponentTypeResponse>> Get(int id, CancellationToken ct) =>
        Ok(await types.GetAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<PayComponentTypeResponse>> Create(CreatePayComponentTypeRequest request, CancellationToken ct)
    {
        var created = await types.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PayComponentTypeResponse>> Update(int id, UpdatePayComponentTypeRequest request, CancellationToken ct) =>
        Ok(await types.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await types.DeleteAsync(id, ct);
        return NoContent();
    }
}
