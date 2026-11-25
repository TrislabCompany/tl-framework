using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Integration.Tests.Infrastructure;

/// <summary>
/// One Mailpit SMTP catcher for the whole test run. The hosts send through the framework's real
/// <c>SmtpMailService</c>, so a test checks the e-mail a user would get: recipient, subject, bodies and attachments.
/// <para>
/// It is shared by every test host on purpose. Hangfire keeps its job storage and activator in process-wide
/// statics, so an e-mail job queued by one test host can run on another; with one catcher the message arrives
/// whichever host sends it. Tests tell their messages apart by recipient, so each test uses its own address.
/// </para>
/// </summary>
internal static class Mailpit
{
    private const int SmtpPort = 1025;
    private const int ApiPort = 8025;

    // Accept any SMTP credentials over plain SMTP: the framework treats SMTP without a password as "mail disabled".
    private static readonly IContainer Container = new ContainerBuilder("axllent/mailpit:v1.28.0@sha256:c076638db1e15662150be4fb62b8a6e96ef6ba5bde90c838a0239225854830f7")
        .WithEnvironment("MP_SMTP_AUTH_ACCEPT_ANY", "1")
        .WithEnvironment("MP_SMTP_AUTH_ALLOW_INSECURE", "1")
        .WithEnvironment("MP_MAX_MESSAGES", "0")
        .WithPortBinding(SmtpPort, true)
        .WithPortBinding(ApiPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(ApiPort).ForPath("/livez")))
        .WithAutoRemove(true)
        .WithCleanUp(true)
        .Build();

    private static readonly Lazy<Task> Started = new(() => Container.StartAsync());

    private static readonly Lazy<HttpClient> Api = new(() => new HttpClient
    {
        BaseAddress = new Uri($"http://{Container.Hostname}:{Container.GetMappedPublicPort(ApiPort)}/api/v1/"),
    });

    public static Task StartAsync() => Started.Value;

    public static string SmtpHost => Container.Hostname;

    public static int SmtpMappedPort => Container.GetMappedPublicPort(SmtpPort);

    /// <summary>The messages sent to <paramref name="address"/>, oldest first, with their bodies.</summary>
    public static async Task<IReadOnlyList<MailpitMessage>> MessagesToAsync(string address)
    {
        var search = await Api.Value.GetFromJsonAsync<SearchResult>(
            $"search?limit=500&query={Uri.EscapeDataString($"to:\"{address}\"")}");
        var messages = new List<MailpitMessage>();
        foreach (var summary in (search?.Messages ?? []).OrderBy(m => m.Created))
        {
            messages.Add((await Api.Value.GetFromJsonAsync<MailpitMessage>($"message/{summary.Id}"))!);
        }

        return messages;
    }

    /// <summary>Waits until <paramref name="address"/> has a message matching <paramref name="match"/> beyond the first <paramref name="skip"/> matches.</summary>
    public static async Task<MailpitMessage> WaitForMessageAsync(
        string address, Func<MailpitMessage, bool>? match = null, int skip = 0, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        while (true)
        {
            var found = (await MessagesToAsync(address)).Where(match ?? (_ => true)).ToList();
            if (found.Count > skip)
            {
                return found[^1];
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"No matching e-mail reached {address}.");
            }

            await Task.Delay(200);
        }
    }

    /// <summary>Deletes every message sent to <paramref name="address"/>.</summary>
    public static async Task DeleteMessagesToAsync(string address)
    {
        using var response = await Api.Value.DeleteAsync($"search?query={Uri.EscapeDataString($"to:\"{address}\"")}");
        response.EnsureSuccessStatusCode();
    }

    private sealed record SearchResult([property: JsonPropertyName("messages")] IReadOnlyList<MessageSummary>? Messages);

    private sealed record MessageSummary(
        [property: JsonPropertyName("ID")] string Id,
        [property: JsonPropertyName("Created")] DateTimeOffset Created);
}

/// <summary>A message as Mailpit stored it.</summary>
internal sealed record MailpitMessage(
    [property: JsonPropertyName("Subject")] string Subject,
    [property: JsonPropertyName("Text")] string? Text,
    [property: JsonPropertyName("HTML")] string? Html,
    [property: JsonPropertyName("To")] IReadOnlyList<MailpitAddress> To,
    [property: JsonPropertyName("Attachments")] IReadOnlyList<MailpitAttachment>? Attachments)
{
    /// <summary>The text body, or the HTML one when there is no text part.</summary>
    public string Body => string.IsNullOrEmpty(Text) ? Html ?? string.Empty : Text;
}

internal sealed record MailpitAddress([property: JsonPropertyName("Address")] string Address);

internal sealed record MailpitAttachment(
    [property: JsonPropertyName("FileName")] string FileName,
    [property: JsonPropertyName("ContentType")] string ContentType);
