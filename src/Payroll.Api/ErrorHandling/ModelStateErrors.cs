using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Payroll.Api.Localization;

namespace Payroll.Api.ErrorHandling;

/// <summary>
/// Model binding / JSON errors get the same shape as FluentValidation errors (§9): camelCase JSON-path keys
/// ("effectiveFrom", "taxBrackets[0].upperBound") and messages without internal type names.
/// </summary>
public static class ModelStateErrors
{
    public const string BodyKey = "body";

    /// <summary>"$.taxBrackets[0].upperBound" → "taxBrackets[0].upperBound"; "$" or "" → "body"; "FullName" → "fullName".</summary>
    public static string ToJsonPath(string modelStateKey)
    {
        var key = modelStateKey.StartsWith("$.", StringComparison.Ordinal) ? modelStateKey[2..] : modelStateKey;
        return key is "" or "$" ? BodyKey : JsonPath.ToCamelCase(key);
    }

    public static string Describe(IApiText text, string jsonPath, ModelError error)
    {
        if (error.Exception is JsonException || error.ErrorMessage.Contains("could not be converted", StringComparison.Ordinal))
        {
            return jsonPath == BodyKey
                ? text.Error("Model.InvalidJson", "The request body is not valid JSON.")
                : text.Error("Model.InvalidFormat",
                    "The value is not in a valid format (dates are yyyy-MM-dd, numbers use a dot for decimals).");
        }

        return string.IsNullOrWhiteSpace(error.ErrorMessage) ? text.Error("Model.Invalid", "The value is not valid.") : error.ErrorMessage;
    }

    public static IActionResult ToProblemResult(ActionContext context)
    {
        var text = context.HttpContext.RequestServices.GetRequiredService<IApiText>();
        var normalized = new ModelStateDictionary();
        foreach (var (key, entry) in context.ModelState)
        {
            var path = ToJsonPath(key);
            foreach (var error in entry.Errors)
            {
                var message = Describe(text, path, error);
                if (normalized[path]?.Errors.Any(e => e.ErrorMessage == message) != true)
                {
                    normalized.AddModelError(path, message);
                }
            }
        }

        // The factory applies AddApiProblemDetails' customisation, so the response also carries traceId.
        var problem = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateValidationProblemDetails(context.HttpContext, normalized, StatusCodes.Status400BadRequest,
                title: text.Error("Title.Validation", "One or more validation errors occurred."));

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }
}
