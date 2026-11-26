using System.Text.Json;
using System.Text.Json.Serialization;
using Trislab.Modules.SubscriptionsUsage.Contracts;
using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Billing;

/// <summary>
/// The limits each product declares (<c>IUsageMeter</c>), on the test host's three meters: <c>test-pages</c>, a counter
/// with a hard limit (default 3); <c>test-extra</c>, an extra-usage counter (default 2); <c>test-items</c>, a gauge of
/// the tenant's test items (default 2). Every test uses its own fresh tenant, so counters never meet.
/// </summary>
[Collection(TrislabCollectionDefinition.Name)]
public sealed class UsageLimitsTests
{
    private const string BillingBasePath = "/api/v1/billing";
    private const string TestBasePath = "/api/v1/test";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AuthHelper _auth;

    public UsageLimitsTests(TrislabWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task Meters_Should_ListEveryDeclaredMeter_ForRoot()
    {
        using var root = await _auth.CreateRootAdminClientAsync();

        using var response = await root.GetAsync($"{BillingBasePath}/meters");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var meters = await ParseAsync<List<UsageMeterDto>>(response);
        meters.ShouldContain(m => m.Key == "test-pages" && m.Mode == UsageLimitMode.HardLimit && m.DefaultLimit == 3 && !m.IsGauge);
        meters.ShouldContain(m => m.Key == "test-extra" && m.Mode == UsageLimitMode.ExtraUsage);
        meters.ShouldContain(m => m.Key == "test-items" && m.IsGauge);
    }

    [Fact]
    public async Task MetersAndPlanLimits_Should_BeForbidden_ForTenantAdmin()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        var planId = await CreatePlanAsync(root, "limits-forbidden");
        var (tenant, _) = await CreateTenantClientAsync(root, "forbid");
        using var _tenant = tenant;

        using var meters = await tenant.GetAsync($"{BillingBasePath}/meters");
        using var set = await tenant.PutAsJsonAsync($"{BillingBasePath}/plans/{planId}/limits",
            new { limits = new[] { new { meterKey = "test-pages", limit = 1000 } } });

        meters.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        set.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetPlanLimits_Should_Reject_AMeterTheProductDidNotDeclare()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        var planId = await CreatePlanAsync(root, "limits-unknown");

        using var response = await root.PutAsJsonAsync($"{BillingBasePath}/plans/{planId}/limits",
            new { limits = new[] { new { meterKey = "no-such-meter", limit = 5 } } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Counter_Should_StopAtTheDefaultLimit_WithItsCode_AndShowTheUsage()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        var (tenant, _) = await CreateTenantClientAsync(root, "default");
        using var _tenant = tenant;

        for (var i = 0; i < 3; i++)
        {
            using var ok = await ConsumeAsync(tenant, "test-pages");
            ok.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        using var refused = await ConsumeAsync(tenant, "test-pages");

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        // The test host answers with the English message and the code; the localized text is BillingResources' (unit tests).
        var refusal = await refused.Content.ReadAsStringAsync();
        refusal.ShouldContain("\"code\":\"Billing.LimitReached\"", customMessage: refusal);

        var pages = (await GetMyUsageAsync(tenant)).Single(u => u.MeterKey == "test-pages");
        pages.Used.ShouldBe(3, "the refused call counts nothing");
        pages.Limit.ShouldBe(3);
    }

    [Fact]
    public async Task PlanLimit_Should_OverrideTheDefault_AndHold_UnderConcurrentCalls()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        var planKey = UniqueKey("limits-plan");
        var planId = await CreatePlanAsync(root, planKey);
        await SetLimitsAsync(root, planId, new { meterKey = "test-pages", limit = 5L, extraUnitPrice = (decimal?)null });
        var (tenant, tenantId) = await CreateTenantClientAsync(root, "plan");
        using var _tenant = tenant;
        await AssignSubscriptionAsync(root, tenantId, planKey);

        var calls = Enumerable.Range(0, 12).Select(async _ =>
        {
            using var response = await ConsumeAsync(tenant, "test-pages");
            return response.StatusCode;
        });
        var statuses = await Task.WhenAll(calls);

        statuses.Count(s => s == HttpStatusCode.OK).ShouldBe(5, "the plan's limit is 5, and concurrent calls never pass it");
        statuses.Count(s => s == HttpStatusCode.Conflict).ShouldBe(7);
        (await GetMyUsageAsync(tenant)).Single(u => u.MeterKey == "test-pages").Used.ShouldBe(5);
    }

    [Fact]
    public async Task ExtraUsage_Should_NeverBeRefused_AndShowThePlansUnitPrice()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        var planKey = UniqueKey("limits-extra");
        var planId = await CreatePlanAsync(root, planKey);
        await SetLimitsAsync(root, planId, new { meterKey = "test-extra", limit = 2L, extraUnitPrice = (decimal?)0.5m });
        var (tenant, tenantId) = await CreateTenantClientAsync(root, "extra");
        using var _tenant = tenant;
        await AssignSubscriptionAsync(root, tenantId, planKey);

        for (var i = 0; i < 4; i++)
        {
            using var response = await ConsumeAsync(tenant, "test-extra");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var extra = (await GetMyUsageAsync(tenant)).Single(u => u.MeterKey == "test-extra");
        extra.Used.ShouldBe(4);
        extra.Limit.ShouldBe(2);
        extra.ExtraUnitPrice.ShouldBe(0.5m);
        extra.Currency.ShouldBe("EUR");
    }

    [Fact]
    public async Task Gauge_Should_RefuseTheItemPastTheLimit()
    {
        using var root = await _auth.CreateRootAdminClientAsync();
        var (tenant, _) = await CreateTenantClientAsync(root, "gauge");
        using var _tenant = tenant;

        using var first = await tenant.PostAsJsonAsync($"{TestBasePath}/items", new { name = "one" });
        using var second = await tenant.PostAsJsonAsync($"{TestBasePath}/items", new { name = "two" });
        using var third = await tenant.PostAsJsonAsync($"{TestBasePath}/items", new { name = "three" });

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        third.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await GetMyUsageAsync(tenant)).Single(u => u.MeterKey == "test-items").Used.ShouldBe(2);
    }

    [Fact]
    public async Task Root_Should_NeverBeLimited()
    {
        using var root = await _auth.CreateRootAdminClientAsync();

        for (var i = 0; i < 4; i++)
        {
            using var response = await ConsumeAsync(root, "test-pages");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    private static Task<HttpResponseMessage> ConsumeAsync(HttpClient client, string meterKey) =>
        client.PostAsync($"{TestBasePath}/limits/{meterKey}/consume", content: null);

    private static async Task<List<UsageLimitStatusDto>> GetMyUsageAsync(HttpClient client)
    {
        using var response = await client.GetAsync($"{BillingBasePath}/limits/me");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await ParseAsync<List<UsageLimitStatusDto>>(response);
    }

    private static string UniqueKey(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    private static async Task<T> ParseAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize response to {typeof(T).Name}. Body: {json}");
    }

    private static async Task<Guid> CreatePlanAsync(HttpClient root, string key)
    {
        using var response = await root.PostAsJsonAsync($"{BillingBasePath}/plans",
            new { key, name = $"Plan {key}", currency = "EUR", monthlyBasePrice = 0m });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"CreatePlan failed: {body}");
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task SetLimitsAsync(HttpClient root, Guid planId, params object[] limits)
    {
        using var response = await root.PutAsJsonAsync($"{BillingBasePath}/plans/{planId}/limits", new { limits });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"SetPlanLimits failed: {body}");
    }

    private static async Task AssignSubscriptionAsync(HttpClient root, string tenantId, string planKey)
    {
        using var response = await root.PostAsJsonAsync($"{BillingBasePath}/subscriptions", new { tenantId, planKey });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"AssignSubscription failed: {body}");
    }

    /// <summary>Provisions a fresh tenant and returns an authenticated admin client for it.</summary>
    private async Task<(HttpClient Client, string TenantId)> CreateTenantClientAsync(HttpClient root, string label)
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tenantId = $"limits-{label}-{uniqueId}";
        var adminEmail = $"limits-{label}-{uniqueId}@tenant.com";

        using var created = await root.PostAsJsonAsync(TestConstants.TenantsBasePath, new
        {
            id = tenantId,
            name = $"Tenant {tenantId}",
            connectionString = (string?)null,
            adminEmail,
            adminPassword = TestConstants.DefaultPassword,
            issuer = $"{tenantId}.issuer"
        });
        var body = await created.Content.ReadAsStringAsync();
        created.StatusCode.ShouldBe(HttpStatusCode.Created, $"Create tenant failed: {body}");
        await TenantProvisioningWait.WaitForProvisioningAsync(root, tenantId);

        for (var attempt = 0; attempt < 29; attempt++)
        {
            try
            {
                return (await _auth.CreateAuthenticatedClientAsync(adminEmail, TestConstants.DefaultPassword, tenantId), tenantId);
            }
            catch (HttpRequestException)
            {
                await Task.Delay(1000);
            }
        }

        return (await _auth.CreateAuthenticatedClientAsync(adminEmail, TestConstants.DefaultPassword, tenantId), tenantId);
    }
}
