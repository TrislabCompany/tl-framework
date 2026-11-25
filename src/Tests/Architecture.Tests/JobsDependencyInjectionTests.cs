using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Hangfire's static API (<c>BackgroundJob</c>, <c>RecurringJob</c>, <c>JobStorage.Current</c>, the global
/// configuration, activator and filters) belongs to whichever host configured Hangfire last. Two hosts in one process
/// (the test hosts, a product's own test host) then queue on each other's storage and run each other's jobs with the
/// wrong services. The framework registers the storage, the activator and the filters per host in DI; this keeps
/// every other use of the statics out, tests included.
/// </summary>
public sealed partial class JobsDependencyInjectionTests
{
    private static readonly string SolutionRoot = ModuleArchitectureTestsFixture.SolutionRoot;

    [GeneratedRegex(
        @"(?<![.\w])(BackgroundJob|RecurringJob)\.\w+\s*[(<]|\bJobStorage\.Current\b|\bJobActivator\.Current\b|\bGlobalConfiguration\.Configuration\b|\bGlobalJobFilters\.",
        RegexOptions.CultureInvariant)]
    private static partial Regex HangfireStatic();

    [Fact]
    public void Nothing_Uses_Hangfires_Process_Wide_Statics()
    {
        var sep = Path.DirectorySeparatorChar;
        var thisFile = $"{sep}Architecture.Tests{sep}";
        var offenders = Directory.EnumerateFiles(Path.Combine(SolutionRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}", StringComparison.Ordinal)
                && !f.Contains($"{sep}bin{sep}", StringComparison.Ordinal)
                && !f.Contains(thisFile, StringComparison.Ordinal))
            .SelectMany(f => File.ReadLines(f)
                .Select((line, index) => (line, index))
                .Where(l => !l.line.TrimStart().StartsWith("//", StringComparison.Ordinal) && HangfireStatic().IsMatch(l.line))
                .Select(l => $"{Path.GetRelativePath(SolutionRoot, f)}:{l.index + 1}"))
            .ToList();

        offenders.ShouldBeEmpty(
            "inject IJobService, IBackgroundJobClient, IRecurringJobManager or JobStorage instead of Hangfire's statics (jobs.md)");
    }
}
