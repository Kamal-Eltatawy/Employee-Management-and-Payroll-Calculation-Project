using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Payroll.Application.Common;
using Payroll.Domain.Common;

namespace Payroll.Api.ErrorHandling;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = ToProblem(exception);
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

    private static ProblemDetails ToProblem(Exception exception) => exception switch
    {
        ValidationException validation => new HttpValidationProblemDetails(ToFieldErrors(validation))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
        },
        NotFoundException notFound => Problem(StatusCodes.Status404NotFound, "Resource not found.", notFound.Message),
        ConflictException { Errors.Count: > 0 } conflict =>
            new HttpValidationProblemDetails(conflict.Errors.ToDictionary(e => e.Key, e => e.Value))
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict.",
                Detail = conflict.Message,
            },
        ConflictException conflict => Problem(StatusCodes.Status409Conflict, "Conflict.", conflict.Message),
        ForbiddenException forbidden => Problem(StatusCodes.Status403Forbidden, "Forbidden.", forbidden.Message),
        DomainException domain => Problem(StatusCodes.Status422UnprocessableEntity, "Business rule violated.", domain.Message),
        _ => Problem(StatusCodes.Status500InternalServerError, "An unexpected error occurred.", detail: null),
    };

    private static ProblemDetails Problem(int status, string title, string? detail) =>
        new() { Status = status, Title = title, Detail = detail };

    private static Dictionary<string, string[]> ToFieldErrors(ValidationException exception) =>
        exception.Errors
            .GroupBy(failure => JsonPath.ToCamelCase(failure.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray());
}
