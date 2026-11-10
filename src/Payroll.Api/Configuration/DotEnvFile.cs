namespace Payroll.Api.Configuration;

/// <summary>
/// Loads KEY=VALUE pairs from the git-ignored .env file into environment variables for local runs.
/// Variables that are already set (for example by docker compose) always win.
/// </summary>
public static class DotEnvFile
{
    public static void LoadFromAncestors(string startDirectory, string fileName = ".env")
    {
        var path = FindUpwards(startDirectory, fileName);
        if (path is null)
        {
            return;
        }

        foreach (var (key, value) in Parse(File.ReadLines(path)))
        {
            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    public static string? FindUpwards(string startDirectory, string fileName)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static IEnumerable<KeyValuePair<string, string>> Parse(IEnumerable<string> lines)
    {
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = Unquote(line[(separator + 1)..].Trim());
            yield return new KeyValuePair<string, string>(key, value);
        }
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0]
            ? value[1..^1]
            : value;
}
