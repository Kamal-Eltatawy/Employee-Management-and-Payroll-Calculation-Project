using System.Globalization;
using System.Reflection;
using Payroll.Api.Localization;
using Payroll.Application.Common;
using Payroll.Domain.Common;
using Payroll.Domain.Payroll;

namespace Payroll.Api.UnitTests.Localization;

public sealed class ApiTextTests
{
    private static readonly ResxApiText Text = new();

    private static IEnumerable<string> AllCodes()
    {
        var codes = typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Concat(typeof(DomainErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetValue(null)!)
            .Where(code => code != ErrorCodes.BracketShape)
            .Distinct()
            .ToList();
        codes.AddRange(Enum.GetNames<TaxMethod>().Select(method => $"{ErrorCodes.BracketShape}.{method}"));
        codes.AddRange(["NotFound.Employee", "NotFound.PayComponentType", "NotFound.PayrollPolicy"]);
        return codes;
    }

    private static T InCulture<T>(string culture, Func<T> action)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Every_error_code_has_an_english_and_an_arabic_text()
    {
        foreach (var code in AllCodes())
        {
            var english = InCulture("en", () => Text.Error(code, ["x", "y"], "MISSING"));
            var arabic = InCulture("ar", () => Text.Error(code, ["x", "y"], "MISSING"));

            Assert.True(english != "MISSING", $"No English text for {code}.");
            Assert.True(arabic != "MISSING" && arabic != english, $"No Arabic text for {code}.");
        }
    }

    [Fact]
    public void Arguments_are_formatted_into_the_text() =>
        Assert.Equal("Employee was hired in 2026-03.", InCulture("en", () => Text.Error("Employee.HiredAfterMonth", ["2026-03"], "x")));

    [Fact]
    public void Unknown_code_falls_back_to_the_given_message() =>
        Assert.Equal("fallback", Text.Error("No.Such.Code", [], "fallback"));

    [Fact]
    public void Field_names_are_translated()
    {
        Assert.Equal("Full name", InCulture("en", () => Text.FieldName("FullName")));
        Assert.Equal("الاسم الكامل", InCulture("ar", () => Text.FieldName("FullName")));
        Assert.Null(Text.FieldName("NoSuchField"));
        Assert.Equal("الاسم بالعربية", InCulture("ar", () => Text.FieldName("NameAr")));
    }
}
