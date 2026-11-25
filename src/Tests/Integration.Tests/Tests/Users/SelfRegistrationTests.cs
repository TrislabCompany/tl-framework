using System.Diagnostics;
using Integration.Tests.Infrastructure;
using Trislab.Modules.Identity.Data;

namespace Integration.Tests.Tests.Users;

/// <summary>
/// Self-registration never reveals whether an address has an account: the same answer, the same least
/// duration, and the owner of an existing account gets an e-mail instead. The operator console has no sign-up.
/// </summary>
[Collection(TrislabCollectionDefinition.Name)]
public sealed class SelfRegistrationTests
{
    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public SelfRegistrationTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task SelfRegister_Answers_The_Same_For_A_New_And_An_Existing_Address()
    {
        var tenantId = await CreateTenantAsync();
        var existingEmail = $"selfexist-{Guid.NewGuid():N}@example.com";
        (await SelfRegisterAsync(tenantId, existingEmail)).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        static bool IsSignUpNotice(MailpitMessage m) => m.Subject.Contains("sign up", StringComparison.OrdinalIgnoreCase);
        var noticesBefore = (await Mailpit.MessagesToAsync(existingEmail)).Count(IsSignUpNotice);

        var timer = Stopwatch.StartNew();
        using var fresh = await SelfRegisterAsync(tenantId, $"selfnew-{Guid.NewGuid():N}@example.com");
        var freshElapsed = timer.Elapsed;
        timer.Restart();
        using var again = await SelfRegisterAsync(tenantId, existingEmail, password: "Other@1234!");
        var againElapsed = timer.Elapsed;

        fresh.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        again.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await again.Content.ReadAsStringAsync()).ShouldBe(await fresh.Content.ReadAsStringAsync());

        // Both take at least the configured floor (1 s), so the timing carries no signal.
        freshElapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(950));
        againElapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(950));

        // The owner of the existing account is told by e-mail, and no second account exists.
        await Mailpit.WaitForMessageAsync(existingEmail, IsSignUpNotice, skip: noticesBefore);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var normalized = existingEmail.ToUpperInvariant();
        (await db.Users.IgnoreQueryFilters().CountAsync(u => u.NormalizedEmail == normalized)).ShouldBe(1);
    }

    [Fact]
    public async Task SelfRegister_Should_Return400_When_PayloadInvalid()
    {
        var tenantId = await CreateTenantAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("tenant", tenantId);

        var response = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/self-register", new
        {
            firstName = "",
            lastName = "",
            email = "not-an-email",
            userName = "",
            password = "weak",
            confirmPassword = "weak"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SelfRegister_Is_Closed_For_The_Operator_Console()
    {
        (await SelfRegisterAsync(TestConstants.RootTenantId, $"selfroot-{Guid.NewGuid():N}@example.com")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> SelfRegisterAsync(string tenantId, string email, string password = "Test@1234!")
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("tenant", tenantId);
        return await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/self-register", new
        {
            firstName = "Self",
            lastName = "Reg",
            email,
            userName = $"self-{Guid.NewGuid():N}"[..20],
            password,
            confirmPassword = password,
        });
    }

    private async Task<string> CreateTenantAsync()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var tenantId = $"selfreg-{Guid.NewGuid().ToString("N")[..8]}";
        var response = await rootClient.PostAsJsonAsync(TestConstants.TenantsBasePath, new
        {
            id = tenantId,
            name = $"Tenant {tenantId}",
            connectionString = (string?)null,
            adminEmail = $"admin-{tenantId}@tenant.com",
            adminPassword = TestConstants.DefaultPassword,
            issuer = $"{tenantId}.issuer",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        await TenantProvisioningWait.WaitForProvisioningAsync(rootClient, tenantId);
        return tenantId;
    }
}
