using Shouldly;
using System.Xml.Linq;
using Xunit;

namespace Architecture.Tests;

public class ModuleArchitectureTests
{
    [Fact]
    public void Modules_Should_Not_Depend_On_Other_Modules()
    {
        string solutionRoot = GetSolutionRoot();
        var runtimeProjects = new[] { "Modules", "Samples" }
            .Select(folder => Path.Combine(solutionRoot, "src", folder))
            .SelectMany(root => Directory.GetFiles(root, "Modules.*.csproj", SearchOption.AllDirectories))
            .Where(path => !path.Contains(".Contracts", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        runtimeProjects.Length.ShouldBeGreaterThan(0);

        foreach (string projectPath in runtimeProjects)
        {
            string currentName = Path.GetFileNameWithoutExtension(projectPath);

            var document = XDocument.Load(projectPath);
            var references = document
                .Descendants("ProjectReference")
                .Select(x => (string?)x.Attribute("Include") ?? string.Empty)
                .Where(include => include.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (string include in references)
            {
                string referencedName = Path.GetFileNameWithoutExtension(include);

                bool isModuleRuntime = referencedName.StartsWith("Modules.", StringComparison.OrdinalIgnoreCase)
                                       && !referencedName.EndsWith(".Contracts", StringComparison.OrdinalIgnoreCase);

                if (!isModuleRuntime)
                {
                    continue;
                }

                bool isSelfReference = string.Equals(referencedName, currentName, StringComparison.OrdinalIgnoreCase);
                isSelfReference.ShouldBeTrue(
                    $"Module runtime project '{currentName}' must not reference other module runtime project '{referencedName}'. " +
                    "Only contracts or building block projects are allowed.");
            }
        }
    }

    /// <summary>
    /// The samples (src/Samples) leave with tl-product-saas, so nothing in the framework may need them:
    /// no building block or module references a sample project.
    /// </summary>
    [Fact]
    public void Framework_Should_Not_Reference_Samples()
    {
        string solutionRoot = GetSolutionRoot();

        var violations = new[] { "BuildingBlocks", "Modules" }
            .Select(folder => Path.Combine(solutionRoot, "src", folder))
            .SelectMany(root => Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories))
            .SelectMany(projectPath => XDocument.Load(projectPath)
                .Descendants("ProjectReference")
                .Select(x => (string?)x.Attribute("Include") ?? string.Empty)
                .Where(include => include.Replace('\\', '/').Contains("/Samples/", StringComparison.OrdinalIgnoreCase))
                .Select(include => $"{Path.GetFileName(projectPath)} -> {include}"))
            .ToList();

        violations.ShouldBeEmpty("The framework must not reference the samples: " + string.Join("; ", violations));
    }

    private static string GetSolutionRoot()
    {
        // Start at current directory and walk up until we find `src`.
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Unable to locate solution root containing 'src' folder.");
        }

        return directory.FullName;
    }
}