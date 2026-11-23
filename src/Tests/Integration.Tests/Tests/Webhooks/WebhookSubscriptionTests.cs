using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Webhooks;

/// <summary>Runs in both tenancy modes: see <see cref="WebhookSubscriptionTests"/> and <see cref="WebhookSubscriptionTestsInSingleTenantMode"/>.</summary>
public abstract class WebhookSubscriptionTestsBase
{
    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    protected WebhookSubscriptionTestsBase(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task CreateWebhookSubscription_Should_ReturnCreated_When_DataIsValid()
    {
        using var client = await _auth.CreateRootAdminClientAsync();

        var response = await client.PostAsJsonAsync(
            $"{TestConstants.WebhooksBasePath}/subscriptions", new
            {
                url = "https://example.com/webhook",
                events = new[] { "user.created", "user.updated" },
                secret = "test-webhook-secret-123"
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetWebhookSubscriptions_Should_ReturnOk_When_SubscriptionsExist()
    {
        using var client = await _auth.CreateRootAdminClientAsync();

        // Create a subscription first
        await client.PostAsJsonAsync($"{TestConstants.WebhooksBasePath}/subscriptions", new
        {
            url = "https://example.com/webhook-list",
            events = new[] { "tenant.created" },
            secret = "list-secret-123"
        });

        var response = await client.GetAsync(
            $"{TestConstants.WebhooksBasePath}/subscriptions?pageNumber=1&pageSize=10");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteWebhookSubscription_Should_ReturnNoContent_When_SubscriptionExists()
    {
        using var client = await _auth.CreateRootAdminClientAsync();

        var createResponse = await client.PostAsJsonAsync(
            $"{TestConstants.WebhooksBasePath}/subscriptions", new
            {
                url = "https://example.com/webhook-delete",
                events = new[] { "user.deleted" },
                secret = "delete-secret-123"
            });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var subscriptionId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var response = await client.DeleteAsync(
            $"{TestConstants.WebhooksBasePath}/subscriptions/{subscriptionId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetWebhookSubscriptions_Should_Return400_When_PageSizeIsZero()
    {
        using var client = await _auth.CreateRootAdminClientAsync();

        // PageSize=0 previously reached Math.Ceiling(total / (double)0) -> 500. The validator
        // now rejects it with a clean 400.
        var response = await client.GetAsync(
            $"{TestConstants.WebhooksBasePath}/subscriptions?pageNumber=1&pageSize=0");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateWebhookSubscription_Should_Return401_When_NotAuthenticated()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("tenant", _factory.TenantId);

        var response = await client.PostAsJsonAsync(
            $"{TestConstants.WebhooksBasePath}/subscriptions", new
            {
                url = "https://example.com/noauth-webhook",
                events = new[] { "user.created" },
                secret = "noauth-secret"
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

[Collection(TrislabCollectionDefinition.Name)]
public sealed class WebhookSubscriptionTests(TrislabWebApplicationFactory factory) : WebhookSubscriptionTestsBase(factory);

[Collection(SingleTenantCollectionDefinition.Name)]
public sealed class WebhookSubscriptionTestsInSingleTenantMode(SingleTenantWebApplicationFactory factory) : WebhookSubscriptionTestsBase(factory);
