using Microsoft.AspNetCore.Mvc;
using Payroll.Application.Payroll;

namespace Payroll.Api.Controllers;

[ApiController]
[Route("api/tax-schemes")]
public sealed class TaxSchemesController(ITaxSchemeService schemes) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaxSchemeResponse>>> List(CancellationToken ct) =>
        Ok(await schemes.ListAsync(ct));
}
