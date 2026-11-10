using Microsoft.AspNetCore.Mvc;
using Payroll.Application.Payroll;

namespace Payroll.Api.Controllers;

[ApiController]
[Route("api/employees/{id:int}/payslip")]
public sealed class PayslipController(IPayslipService payslips) : ControllerBase
{
    /// <summary>Payslip for a month (yyyy-MM). Defaults to the current month. Future months are previews.</summary>
    [HttpGet]
    public async Task<ActionResult<PayslipResponse>> Get(int id, [FromQuery] string? month, CancellationToken ct) =>
        Ok(await payslips.GetAsync(id, month, ct));
}
