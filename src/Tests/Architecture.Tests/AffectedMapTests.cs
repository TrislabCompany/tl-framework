using System.Text.Json;
using Shouldly;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// src/Tests/affected-map.json decides which integration-test areas a change runs
/// (scripts/affected-tests.ps1). A project missing from it would make every change to it run everything,
/// and an area no project names would run only on a full run, so the map must stay complete.
/// </summary>
public class AffectedMapTests
{
    private static readonly string SolutionRoot = ModuleArchitectureTestsFixture.SolutionRoot;

    private static readonly string AreasRoot =
        Path.Combine(SolutionRoot, "src", "Tests", "Integration.Tests", "Tests");

    [Fact]
    public void Every_BuildingBlock_And_Module_Project_Has_Areas_Or_Runs_Everything()
    {
        var map = ReadMap();

        var missing = SourceProjects()
            .Where(p => !map.Areas.ContainsKey(p.Name) && !map.AllFolders.Contains(p.Folder))
            .Select(p => p.Name)
            .ToList();

        missing.ShouldBeEmpty(
            "Add these projects to \"areas\" in src/Tests/affected-map.json, with the Integration.Tests areas they need");
    }

    [Fact]
    public void Every_Mapped_Area_Is_A_Folder_Of_Integration_Tests()
    {
        var folders = AreaFolders();

        var unknown = ReadMap().Areas
            .SelectMany(kv => kv.Value.Select(area => $"{kv.Key}: {area}"))
            .Where(entry => !folders.Contains(entry[(entry.IndexOf(": ", StringComparison.Ordinal) + 2)..]))
            .ToList();

        unknown.ShouldBeEmpty("These areas in src/Tests/affected-map.json are not folders of Integration.Tests/Tests");
    }

    [Fact]
    public void Every_Integration_Test_Area_Is_Named_By_A_Project()
    {
        var named = ReadMap().Areas.Values.SelectMany(a => a).ToHashSet(StringComparer.Ordinal);

        var orphans = AreaFolders().Where(f => !named.Contains(f)).ToList();

        orphans.ShouldBeEmpty(
            "Name these Integration.Tests areas under the projects they test in src/Tests/affected-map.json");
    }

    private static HashSet<string> AreaFolders() =>
        Directory.GetDirectories(AreasRoot)
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<(string Name, string Folder)> SourceProjects() =>
        new[] { "BuildingBlocks", "Modules" }
            .SelectMany(dir => Directory.GetFiles(Path.Combine(SolutionRoot, "src", dir), "*.csproj", SearchOption.AllDirectories))
            .Select(f => (
                Path.GetFileNameWithoutExtension(f),
                Path.GetRelativePath(SolutionRoot, Path.GetDirectoryName(f)!).Replace('\\', '/')));

    private static AffectedMap ReadMap()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(SolutionRoot, "src", "Tests", "affected-map.json")));

        var all = doc.RootElement.GetProperty("all").EnumerateArray()
            .Select(e => e.GetString()!)
            .Where(g => g.EndsWith("/**", StringComparison.Ordinal))
            .Select(g => g[..^3])
            .ToHashSet(StringComparer.Ordinal);

        var areas = doc.RootElement.GetProperty("areas").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(a => a.GetString()!).ToList(), StringComparer.Ordinal);

        return new AffectedMap(all, areas);
    }

    private sealed record AffectedMap(HashSet<string> AllFolders, Dictionary<string, List<string>> Areas);
}
