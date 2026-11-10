using Mapster;
using Payroll.Application.Common;
using Payroll.Application.Departments;
using Payroll.Application.UnitTests.Fakes;
using Payroll.Domain.Departments;

namespace Payroll.Application.UnitTests.Common;

public sealed class MappingConfigTests
{
    [Fact]
    public void Configuration_compiles_with_every_destination_member_mapped()
    {
        var config = MappingConfig.Create();

        config.Compile();
    }

    [Fact]
    public async Task Department_service_lists_departments_by_name()
    {
        var db = new FakeDatabase();
        db.Departments.Seed(2, "Sales");
        db.Departments.Seed(1, "Engineering");
        var service = new DepartmentService(db.Departments, MappingConfig.Create());

        var departments = await service.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { new DepartmentResponse(1, "Engineering"), new DepartmentResponse(2, "Sales") }, departments.ToArray());
    }

    [Fact]
    public void Department_maps_to_response() =>
        Assert.Equal(new DepartmentResponse(3, "Finance"), new Department(3, "Finance").Adapt<DepartmentResponse>(MappingConfig.Create()));
}
