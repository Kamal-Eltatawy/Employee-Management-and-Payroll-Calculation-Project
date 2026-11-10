using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Payroll.Api.Localization;

namespace Payroll.Api.ErrorHandling;

/// <summary>
/// The single exception → RFC 7807 entry point (§9, D17). It picks the mapper registered for the exception's type
/// (or its closest base type); anything unmapped is a 500 that is logged with its traceId and leaks no details.
/// </summary>
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IEnumerable<IProblemMapper> mappers,
    IApiText text,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private readonly Dictionary<Type, IProblemMapper> _mappers = mappers.ToDictionary(mapper => mapper.ExceptionType);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = FindMapper(exception.GetType())?.Map(exception) ?? Unexpected();
        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            // Logged once, here, with the same traceId the client receives (§16).
            logger.LogError(exception, "Unhandled exception for {Method} {Path} (traceId {TraceId})",
                httpContext.Request.Method, httpContext.Request.Path,
                System.Diagnostics.Activity.Current?.Id ?? httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private IProblemMapper? FindMapper(Type? type)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (_mappers.TryGetValue(type, out var mapper))
            {
                return mapper;
            }
        }

        return null;
    }

    private ProblemDetails Unexpected() => new()
    {
        Status = StatusCodes.Status500InternalServerError,
        Title = text.Error("Title.Unexpected", "An unexpected error occurred."),
    };
}
