using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// A job's arguments stay in the job store until the job expires. Mail queued as
/// <c>jobs.Enqueue(() => mailService.SendAsync(request, …))</c> stores the whole message, links and tokens included,
/// in clear; <c>IBackgroundMailer</c> stores it encrypted. So no framework code queues or schedules a send directly.
/// </summary>
public sealed partial class QueuedMailTests
{
    private static readonly string SolutionRoot = ModuleArchitectureTestsFixture.SolutionRoot;

    [GeneratedRegex(@"\b(Enqueue|Schedule)\w*\s*(<[^>]*>)?\(.*\.SendAsync\(", RegexOptions.CultureInvariant)]
    private static partial Regex QueuedSend();

    [Fact]
    public void Mail_Is_Queued_Only_Through_The_Encrypted_Mail_Queue()
    {
        var sep = Path.DirectorySeparatorChar;
        var offenders = new[] { "Modules", "BuildingBlocks" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(SolutionRoot, "src", folder), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{sep}obj{sep}", StringComparison.Ordinal) && !f.Contains($"{sep}bin{sep}", StringComparison.Ordinal))
            .SelectMany(f => File.ReadLines(f)
                .Select((line, index) => (line, index))
                .Where(l => !l.line.TrimStart().StartsWith("//", StringComparison.Ordinal) && QueuedSend().IsMatch(l.line))
                .Select(l => $"{Path.GetRelativePath(SolutionRoot, f)}:{l.index + 1}"))
            .ToList();

        offenders.ShouldBeEmpty("queue e-mail with IBackgroundMailer.Enqueue(request); a send inside a job expression stores the message in clear");
    }
}
