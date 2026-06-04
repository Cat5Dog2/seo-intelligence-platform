using System.Text.RegularExpressions;

namespace E2ETests;

internal static class DotEnvEnvironment
{
    private static readonly Regex EnvironmentVariableNamePattern = new(
        "^[A-Za-z_][A-Za-z0-9_.:-]*$",
        RegexOptions.CultureInvariant);
    private static readonly object SyncRoot = new();
    private static bool repositoryDotEnvLoaded;

    public static void LoadRepositoryDotEnv()
    {
        lock (SyncRoot)
        {
            if (repositoryDotEnvLoaded)
            {
                return;
            }

            repositoryDotEnvLoaded = true;
            var dotEnvPath = FindRepositoryDotEnv();
            if (dotEnvPath is not null)
            {
                LoadFile(dotEnvPath);
            }
        }
    }

    internal static void LoadFile(string path, bool overrideExisting = false)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line[7..].TrimStart();
            }

            var separator = line.IndexOf('=');
            if (separator < 1)
            {
                continue;
            }

            var name = line[..separator].Trim();
            if (!EnvironmentVariableNamePattern.IsMatch(name))
            {
                throw new InvalidOperationException($"Invalid environment variable name in {path}: {name}");
            }

            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && IsWrappedInMatchingQuotes(value))
            {
                value = value[1..^1];
            }

            if (!overrideExisting && Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process) is not null)
            {
                continue;
            }

            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
        }
    }

    private static bool IsWrappedInMatchingQuotes(string value)
    {
        var quote = value[0];
        return (quote == '"' || quote == '\'') && value[^1] == quote;
    }

    private static string? FindRepositoryDotEnv()
    {
        foreach (var startDirectory in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var repositoryRoot = FindRepositoryRoot(startDirectory);
            if (repositoryRoot is null)
            {
                continue;
            }

            var candidate = Path.Combine(repositoryRoot.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static DirectoryInfo? FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SeoIntelligence.sln")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
