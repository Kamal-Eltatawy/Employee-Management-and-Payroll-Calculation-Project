using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Payroll.Api.Controllers;
using Payroll.Application.Common;
using Payroll.Application.Departments;
using Payroll.Application.Employees;
using Payroll.Domain.Employees;

namespace Payroll.Api.UnitTests.Controllers;

public sealed class EmployeesControllerTests
{
    private static readonly EmployeeResponse Ali = new(7, "Ali Hassan", "ali@demo.local", "Developer", 1, "Engineering",
        1, "Standard", new DateOnly(2024, 6, 1), 17000m, EmployeeStatus.Active, [], "AAAAAAAAB9E=");

    private readonly IEmployeeService _service = Substitute.For<IEmployeeService>();
    private readonly EmployeesController _controller;

    public EmployeesControllerTests() => _controller = new EmployeesController(_service);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_returns_200_with_the_employee()
    {
        _service.GetAsync(7, Arg.Any<CancellationToken>()).Returns(Ali);

        var result = await _controller.Get(7, Ct);

        Assert.Same(Ali, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task Create_returns_201_pointing_at_get()
    {
        var request = new CreateEmployeeRequest(Ali.FullName, Ali.Email, Ali.JobTitle, 1, 1, Ali.HireDate, 17000m,
            EmployeeStatus.Active, []);
        _service.CreateAsync(request, Arg.Any<CancellationToken>()).Returns(Ali);

        var result = await _controller.Create(request, Ct);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(EmployeesController.Get), created.ActionName);
        Assert.Equal(7, created.RouteValues!["id"]);
        Assert.Same(Ali, created.Value);
    }

    [Fact]
    public async Task Update_returns_200_with_the_new_row_version()
    {
        var request = new UpdateEmployeeRequest(Ali.FullName, Ali.Email, Ali.JobTitle, 1, 1, Ali.HireDate, 17000m,
            EmployeeStatus.Active, [], "AAAAAAAAB9E=");
        var updated = Ali with { RowVersion = "AAAAAAAAB9I=" };
        _service.UpdateAsync(7, request, Arg.Any<CancellationToken>()).Returns(updated);

        var result = await _controller.Update(7, request, Ct);

        Assert.Same(updated, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task Delete_returns_204()
    {
        var result = await _controller.Delete(7, Ct);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Search_passes_the_query_values_and_defaults_to_the_service()
    {
        var page = new PagedResult<EmployeeListItem>([], 1, 10, 0);
        _service.SearchAsync(new EmployeeSearchRequest("ali", 1, EmployeeStatus.Active, "salary", "desc", 1, 10),
            Arg.Any<CancellationToken>()).Returns(page);

        var result = await _controller.Search("ali", 1, EmployeeStatus.Active, "salary", "desc", Ct);

        Assert.Same(page, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task Departments_list_returns_200()
    {
        var service = Substitute.For<IDepartmentService>();
        IReadOnlyList<DepartmentResponse> departments = [new(1, "Engineering")];
        service.ListAsync(Arg.Any<CancellationToken>()).Returns(departments);

        var result = await new DepartmentsController(service).List(Ct);

        Assert.Same(departments, Assert.IsType<OkObjectResult>(result.Result).Value);
    }
}
