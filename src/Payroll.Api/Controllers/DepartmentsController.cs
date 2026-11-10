using Microsoft.AspNetCore.Mvc;
using Payroll.Application.Departments;

namespace Payroll.Api.Controllers;

[ApiController]
[Route("api/departments")]
public sealed class DepartmentsController(IDepartmentService departments) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentResponse>>> List(CancellationToken ct) =>
        Ok(await departments.ListAsync(ct));
}
