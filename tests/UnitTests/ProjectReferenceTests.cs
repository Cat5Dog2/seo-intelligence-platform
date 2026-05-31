using System.Xml.Linq;

namespace UnitTests;

public sealed class ProjectReferenceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ProductionProjectsFollowCleanArchitectureDependencyDirection()
    {
        var repositoryRoot = FindRepositoryRoot();

        var expectedReferences = new Dictionary<string, string[]>
        {
            ["src/SeoIntelligence.Domain/SeoIntelligence.Domain.csproj"] = [],
            ["src/SeoIntelligence.Contracts/SeoIntelligence.Contracts.csproj"] = [],
            ["src/SeoIntelligence.Application/SeoIntelligence.Application.csproj"] =
            [
                "src/SeoIntelligence.Domain/SeoIntelligence.Domain.csproj",
                "src/SeoIntelligence.Contracts/SeoIntelligence.Contracts.csproj"
            ],
            ["src/SeoIntelligence.Infrastructure/SeoIntelligence.Infrastructure.csproj"] =
            [
                "src/SeoIntelligence.Application/SeoIntelligence.Application.csproj"
            ],
            ["src/SeoIntelligence.Api/SeoIntelligence.Api.csproj"] =
            [
                "src/SeoIntelligence.Application/SeoIntelligence.Application.csproj",
                "src/SeoIntelligence.Infrastructure/SeoIntelligence.Infrastructure.csproj"
            ],
            ["src/SeoIntelligence.Web/SeoIntelligence.Web.csproj"] =
            [
                "src/SeoIntelligence.Application/SeoIntelligence.Application.csproj"
            ],
            ["src/SeoIntelligence.Worker/SeoIntelligence.Worker.csproj"] =
            [
                "src/SeoIntelligence.Application/SeoIntelligence.Application.csproj",
                "src/SeoIntelligence.Infrastructure/SeoIntelligence.Infrastructure.csproj"
            ]
        };

        foreach (var (projectPath, references) in expectedReferences)
        {
            var projectFile = Path.Combine(repositoryRoot, NormalizePath(projectPath));
            var projectDirectory = Path.GetDirectoryName(projectFile)
                ?? throw new DirectoryNotFoundException(projectFile);
            var document = XDocument.Load(projectFile);

            var actualReferences = document
                .Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => Path.GetRelativePath(
                    repositoryRoot,
                    Path.GetFullPath(ToNativePath(value!), projectDirectory)))
                .Select(NormalizePath)
                .Order(StringComparer.Ordinal)
                .ToArray();

            var expectedProjectReferences = references
                .Select(NormalizePath)
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expectedProjectReferences, actualReferences);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SeoIntelligence.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate SeoIntelligence.sln.");
    }

    private static string NormalizePath(string path)
        => path.Replace('\\', '/');

    private static string ToNativePath(string path)
        => path
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
}
