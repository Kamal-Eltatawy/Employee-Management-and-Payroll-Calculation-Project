using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payroll.Api.Auth;
using Payroll.Application.Dashboard;

namespace Payroll.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.HrOnly)]
[Route("api/dashboard")]
public sealed class DashboardController(IDashboardService dashboard) : ControllerBase
{
    /// <summary>HR home page: head counts and the current month's payroll totals (D59).</summary>
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken ct) => Ok(await dashboard.GetAsync(ct));
}
