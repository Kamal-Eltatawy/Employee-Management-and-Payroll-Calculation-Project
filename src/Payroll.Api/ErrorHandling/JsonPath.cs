namespace Payroll.Api.ErrorHandling;

/// <summary>Converts C# property paths (e.g. "PayComponents[0].Amount") to the JSON paths the client sends.</summary>
public static class JsonPath
{
    public static string ToCamelCase(string propertyPath)
    {
        var segments = propertyPath.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.Length > 0 && char.IsUpper(segment[0]))
            {
                segments[i] = char.ToLowerInvariant(segment[0]) + segment[1..];
            }
        }

        return string.Join('.', segments);
    }
}
