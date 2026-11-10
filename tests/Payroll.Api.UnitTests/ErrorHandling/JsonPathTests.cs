using Payroll.Api.ErrorHandling;

namespace Payroll.Api.UnitTests.ErrorHandling;

public sealed class JsonPathTests
{
    [Theory]
    [InlineData("FullName", "fullName")]
    [InlineData("PayComponents[0].Amount", "payComponents[0].amount")]
    [InlineData("email", "email")]
    [InlineData("Id", "id")]
    [InlineData("", "")]
    public void ToCamelCase_lower_cases_the_first_letter_of_every_segment(string input, string expected)
    {
        Assert.Equal(expected, JsonPath.ToCamelCase(input));
    }
}
