using Microsoft.AspNetCore.Mvc;
using Payroll.Application.Common;
using Payroll.Application.Employees;
using Payroll.Domain.Employees;

namespace Payroll.Api.Controllers;

[ApiController]
[Route("api/employees")]
public sealed class EmployeesController(IEmployeeService employees) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<EmployeeListItem>>> Search(
        [FromQuery] string? search,
        [FromQuery] int? departmentId,
        [FromQuery] EmployeeStatus? status,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10) =>
        Ok(await employees.SearchAsync(
            new EmployeeSearchRequest(search, departmentId, status, sortBy, sortDir, page, pageSize), ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeResponse>> Get(int id, CancellationToken ct) =>
        Ok(await employees.GetAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<EmployeeResponse>> Create(CreateEmployeeRequest request, CancellationToken ct)
    {
        var created = await employees.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmployeeResponse>> Update(int id, UpdateEmployeeRequest request, CancellationToken ct) =>
        Ok(await employees.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await employees.DeleteAsync(id, ct);
        return NoContent();
    }
}
