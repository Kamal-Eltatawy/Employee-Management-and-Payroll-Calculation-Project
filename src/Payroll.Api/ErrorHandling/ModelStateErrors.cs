using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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

    public static string Describe(string jsonPath, ModelError error)
    {
        if (error.Exception is JsonException || error.ErrorMessage.Contains("could not be converted", StringComparison.Ordinal))
        {
            return jsonPath == BodyKey
                ? "The request body is not valid JSON."
                : "The value is not in a valid format (dates are yyyy-MM-dd, numbers use a dot for decimals).";
        }

        return string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The value is not valid." : error.ErrorMessage;
    }

    public static IActionResult ToProblemResult(ActionContext context)
    {
        var normalized = new ModelStateDictionary();
        foreach (var (key, entry) in context.ModelState)
        {
            var path = ToJsonPath(key);
            foreach (var error in entry.Errors)
            {
                var message = Describe(path, error);
                if (normalized[path]?.Errors.Any(e => e.ErrorMessage == message) != true)
                {
                    normalized.AddModelError(path, message);
                }
            }
        }

        // The factory applies AddApiProblemDetails' customisation, so the response also carries traceId.
        var problem = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateValidationProblemDetails(context.HttpContext, normalized, StatusCodes.Status400BadRequest,
                title: "One or more validation errors occurred.");

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }
}
