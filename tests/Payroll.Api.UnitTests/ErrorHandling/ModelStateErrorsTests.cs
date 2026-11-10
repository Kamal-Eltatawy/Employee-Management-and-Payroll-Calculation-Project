using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Payroll.Api.ErrorHandling;

namespace Payroll.Api.UnitTests.ErrorHandling;

public sealed class ModelStateErrorsTests
{
    [Theory]
    [InlineData("$.effectiveFrom", "effectiveFrom")]
    [InlineData("$.taxBrackets[0].upperBound", "taxBrackets[0].upperBound")]
    [InlineData("$", "body")]
    [InlineData("", "body")]
    [InlineData("FullName", "fullName")]
    public void Keys_become_camel_case_json_paths(string key, string expected) =>
        Assert.Equal(expected, ModelStateErrors.ToJsonPath(key));

    [Fact]
    public void Conversion_errors_do_not_leak_type_names()
    {
        var error = new ModelError(new JsonException("The JSON value could not be converted to Payroll.Application.Payroll.PayrollPolicyRequest."), "");

        var message = ModelStateErrors.Describe(new Payroll.Api.Localization.ResxApiText(), "effectiveFrom", error);

        Assert.DoesNotContain("Payroll.", message);
        Assert.Contains("yyyy-MM-dd", message);
    }

    [Fact]
    public void Invalid_json_body_has_its_own_message() =>
        Assert.Equal("The request body is not valid JSON.", ModelStateErrors.Describe(new Payroll.Api.Localization.ResxApiText(), "body", new ModelError(new JsonException("bad"), "")));

    [Fact]
    public void Other_messages_are_kept() =>
        Assert.Equal("The value 'abc' is not valid.", ModelStateErrors.Describe(new Payroll.Api.Localization.ResxApiText(), "page", new ModelError("The value 'abc' is not valid.")));
}
