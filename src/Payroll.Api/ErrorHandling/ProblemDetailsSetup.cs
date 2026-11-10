using System.Diagnostics;
using Payroll.Api.Localization;

namespace Payroll.Api.ErrorHandling;

public static class ProblemDetailsSetup
{
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddSingleton<IApiText, ResxApiText>();
        services.AddSingleton<IProblemMapper, ValidationProblemMapper>();
        services.AddSingleton<IProblemMapper, NotFoundProblemMapper>();
        services.AddSingleton<IProblemMapper, ConflictProblemMapper>();
        services.AddSingleton<IProblemMapper, UnauthorizedProblemMapper>();
        services.AddSingleton<IProblemMapper, ForbiddenProblemMapper>();
        services.AddSingleton<IProblemMapper, DomainProblemMapper>();

        return services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
            context.ProblemDetails.Extensions["traceId"] =
                Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        });
    }
}
