using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.InvoiceDocuments.Contracts.Dtos;

namespace Integration.Tests.Tests.InvoiceDocuments;

/// <summary>
/// Multi-tenant mode only: the data of one tenant is never seen by another, and a tenant that has not saved its
/// business details, or whose country has no rule pack, cannot create a document.
/// </summary>
[Collection(TrislabCollectionDefinition.Name)]
public sealed class InvoiceDocumentsTenantIsolationTests
{
    private const string BasePath = "/api/v1/invoice-documents";

    private readonly AuthHelper _auth;

    public InvoiceDocumentsTenantIsolationTests(TrislabWebApplicationFactory factory)
    {
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task Another_Tenant_Should_Not_See_Customers_Or_Documents_Of_The_Root_Tenant()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var name = $"Isolated {Guid.NewGuid():N}";
        using (var profile = await rootClient.PutAsJsonAsync($"{BasePath}/settings/issuer", IssuerBody("ZZ")))
        {
            profile.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var customer = await rootClient.PostAsJsonAsync($"{BasePath}/customers", CustomerBody(name));
        var customerId = await customer.DeserializeAsync<Guid>();
        using var draft = await rootClient.PostAsJsonAsync($"{BasePath}/documents", new
        {
            type = "Quote", customerId, lines = new[] { new { itemId = (Guid?)null, description = "Secret", quantity = 1m, unit = (string?)null, unitPrice = (decimal?)10m, taxCode = "S" } },
        });
        var documentId = await draft.DeserializeAsync<Guid>();

        using var otherClient = await CreateOtherTenantClientAsync();

        using var list = await otherClient.GetAsync($"{BasePath}/customers?search={name}");
        (await list.DeserializeAsync<PagedResponse<CustomerDto>>()).Items.ShouldBeEmpty();
        using var cross = await otherClient.GetAsync($"{BasePath}/documents/{documentId}");
        cross.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await cross.Content.ReadAsStringAsync()).ShouldNotContain("Secret");
        using var profileOfOther = await otherClient.GetAsync($"{BasePath}/settings/issuer");
        (await profileOfOther.Content.ReadAsStringAsync()).ShouldNotContain("Test Issuer");
    }

    [Fact]
    public async Task CreateDraft_Should_Return422_Without_Business_Details_Or_Without_A_Rule_Pack_For_The_Country()
    {
        using var otherClient = await CreateOtherTenantClientAsync();
        using var customer = await otherClient.PostAsJsonAsync($"{BasePath}/customers", CustomerBody($"Customer {Guid.NewGuid():N}"));
        var customerId = await customer.DeserializeAsync<Guid>();
        var body = new
        {
            type = "Invoice", customerId, lines = new[] { new { itemId = (Guid?)null, description = "Work", quantity = 1m, unit = (string?)null, unitPrice = (decimal?)10m, taxCode = "S" } },
        };

        using var noProfile = await otherClient.PostAsJsonAsync($"{BasePath}/documents", body);
        noProfile.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        // A country with no installed pack.
        using var save = await otherClient.PutAsJsonAsync($"{BasePath}/settings/issuer", IssuerBody("QQ"));
        save.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var noPack = await otherClient.PostAsJsonAsync($"{BasePath}/documents", body);
        noPack.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await noPack.Content.ReadAsStringAsync()).ShouldContain("QQ");
    }

    private async Task<HttpClient> CreateOtherTenantClientAsync()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var tenantId = $"invdoc-{uniqueId}";
        var adminEmail = $"invdoc-admin-{uniqueId}@tenant.com";

        var response = await rootClient.PostAsJsonAsync(TestConstants.TenantsBasePath, new
        {
            id = tenantId,
            name = $"Tenant {tenantId}",
            connectionString = (string?)null,
            adminEmail,
            adminPassword = TestConstants.DefaultPassword,
            issuer = $"{tenantId}.issuer",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        await TenantProvisioningWait.WaitForProvisioningAsync(rootClient, tenantId);

        for (int i = 0; i < 30; i++)
        {
            try
            {
                return await _auth.CreateAuthenticatedClientAsync(adminEmail, TestConstants.DefaultPassword, tenantId);
            }
            catch (HttpRequestException) when (i < 29)
            {
                await Task.Delay(1000);
            }
        }

        return await _auth.CreateAuthenticatedClientAsync(adminEmail, TestConstants.DefaultPassword, tenantId);
    }

    private static object IssuerBody(string countryCode) => new
    {
        name = "Test Issuer Ltd",
        addressLine = "Main street 1",
        postalCode = "1000",
        city = "Testville",
        countryCode,
        taxNumber = "ZZ99999999",
        iban = (string?)null,
        email = (string?)null,
        defaultCurrency = "EUR",
        defaultPaymentTermDays = 14,
    };

    private static object CustomerBody(string name) => new
    {
        name,
        email = (string?)null,
        addressLine = "Side street 2",
        postalCode = "2000",
        city = "Buyertown",
        countryCode = "ZZ",
        taxNumber = "ZZ12345678",
        note = (string?)null,
    };
}
