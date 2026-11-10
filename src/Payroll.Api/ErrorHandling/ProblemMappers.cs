using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Payroll.Api.Localization;
using Payroll.Application.Common;
using Payroll.Domain.Common;

namespace Payroll.Api.ErrorHandling;

/// <summary>
/// Turns one exception type into RFC 7807 Problem Details (§9). Each mapper is registered in DI, so a new error kind
/// is a new class, not an edit of the handler (OCP, D62).
/// </summary>
public interface IProblemMapper
{
    Type ExceptionType { get; }

    ProblemDetails Map(Exception exception);
}

public abstract class ProblemMapper<TException>(IApiText text) : IProblemMapper
    where TException : Exception
{
    public Type ExceptionType => typeof(TException);

    protected IApiText Text => text;

    public ProblemDetails Map(Exception exception) => Map((TException)exception);

    protected abstract ProblemDetails Map(TException exception);

    /// <summary>The translated message when the error carries a known code (D56); otherwise the English message.</summary>
    protected string Detail(Exception exception) =>
        exception is ILocalizableError localizable
            ? text.Error(localizable.Code, localizable.Arguments, exception.Message)
            : exception.Message;

    protected ProblemDetails Problem(int status, string titleCode, string titleFallback, string? detail) =>
        new() { Status = status, Title = text.Error(titleCode, titleFallback), Detail = detail };
}

public sealed class ValidationProblemMapper(IApiText text) : ProblemMapper<ValidationException>(text)
{
    /// <summary>Our own codes are translated here; FluentValidation's built-in messages are already in the request language.</summary>
    protected override ProblemDetails Map(ValidationException exception) =>
        new HttpValidationProblemDetails(exception.Errors
            .GroupBy(failure => JsonPath.ToCamelCase(failure.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => Text.Error(failure.ErrorCode, [], failure.ErrorMessage)).ToArray()))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = Text.Error("Title.Validation", "One or more validation errors occurred."),
        };
}

public sealed class NotFoundProblemMapper(IApiText text) : ProblemMapper<NotFoundException>(text)
{
    protected override ProblemDetails Map(NotFoundException exception) =>
        Problem(StatusCodes.Status404NotFound, "Title.NotFound", "Resource not found.", Detail(exception));
}

public sealed class ConflictProblemMapper(IApiText text) : ProblemMapper<ConflictException>(text)
{
    /// <summary>A conflict about a field (duplicate email or name) also carries <c>errors</c>, so the client can show it on the field (D42).</summary>
    protected override ProblemDetails Map(ConflictException exception)
    {
        var detail = Detail(exception);
        if (exception.Errors.Count == 0)
        {
            return Problem(StatusCodes.Status409Conflict, "Title.Conflict", "Conflict.", detail);
        }

        return new HttpValidationProblemDetails(exception.Errors.ToDictionary(
            error => error.Key,
            error => exception.Code is null ? error.Value : [detail]))
        {
            Status = StatusCodes.Status409Conflict,
            Title = Text.Error("Title.Conflict", "Conflict."),
            Detail = detail,
        };
    }
}

public sealed class UnauthorizedProblemMapper(IApiText text) : ProblemMapper<UnauthorizedException>(text)
{
    protected override ProblemDetails Map(UnauthorizedException exception) =>
        Problem(StatusCodes.Status401Unauthorized, "Title.Unauthorized", "Unauthorized.", Detail(exception));
}

public sealed class ForbiddenProblemMapper(IApiText text) : ProblemMapper<ForbiddenException>(text)
{
    protected override ProblemDetails Map(ForbiddenException exception) =>
        Problem(StatusCodes.Status403Forbidden, "Title.Forbidden", "Forbidden.", Detail(exception));
}

public sealed class DomainProblemMapper(IApiText text) : ProblemMapper<DomainException>(text)
{
    protected override ProblemDetails Map(DomainException exception) =>
        Problem(StatusCodes.Status422UnprocessableEntity, "Title.BusinessRule", "Business rule violated.", Detail(exception));
}
