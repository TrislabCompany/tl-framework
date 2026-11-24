using System.Net.Http.Json;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Notifications.Contracts.v1.DTOs;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR.Client;

namespace Integration.Tests.Tests.Notifications;

/// <summary>
/// The path a module takes to notify a user: it publishes <c>UserNotificationRequestedIntegrationEvent</c>, the
/// Notifications module writes the inbox row and pushes it over the realtime hub. The test host's
/// <c>POST /api/v1/test/notifications</c> publishes the event.
/// </summary>
[Collection(TrislabCollectionDefinition.Name)]
public sealed class UserNotificationRequestedTests
{
    private const string TestBasePath = "/api/v1/test";
    private const string NotificationsBasePath = "/api/v1/notifications";
    private const string HubPath = "/api/v1/realtime/hub";
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public UserNotificationRequestedTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task Requested_Notification_Should_Be_Persisted_In_The_Inbox_Of_The_User()
    {
        using var adminClient = await _auth.CreateRootAdminClientAsync();
        var (alice, _, _) = await RegisterUserAsync(adminClient, "alice");
        var link = $"/items/{Guid.NewGuid():N}";

        await NotifyAsync(adminClient, alice.Id, "test.ping", "Ping for Alice", "take a look", link);

        using var aliceClient = await _auth.CreateAuthenticatedClientAsync(alice.Email, alice.Password);
        var inbox = await ReadInboxAsync(aliceClient);
        var ping = inbox.FirstOrDefault(n => n.Type == "test.ping" && n.Link == link);
        ping.ShouldNotBeNull("Expected the requested notification to land in Alice's inbox");
        ping.Title.ShouldBe("Ping for Alice");
        ping.Body.ShouldBe("take a look");
        ping.ReadAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Requested_Notification_Should_Be_Pushed_Over_SignalR()
    {
        using var adminClient = await _auth.CreateRootAdminClientAsync();
        var (bob, bobToken, _) = await RegisterUserAsync(adminClient, "bob");

        await using var bobHub = await ConnectAsync(bobToken);
        using var inbox = new EventInbox<NotificationPayload>(bobHub, "NotificationCreated");
        var link = $"/items/{Guid.NewGuid():N}";

        await NotifyAsync(adminClient, bob.Id, "test.push", "Push for Bob", null, link);

        var received = await inbox.WaitForFirstAsync(p => p.Type == "test.push" && p.Link == link, EventTimeout);
        received.ShouldNotBeNull("Expected NotificationCreated to fire on Bob's hub connection");
        received.Title.ShouldBe("Push for Bob");
    }

    [Fact]
    public async Task Requested_Notification_Should_Not_Reach_Other_Users()
    {
        using var adminClient = await _auth.CreateRootAdminClientAsync();
        var (carol, _, _) = await RegisterUserAsync(adminClient, "carol");
        var (dave, _, _) = await RegisterUserAsync(adminClient, "dave");
        var link = $"/items/{Guid.NewGuid():N}";

        await NotifyAsync(adminClient, carol.Id, "test.private", "Only for Carol", null, link);

        using var daveClient = await _auth.CreateAuthenticatedClientAsync(dave.Email, dave.Password);
        (await ReadInboxAsync(daveClient)).ShouldNotContain(n => n.Link == link);
    }

    private static async Task NotifyAsync(HttpClient client, string userId, string type, string title, string? body, string link)
    {
        using var response = await client.PostAsJsonAsync($"{TestBasePath}/notifications", new { userId, type, title, body, link });
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NoContent);
    }

    // ─── helpers ─────────────────────────────────────────────────────

    private async Task<HubConnection> ConnectAsync(string accessToken)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(
                $"http://localhost{HubPath}?access_token={Uri.EscapeDataString(accessToken)}",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.WebSocketFactory = (_, _) => throw new NotSupportedException();
                    options.SkipNegotiation = false;
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                    options.Headers["tenant"] = TestConstants.RootTenantId;
                })
            .Build();
        await connection.StartAndWaitForServerAsync();
        return connection;
    }

    private static async Task<IReadOnlyList<NotificationDto>> ReadInboxAsync(HttpClient client)
    {
        using var response = await client.GetAsync($"{NotificationsBasePath}/?pageSize=100");
        return await response.DeserializeAsync<IReadOnlyList<NotificationDto>>();
    }

    private async Task<(UserCredentials user, string accessToken, string refreshToken)> RegisterUserAsync(
        HttpClient adminClient, string prefix)
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var email = $"{prefix}-{unique}@example.com";
        var userName = $"{prefix}{unique}";
        const string password = "Test@1234!";

        using var response = await adminClient.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/register", new
        {
            firstName = prefix,
            lastName = "Test",
            email,
            userName,
            password,
            confirmPassword = password,
        });
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created);
        var registered = await response.DeserializeAsync<RegisterResult>();

        // Registered users land with EmailConfirmed=false and `/token/issue` rejects them with
        // 401 until they confirm. Bypass the email link in-process so the test can sign in.
        await ConfirmEmailAsync(registered.UserId);

        var token = await _auth.GetTokenAsync(email, password);
        return (new UserCredentials(registered.UserId, userName, email, password), token.AccessToken, token.RefreshToken);
    }

    private async Task ConfirmEmailAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var tenantStore = scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();
        var tenant = await tenantStore.GetAsync(TestConstants.RootTenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TrislabUser>>();
        var user = await userManager.FindByIdAsync(userId);
        user.ShouldNotBeNull();
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            (await userManager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
        }
    }

    private sealed record UserCredentials(string Id, string UserName, string Email, string Password);

    private sealed record NotificationPayload(
        Guid Id,
        string Type,
        string Title,
        string? Body,
        string? Link,
        string Source,
        DateTime CreatedAtUtc);

    private sealed class EventInbox<T> : IDisposable
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<T> _items = new();
        private readonly System.Threading.SemaphoreSlim _signal = new(0);
        private readonly IDisposable _subscription;

        public EventInbox(HubConnection connection, string eventName)
        {
            _subscription = connection.On<T>(eventName, payload =>
            {
                _items.Enqueue(payload);
                _signal.Release();
            });
        }

        public async Task<T?> WaitForFirstAsync(Func<T, bool> predicate, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            if (TryFindMatch(predicate, out var prearrived))
            {
                return prearrived;
            }
            try
            {
                while (await _signal.WaitAsync(timeout, cts.Token).ConfigureAwait(false))
                {
                    if (TryFindMatch(predicate, out var hit)) return hit;
                }
            }
            catch (OperationCanceledException)
            {
                // Fall through to return null on timeout.
            }
            return default;
        }

        private bool TryFindMatch(Func<T, bool> predicate, out T match)
        {
            var snapshot = new List<T>();
            while (_items.TryDequeue(out var item)) snapshot.Add(item);
            int idx = snapshot.FindIndex(p => predicate(p));
            if (idx < 0)
            {
                foreach (var item in snapshot) _items.Enqueue(item);
                match = default!;
                return false;
            }
            match = snapshot[idx];
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (i != idx) _items.Enqueue(snapshot[i]);
            }
            return true;
        }

        public void Dispose()
        {
            _subscription.Dispose();
            _signal.Dispose();
        }
    }
}
