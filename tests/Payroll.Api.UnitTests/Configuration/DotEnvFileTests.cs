using Payroll.Api.Configuration;

namespace Payroll.Api.UnitTests.Configuration;

public sealed class DotEnvFileTests
{
    [Fact]
    public void Parse_skips_blank_lines_and_comments()
    {
        var pairs = DotEnvFile.Parse(["", "   ", "# comment", "KEY=value"]).ToList();

        Assert.Equal(new KeyValuePair<string, string>("KEY", "value"), Assert.Single(pairs));
    }

    [Fact]
    public void Parse_keeps_equals_signs_inside_the_value()
    {
        var pairs = DotEnvFile.Parse(["ConnectionStrings__Default=Server=db;Password=a=b"]).ToList();

        Assert.Equal("Server=db;Password=a=b", pairs.Single().Value);
    }

    [Theory]
    [InlineData("KEY=\"value with spaces\"", "value with spaces")]
    [InlineData("KEY='single'", "single")]
    [InlineData("KEY=\"unbalanced", "\"unbalanced")]
    public void Parse_strips_only_matching_surrounding_quotes(string line, string expected)
    {
        Assert.Equal(expected, DotEnvFile.Parse([line]).Single().Value);
    }

    [Theory]
    [InlineData("NO_EQUALS_SIGN")]
    [InlineData("=value-without-key")]
    public void Parse_ignores_malformed_lines(string line)
    {
        Assert.Empty(DotEnvFile.Parse([line]));
    }

    [Fact]
    public void FindUpwards_returns_the_file_from_the_nearest_ancestor()
    {
        var fileName = $"test-{Guid.NewGuid():N}.env";
        var root = Directory.CreateTempSubdirectory().FullName;
        var nested = Directory.CreateDirectory(Path.Combine(root, "a", "b")).FullName;
        var expected = Path.Combine(root, fileName);
        File.WriteAllText(expected, "KEY=value");

        try
        {
            Assert.Equal(expected, DotEnvFile.FindUpwards(nested, fileName));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindUpwards_returns_null_when_no_ancestor_has_the_file()
    {
        var start = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Assert.Null(DotEnvFile.FindUpwards(start, $"missing-{Guid.NewGuid():N}.env"));
        }
        finally
        {
            Directory.Delete(start, recursive: true);
        }
    }
}
