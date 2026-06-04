namespace E2ETests;

public sealed class DotEnvEnvironmentTests
{
    [Fact]
    [Trait("Category", "E2E")]
    public void LoadFileSetsVariablesAndKeepsExistingProcessValues()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var fromFileName = $"DOTENV_TEST_FROM_FILE_{suffix}";
        var quotedName = $"DOTENV_TEST_QUOTED_{suffix}";
        var existingName = $"DOTENV_TEST_EXISTING_{suffix}";
        var missingLineName = $"DOTENV_TEST_MISSING_LINE_{suffix}";
        using var dotEnv = TemporaryDotEnv.Create($"""
            # comments are ignored
            export {fromFileName}=from-file
            {quotedName}="quoted value"
            {existingName}=from-env
            malformed line without separator
            {missingLineName}
            """);

        Environment.SetEnvironmentVariable(existingName, "from-process", EnvironmentVariableTarget.Process);

        try
        {
            DotEnvEnvironment.LoadFile(dotEnv.Path);

            Assert.Equal("from-file", Environment.GetEnvironmentVariable(fromFileName, EnvironmentVariableTarget.Process));
            Assert.Equal("quoted value", Environment.GetEnvironmentVariable(quotedName, EnvironmentVariableTarget.Process));
            Assert.Equal("from-process", Environment.GetEnvironmentVariable(existingName, EnvironmentVariableTarget.Process));
            Assert.Null(Environment.GetEnvironmentVariable(missingLineName, EnvironmentVariableTarget.Process));
        }
        finally
        {
            ClearProcessVariables(fromFileName, quotedName, existingName, missingLineName);
        }
    }

    [Fact]
    [Trait("Category", "E2E")]
    public void LoadFileCanOverrideExistingProcessValuesWhenRequested()
    {
        var variableName = $"DOTENV_TEST_OVERRIDE_{Guid.NewGuid():N}";
        using var dotEnv = TemporaryDotEnv.Create($"{variableName}=from-env");
        Environment.SetEnvironmentVariable(variableName, "from-process", EnvironmentVariableTarget.Process);

        try
        {
            DotEnvEnvironment.LoadFile(dotEnv.Path, overrideExisting: true);

            Assert.Equal("from-env", Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Process));
        }
        finally
        {
            ClearProcessVariables(variableName);
        }
    }

    [Fact]
    [Trait("Category", "E2E")]
    public void LoadFileRejectsInvalidVariableNames()
    {
        using var dotEnv = TemporaryDotEnv.Create("1INVALID=value");

        var exception = Assert.Throws<InvalidOperationException>(() => DotEnvEnvironment.LoadFile(dotEnv.Path));

        Assert.Contains("Invalid environment variable name", exception.Message, StringComparison.Ordinal);
    }

    private static void ClearProcessVariables(params string[] names)
    {
        foreach (var name in names)
        {
            Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.Process);
        }
    }

    private sealed class TemporaryDotEnv : IDisposable
    {
        private readonly string directoryPath;

        private TemporaryDotEnv(string directoryPath, string path)
        {
            this.directoryPath = directoryPath;
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDotEnv Create(string content)
        {
            var directoryPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"seo-intelligence-dotenv-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directoryPath);
            var path = System.IO.Path.Combine(directoryPath, ".env");
            File.WriteAllText(path, content);
            return new TemporaryDotEnv(directoryPath, path);
        }

        public void Dispose()
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }
}
