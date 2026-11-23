using System.Text;
using System.Text.Json;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Quota;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Shared.Quota;
using Trislab.Framework.Tenancy;
using Trislab.Modules.Identity.Domain;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests.Tests.Tenancy;

/// <summary>Reads the claims of a JWT without validating it; the host already did.</summary>
internal static class JwtPayload
{
    public static string? Claim(string jwt, string name)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        return doc.RootElement.TryGetProperty(name, out var value) ? value.GetString() : null;
    }
}

/// <summary>What single-tenant mode promises: a fixed tenant, no tenant on sign-in, no catalog, no quotas.</summary>
[Collection(SingleTenantCollectionDefinition.Name)]
public sealed class SingleTenantModeTests
{
    private readonly SingleTenantWebApplicationFactory _factory;

    public SingleTenantModeTests(SingleTenantWebApplicationFactory factory) => _factory = factory;

    private async Task<HttpResponseMessage> SignInAsync(string? tenantHeader)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue");
        if (tenantHeader is not null)
        {
            request.Headers.Add("tenant", tenantHeader);
        }

        request.Content = JsonContent.Create(new { email = TestConstants.SingleTenantAdminEmail, password = TestConstants.DefaultPassword });
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task SignIn_Should_Succeed_Without_ATenantHeader_And_CarryTheTenantClaim()
    {
        using var response = await SignInAsync(tenantHeader: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = await response.DeserializeAsync<TokenResult>();
        JwtPayload.Claim(token.AccessToken, "tenant").ShouldBe(TestConstants.SingleTenantId);
    }

    [Fact]
    public async Task SignIn_Should_Ignore_ATenantHeader_The_Caller_Sends()
    {
        using var response = await SignInAsync(tenantHeader: "someone-else");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = await response.DeserializeAsync<TokenResult>();
        JwtPayload.Claim(token.AccessToken, "tenant").ShouldBe(TestConstants.SingleTenantId);
    }

    [Fact]
    public async Task Refresh_Should_Succeed_Without_ATenantHeader()
    {
        using var signIn = await SignInAsync(tenantHeader: null);
        var token = await signIn.DeserializeAsync<TokenResult>();

        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/token/refresh",
            new { token = token.AccessToken, refreshToken = token.RefreshToken });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheOperatorEndpoints_Should_NotExist()
    {
        using var client = await new AuthHelper(_factory).CreateRootAdminClientAsync();

        using var tenants = await client.GetAsync(TestConstants.TenantsBasePath);
        using var plans = await client.GetAsync("/api/v1/billing/plans");

        tenants.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        plans.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public void TheTenancyServices_Should_BeTheSingleTenantOnes()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<ISingleTenantProvider>().Tenant.Id.ShouldBe(TestConstants.SingleTenantId);
        sp.GetRequiredService<ISingleTenantProvider>().Country.ShouldBe(TestConstants.SingleTenantCountry);
        sp.GetRequiredService<IEventTenantScope>().ShouldBeOfType<SingleTenantEventTenantScope>();
        sp.GetRequiredService<IQuotaService>().ShouldBeOfType<NoopQuotaService>();
        sp.GetServices<IMultiTenantStore<AppTenantInfo>>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task EventScope_Should_RunInTheFixedTenant_WhateverIdTheEventCarries()
    {
        var scopeService = _factory.Services.GetRequiredService<IEventTenantScope>();
        var accessor = _factory.Services.GetRequiredService<IMultiTenantContextAccessor<AppTenantInfo>>();
        string? seen = null;

        await scopeService.ExecuteAsync("anything", _ =>
        {
            seen = accessor.MultiTenantContext?.TenantInfo?.Id;
            return Task.CompletedTask;
        });

        seen.ShouldBe(TestConstants.SingleTenantId);
    }

    [Fact]
    public async Task TheData_Should_StillBeStampedWithTheTenant()
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ISingleTenantProvider>().Tenant;
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TrislabUser>>();
        var stamped = await userManager.Users
            .Where(u => u.Email == TestConstants.SingleTenantAdminEmail)
            .Select(u => EF.Property<string>(u, "TenantId"))
            .SingleAsync();

        stamped.ShouldBe(TestConstants.SingleTenantId);
    }
}

/// <summary>The default mode keeps its behaviour: a tenant header is required, tenants come from the catalog.</summary>
[Collection(TrislabCollectionDefinition.Name)]
public sealed class MultitenantModeTests
{
    private readonly TrislabWebApplicationFactory _factory;

    public MultitenantModeTests(TrislabWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SignIn_Should_Return400_Without_ATenantHeader()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/token/issue",
            new { email = TestConstants.RootAdminEmail, password = TestConstants.DefaultPassword });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignIn_Should_CarryTheTenantClaim_Of_TheTenantHeader()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/issue");
        request.Headers.Add("tenant", TestConstants.RootTenantId);
        request.Content = JsonContent.Create(new { email = TestConstants.RootAdminEmail, password = TestConstants.DefaultPassword });

        using var response = await client.SendAsync(request);

        var token = await response.DeserializeAsync<TokenResult>();
        JwtPayload.Claim(token.AccessToken, "tenant").ShouldBe(TestConstants.RootTenantId);
    }

    [Fact]
    public void TheTenancyServices_Should_BeTheMultitenantOnes()
    {
        using var scope = _factory.Services.CreateScope();

        scope.ServiceProvider.GetService<ISingleTenantProvider>().ShouldBeNull();
        scope.ServiceProvider.GetRequiredService<IEventTenantScope>().ShouldBeOfType<FinbuckleEventTenantScope>();
        scope.ServiceProvider.GetServices<IMultiTenantStore<AppTenantInfo>>().Count().ShouldBeGreaterThan(1);
    }
}
