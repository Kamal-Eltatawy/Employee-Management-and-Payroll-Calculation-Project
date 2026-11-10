using Mapster;
using Payroll.Application.Departments;
using Payroll.Application.Employees;
using Payroll.Domain.Departments;
using Payroll.Domain.Employees;

namespace Payroll.Application.Common;

/// <summary>Entity → response mappings only (D20). Validated by a unit test.</summary>
public static class MappingConfig
{
    public static TypeAdapterConfig Create()
    {
        var config = new TypeAdapterConfig { RequireDestinationMemberSource = true };

        config.NewConfig<Employee, EmployeeResponse>()
            .Map(dest => dest.DepartmentName, src => src.Department!.Name)
            .Map(dest => dest.RowVersion, src => Convert.ToBase64String(src.RowVersion));

        config.NewConfig<Employee, EmployeeListItem>()
            .Map(dest => dest.DepartmentName, src => src.Department!.Name);

        config.NewConfig<EmployeePayComponent, EmployeePayComponentResponse>()
            .Map(dest => dest.Name, src => src.PayComponentType!.Name)
            .Map(dest => dest.Kind, src => src.PayComponentType!.Kind)
            .Map(dest => dest.IsTaxable, src => src.PayComponentType!.IsTaxable);

        config.NewConfig<Department, DepartmentResponse>();

        return config;
    }
}
