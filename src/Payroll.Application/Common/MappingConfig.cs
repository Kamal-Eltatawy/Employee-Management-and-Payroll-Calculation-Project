using Mapster;
using Payroll.Application.Departments;
using Payroll.Application.Employees;
using Payroll.Application.PayComponents;
using Payroll.Application.Payroll;
using Payroll.Domain.Departments;
using Payroll.Domain.Employees;
using Payroll.Domain.PayComponents;
using Payroll.Domain.Payroll;

namespace Payroll.Application.Common;

/// <summary>Entity → response mappings only (D20). Validated by a unit test.</summary>
public static class MappingConfig
{
    public static TypeAdapterConfig Create()
    {
        var config = new TypeAdapterConfig { RequireDestinationMemberSource = true };

        config.NewConfig<Employee, EmployeeResponse>()
            .Map(dest => dest.DepartmentName, src => LocalizedName.Pick(src.Department!.Name, src.Department!.NameAr))
            .Map(dest => dest.TaxSchemeName, src => LocalizedName.Pick(src.TaxScheme!.Name, src.TaxScheme!.NameAr))
            .Map(dest => dest.RowVersion, src => Convert.ToBase64String(src.RowVersion));

        config.NewConfig<Employee, EmployeeListItem>()
            .Map(dest => dest.DepartmentName, src => LocalizedName.Pick(src.Department!.Name, src.Department!.NameAr));

        config.NewConfig<EmployeePayComponent, EmployeePayComponentResponse>()
            .Map(dest => dest.Name, src => LocalizedName.Pick(src.PayComponentType!.Name, src.PayComponentType!.NameAr))
            .Map(dest => dest.Kind, src => src.PayComponentType!.Kind)
            .Map(dest => dest.IsTaxable, src => src.PayComponentType!.IsTaxable);

        config.NewConfig<Department, DepartmentResponse>()
            .Map(dest => dest.Name, src => LocalizedName.Pick(src.Name, src.NameAr));
        config.NewConfig<PayComponentType, PayComponentTypeResponse>()
            .Map(dest => dest.Name, src => LocalizedName.Pick(src.Name, src.NameAr))
            .Map(dest => dest.NameEn, src => src.Name);

        config.NewConfig<PayslipLine, PayslipLineResponse>();
        config.NewConfig<Payslip, PayslipResponse>()
            .Map(dest => dest.Month, src => src.Month.ToString())
            .Map(dest => dest.ProrationFactor, src => src.Proration.Factor)
            .Map(dest => dest.DaysPaid, src => src.Proration.DaysPaid)
            .Map(dest => dest.DaysInMonth, src => src.Proration.DaysInMonth);

        return config;
    }
}
