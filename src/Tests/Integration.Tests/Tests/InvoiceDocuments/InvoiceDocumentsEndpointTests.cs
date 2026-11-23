using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Eventing.Persistence;
using Trislab.Framework.Mailing;
using Trislab.Framework.Mailing.Services;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.InvoiceDocuments;

/// <summary>
/// The InvoiceDocuments module end to end, with the test country rule pack (ZZ). The class runs in both tenancy modes:
/// see <see cref="InvoiceDocumentsEndpointTests"/> and <see cref="InvoiceDocumentsEndpointTestsInSingleTenantMode"/>.
/// </summary>
public abstract class InvoiceDocumentsEndpointTestsBase
{
    private const string BasePath = "/api/v1/invoice-documents";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    protected InvoiceDocumentsEndpointTestsBase(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    // ─── auth gating ─────────────────────────────────────────────────

    [Fact]
    public async Task ListDocuments_Should_Return401_When_Unauthenticated()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("tenant", _factory.TenantId);

        using var response = await client.GetAsync($"{BasePath}/documents");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ─── customers and items ─────────────────────────────────────────

    [Fact]
    public async Task Customer_Should_Be_Created_Listed_Updated_And_Deleted()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var name = $"Customer {Guid.NewGuid():N}";

        var id = await CreateCustomerAsync(client, name, email: "c@example.test");

        var found = await ListCustomersAsync(client, name);
        found.Items.ShouldContain(c => c.Id == id && c.CountryCode == "ZZ");

        using var update = await client.PutAsJsonAsync($"{BasePath}/customers/{id}", NewCustomerBody($"{name} Ltd", "c@example.test"));
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ListCustomersAsync(client, $"{name} Ltd")).Items.ShouldContain(c => c.Id == id);

        using var delete = await client.DeleteAsync($"{BasePath}/customers/{id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ListCustomersAsync(client, name)).Items.ShouldNotContain(c => c.Id == id);
    }

    [Fact]
    public async Task CreateCustomer_Should_Return400_When_CountryCode_IsNotTwoLetters()
    {
        using var client = await _auth.CreateRootAdminClientAsync();

        using var response = await client.PostAsJsonAsync($"{BasePath}/customers", NewCustomerBody("Bad", null, countryCode: "ZZZ"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Item_Should_Be_Created_Listed_Updated_And_Deleted()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var name = $"Item {Guid.NewGuid():N}";

        var id = await CreateItemAsync(client, name, 12.5m, "S");

        var found = await ListAsync<ItemDto>(client, $"{BasePath}/items?search={name}");
        found.Items.ShouldContain(i => i.Id == id && i.UnitPrice == 12.5m && i.TaxCode == "S");

        using var update = await client.PutAsJsonAsync($"{BasePath}/items/{id}",
            new { name, description = (string?)null, unit = "h", unitPrice = 15m, taxCode = "Z" });
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ListAsync<ItemDto>(client, $"{BasePath}/items?search={name}")).Items
            .ShouldContain(i => i.Id == id && i.UnitPrice == 15m && i.TaxCode == "Z");

        using var delete = await client.DeleteAsync($"{BasePath}/items/{id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ListAsync<ItemDto>(client, $"{BasePath}/items?search={name}")).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task IssuerProfile_Should_Be_Saved_And_Read_Back()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        await SaveIssuerProfileAsync(client);

        var profile = await (await client.GetAsync($"{BasePath}/settings/issuer")).DeserializeAsync<IssuerProfileDto>();

        profile.CountryCode.ShouldBe("ZZ");
        profile.Name.ShouldBe("Test Issuer Ltd");
        profile.Iban.ShouldBe("ZZ00TEST000000000001");
    }

    // ─── invoice lifecycle ───────────────────────────────────────────

    [Fact]
    public async Task Invoice_Should_Be_Drafted_Priced_Issued_Locked_And_Paid()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        await SaveIssuerProfileAsync(client);
        var customerId = await CreateCustomerAsync(client, $"Customer {Guid.NewGuid():N}", "buyer@example.test");
        var itemId = await CreateItemAsync(client, $"Item {Guid.NewGuid():N}", 100m, "S");

        var draftId = await CreateDraftAsync(client, DocumentType.Invoice, customerId,
            Line(itemId, quantity: 2),
            Line(null, quantity: 1, description: "Zero rated work", unitPrice: 50m, taxCode: "Z"));
        var draft = await GetDocumentAsync(client, draftId);
        draft.Status.ShouldBe(DocumentStatus.Draft);
        draft.Number.ShouldBeNull();
        draft.TotalNet.ShouldBe(250m);
        draft.TotalTax.ShouldBe(40m);
        draft.TotalGross.ShouldBe(290m);
        draft.Lines.Count.ShouldBe(2);
        draft.Lines[1].TaxNote.ShouldBe("Zero rated");

        using var issue = await client.PostAsync($"{BasePath}/documents/{draftId}/issue", content: null);
        issue.StatusCode.ShouldBe(HttpStatusCode.OK);

        var issued = await GetDocumentAsync(client, draftId);
        issued.Status.ShouldBe(DocumentStatus.Issued);
        issued.Number.ShouldNotBeNull();
        issued.Number.ShouldStartWith($"INV-{DateTime.UtcNow.Year}-");
        issued.Issuer!.Name.ShouldBe("Test Issuer Ltd");
        issued.HeaderLines.ShouldContain("Test pack header");
        issued.FooterLines.ShouldContain("Kept for 10 years");
        issued.DueDate.ShouldNotBeNull();

        // An issued document never changes and is never deleted.
        using var edit = await client.PutAsJsonAsync($"{BasePath}/documents/{draftId}",
            new { customerId, dueDate = (DateOnly?)null, notes = "changed", lines = new[] { Line(itemId, 1) } });
        edit.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var delete = await client.DeleteAsync($"{BasePath}/documents/{draftId}");
        delete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var issueAgain = await client.PostAsync($"{BasePath}/documents/{draftId}/issue", content: null);
        issueAgain.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Open before it is paid.
        var open = await ListAsync<DocumentSummaryDto>(client, $"{BasePath}/documents?openOnly=true&customerId={customerId}");
        open.Items.ShouldContain(d => d.Id == draftId && !d.IsOverdue);

        using var pay = await client.PostAsync($"{BasePath}/documents/{draftId}/mark-paid", content: null);
        pay.StatusCode.ShouldBe(HttpStatusCode.OK);
        var paid = await GetDocumentAsync(client, draftId);
        paid.Status.ShouldBe(DocumentStatus.Paid);
        paid.PaidAtUtc.ShouldNotBeNull();
        using var payAgain = await client.PostAsync($"{BasePath}/documents/{draftId}/mark-paid", content: null);
        payAgain.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ListAsync<DocumentSummaryDto>(client, $"{BasePath}/documents?openOnly=true&customerId={customerId}"))
            .Items.ShouldNotContain(d => d.Id == draftId);

        await AssertEventAsync("InvoiceDocumentIssuedIntegrationEvent", draftId);
        await AssertEventAsync("InvoiceDocumentPaidIntegrationEvent", draftId);
    }

    [Fact]
    public async Task Numbers_Should_Follow_The_Series_Without_Gaps_Or_Repeats()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        await SaveIssuerProfileAsync(client);
        var customerId = await CreateCustomerAsync(client, $"Customer {Guid.NewGuid():N}", null);

        var numbers = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var id = await CreateDraftAsync(client, DocumentType.Quote, customerId, Line(null, 1, "Work", 10m, "S"));
            (await client.PostAsync($"{BasePath}/documents/{id}/issue", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
            numbers.Add((await GetDocumentAsync(client, id)).Number!);
        }

        var sequences = numbers.Select(n => int.Parse(n[^4..], System.Globalization.CultureInfo.InvariantCulture)).ToList();
        sequences[1].ShouldBe(sequences[0] + 1);
        sequences[2].ShouldBe(sequences[1] + 1);
    }

    [Fact]
    public async Task Parallel_Issues_Should_Not_Share_A_Number()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        await SaveIssuerProfileAsync(client);
        var customerId = await CreateCustomerAsync(client, $"Customer {Guid.NewGuid():N}", null);
        var ids = new List<Guid>();
        for (int i = 0; i < 4; i++)
        {
            ids.Add(await CreateDraftAsync(client, DocumentType.ProForma, customerId, Line(null, 1, "Work", 10m, "S")));
        }

        var responses = await Task.WhenAll(ids.Select(id => client.PostAsync($"{BasePath}/documents/{id}/issue", content: null)));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        var numbers = new List<string>();
        foreach (var id in ids)
        {
            numbers.Add((await GetDocumentAsync(client, id)).Number!);
        }

        numbers.Distinct().Count().ShouldBe(4);
    }

    [Fact]
    public async Task Issue_Should_Return422_When_The_Rule_Pack_Rejects_The_Document()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        await SaveIssuerProfileAsync(client);
        var customerId = await CreateCustomerAsync(client, $"Customer {Guid.NewGuid():N}", null, taxNumber: null);
        var id = await CreateDraftAsync(client, DocumentType.Invoice, customerId, Line(null, 1, "Big job", 5000m, "S"));

        using var response = await client.PostAsync($"{BasePath}/documents/{id}/issue", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await GetDocumentAsync(client, id)).Status.ShouldBe(DocumentStatus.Draft);
    }

    // ─── pro forma, quote, credit note, advance invoice ──────────────

    [Fact]
    public async Task ProForma_Should_Convert_To_An_Invoice_Draft()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        await SaveIssuerProfileAsync(client);
        var customerId = await CreateCustomerAsync(client, $"Customer {Guid.NewGuid():N}", null);
        var proFormaId = await CreateDraftAsync(client, DocumentType.ProForma, customerId, Line(null, 3, "Plan", 20m, "S"));

        // A draft cannot be converted.
        using var early = await client.PostAsync($"{BasePath}/documents/{proFormaId}/convert?targetType=Invoice", content: null);
        early.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await client.PostAsync($"{BasePath}/documents/{proFormaId}/issue", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var convert = await client.PostAsync($"{BasePath}/documents/{proFormaId}/convert?targetType=Invoice", content: null);
        convert.StatusCode.ShouldBe(HttpStatusCode.OK);
        var invoiceId = await convert.DeserializeAsync<Guid>();

        var invoice = await GetDocumentAsync(client, invoiceId);
        invoice.Type.ShouldBe(DocumentType.Invoice);
        invoice.Status.ShouldBe(DocumentStatus.Draft);
        invoice.TotalGross.ShouldBe(72m);
        invoice.CustomerId.ShouldBe(customerId);
        var source = await GetDocumentAsync(client, proFormaId);
        source.Status.ShouldBe(DocumentStatus.Converted);
        source.ConvertedToDocumentId.ShouldBe(invoiceId);

        // A converted document cannot be converted again, and an invoice is not converted.
        using var again = await client.PostAsync($"{BasePath}/documents/{proFormaId}/convert?targetType=Invoice", content: null);
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Quote_Should_Not_Convert_To_A_Credit_Note()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        await SaveIssuerProfileAsync(client);
        var customerId = await CreateCustomerAsync(client, $"Customer {Guid.NewGuid():N}", null);
        var quoteId = await CreateDraftAsync(client, DocumentType.Quote, customerId, Line(null, 1, "Offer", 100m, "S"));
        (await client.PostAsync($"{BasePath}/documents/{quoteId}/issue", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await client.PostAsync($"{BasePath}/documents/{quoteId}/convert?targetType=CreditNote", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreditNote_Should_Refer_To_An_Invoice_And_Stay_Within_Its_Total()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        await SaveIssuerProfileAsync(client);
        var customerId = await CreateCustomerAsync(client, $"Customer {Guid.NewGuid():N}", null, taxNumber: "ZZ123");
        var invoiceId = await CreateDraftAsync(client, DocumentType.Invoice, customerId, Line(null, 1, "Job", 100m, "S"));

        // A credit note against a draft invoice is refused.
        using var early = await client.PostAsJsonAsync($"{BasePath}/documents", new
        {
            type = "CreditNote", customerId, referenceDocumentId = invoiceId,
            lines = new[] { Line(null, 1, "Refund", 10m, "S") },
        });
        early.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        (await client.PostAsync($"{BasePath}/documents/{invoiceId}/issue", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // A credit note without an invoice is refused by validation.
        using var noReference = await client.PostAsJsonAsync($"{BasePath}/documents", new
        {
            type = "CreditNote", customerId, lines = new[] { Line(null, 1, "Refund", 10m, "S") },
        });
        noReference.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // The invoice is 120 gross. The first credit note is 60 gross; a second of 72 gross would pass 120.
        var first = await CreateCreditNoteAsync(client, customerId, invoiceId, 50m);
        (await client.PostAsync($"{BasePath}/documents/{first}/issue", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var creditNote = await GetDocumentAsync(client, first);
        creditNote.Number.ShouldStartWith("CN-");
        creditNote.ReferenceDocumentId.ShouldBe(invoiceId);
        creditNote.Currency.ShouldBe("EUR");

        var second = await CreateCreditNoteAsync(client, customerId, invoiceId, 60m);
        using var tooMuch = await client.PostAsync($"{BasePath}/documents/{second}/issue", content: null);
        tooMuch.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        // A credit note is not paid.
        using var pay = await client.PostAsync($"{BasePath}/documents/{first}/mark-paid", content: null);
        pay.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AdvanceInvoice_Should_Be_Issued_And_Paid_Like_An_Invoice()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        await SaveIssuerProfileAsync(client);
        var customerId = await CreateCustomerAsync(client, $"Customer {Guid.NewGuid():N}", null);
        var id = await CreateDraftAsync(client, DocumentType.AdvanceInvoice, customerId, Line(null, 1, "Deposit", 200m, "S"));

        (await client.PostAsync($"{BasePath}/documents/{id}/issue", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsync($"{BasePath}/documents/{id}/mark-paid", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var doc = await GetDocumentAsync(client, id);
        doc.Number.ShouldStartWith("ADV-");
        doc.Status.ShouldBe(DocumentStatus.Paid);
    }

    [Fact]
    public async Task Overdue_Invoice_Should_Be_Listed_As_Overdue()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        await SaveIssuerProfileAsync(client);
        var customerId = await CreateCustomerAsync(client, $"Customer {Guid.NewGuid():N}", null);
        var past = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);
        using var create = await client.PostAsJsonAsync($"{BasePath}/documents", new
        {
            type = "Invoice", customerId, dueDate = past.AddDays(10), lines = new[] { Line(null, 1, "Old job", 10m, "S") },
        });
        var id = await create.DeserializeAsync<Guid>();
        (await client.PostAsync($"{BasePath}/documents/{id}/issue?issueDate={past:yyyy-MM-dd}", content: null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var overdue = await ListAsync<DocumentSummaryDto>(client, $"{BasePath}/documents?overdueOnly=true&customerId={customerId}");

        overdue.Items.ShouldContain(d => d.Id == id && d.IsOverdue);
    }

    // ─── PDF, e-mail, export ─────────────────────────────────────────

    [Fact]
    public async Task Pdf_Should_Be_Refused_For_A_Draft_And_Served_For_An_Issued_Document()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        await SaveIssuerProfileAsync(client);
        var customerId = await CreateCustomerAsync(client, $"Customer {Guid.NewGuid():N}", null);
        var id = await CreateDraftAsync(client, DocumentType.Invoice, customerId, Line(null, 1, "Job", 10m, "S"));

        using var draftPdf = await client.GetAsync($"{BasePath}/documents/{id}/pdf");
        draftPdf.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await client.PostAsync($"{BasePath}/documents/{id}/issue", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var pdf = await client.GetAsync($"{BasePath}/documents/{id}/pdf");
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).ShouldBe("%PDF");
    }

    [Fact]
    public async Task Send_Should_Email_The_Pdf_To_The_Customer()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        await SaveIssuerProfileAsync(client);
        var email = $"buyer-{Guid.NewGuid():N}@example.test";
        var customerId = await CreateCustomerAsync(client, $"Customer {Guid.NewGuid():N}", email);
        var id = await CreateDraftAsync(client, DocumentType.Invoice, customerId, Line(null, 1, "Job", 10m, "S"));

        // A draft is not sent.
        using var early = await client.PostAsJsonAsync($"{BasePath}/documents/{id}/send", new { });
        early.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await client.PostAsync($"{BasePath}/documents/{id}/issue", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var send = await client.PostAsJsonAsync($"{BasePath}/documents/{id}/send", new { message = "Thank you" });
        send.StatusCode.ShouldBe(HttpStatusCode.OK);

        var mail = ((NoOpMailService)_factory.Services.GetRequiredService<IMailService>()).Sent
            .Last(m => m.To.Contains(email));
        mail.AttachmentData.Keys.ShouldContain(k => k.EndsWith(".pdf", StringComparison.Ordinal));
        mail.Body!.ShouldContain("Thank you");
        (await GetDocumentAsync(client, id)).SentAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Export_Should_Contain_A_Csv_And_A_Pdf_For_Every_Issued_Document_Of_The_Period()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        await SaveIssuerProfileAsync(client);
        var customerId = await CreateCustomerAsync(client, $"Export, \"quoted\" {Guid.NewGuid():N}", null);
        var issuedId = await CreateDraftAsync(client, DocumentType.Invoice, customerId, Line(null, 1, "Job", 10m, "S"));
        var draftId = await CreateDraftAsync(client, DocumentType.Invoice, customerId, Line(null, 1, "Not issued", 10m, "S"));
        var date = new DateOnly(2031, 3, 15);
        (await client.PostAsync($"{BasePath}/documents/{issuedId}/issue?issueDate={date:yyyy-MM-dd}", content: null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        var number = (await GetDocumentAsync(client, issuedId)).Number!;

        using var response = await client.GetAsync($"{BasePath}/documents/export?from=2031-03-01&to=2031-03-31");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/zip");
        using var zip = new ZipArchive(await response.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);
        zip.Entries.Select(e => e.FullName).ShouldContain("documents.csv");
        zip.Entries.Select(e => e.FullName).ShouldContain($"pdf/{number}.pdf");
        await using var csvStream = await zip.GetEntry("documents.csv")!.OpenAsync();
        using var reader = new StreamReader(csvStream);
        var csv = await reader.ReadToEndAsync();
        csv.ShouldContain(number);
        csv.ShouldContain("\"Export, \"\"quoted\"\"");
        (await GetDocumentAsync(client, draftId)).Status.ShouldBe(DocumentStatus.Draft);
        csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Count(l => l.Contains(number, StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task Export_Should_Return400_When_The_Period_Is_Missing()
    {
        using var client = await _auth.CreateRootAdminClientAsync();

        using var response = await client.GetAsync($"{BasePath}/documents/export");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ─── helpers ─────────────────────────────────────────────────────

    protected async Task AssertEventAsync(string typeName, Guid documentId)
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider
            .GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(_factory.TenantId);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenant);
        var eventing = scope.ServiceProvider.GetRequiredService<EventingDbContext>();

        var rows = await eventing.OutboxMessages.AsNoTracking()
            .Where(m => m.Type.Contains(typeName))
            .ToListAsync();

        rows.ShouldContain(m => m.Payload.Contains(documentId.ToString(), StringComparison.OrdinalIgnoreCase),
            $"the {typeName} for the document must be in the outbox");
    }

    protected static async Task SaveIssuerProfileAsync(HttpClient client)
    {
        using var response = await client.PutAsJsonAsync($"{BasePath}/settings/issuer", new
        {
            name = "Test Issuer Ltd",
            addressLine = "Main street 1",
            postalCode = "1000",
            city = "Testville",
            countryCode = "ZZ",
            taxNumber = "ZZ99999999",
            iban = "ZZ00 TEST 0000 0000 0001",
            email = "issuer@example.test",
            defaultCurrency = "EUR",
            defaultPaymentTermDays = 14,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    protected static object NewCustomerBody(string name, string? email, string countryCode = "ZZ", string? taxNumber = "ZZ12345678")
        => new
        {
            name,
            email,
            addressLine = "Side street 2",
            postalCode = "2000",
            city = "Buyertown",
            countryCode,
            taxNumber,
            note = (string?)null,
        };

    protected static async Task<Guid> CreateCustomerAsync(HttpClient client, string name, string? email, string? taxNumber = "ZZ12345678")
    {
        using var response = await client.PostAsJsonAsync($"{BasePath}/customers", NewCustomerBody(name, email, taxNumber: taxNumber));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }

    protected static async Task<Guid> CreateItemAsync(HttpClient client, string name, decimal price, string taxCode)
    {
        using var response = await client.PostAsJsonAsync($"{BasePath}/items",
            new { name, description = (string?)null, unit = "pcs", unitPrice = price, taxCode });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }

    protected static object Line(Guid? itemId, decimal quantity, string? description = null, decimal? unitPrice = null, string? taxCode = null)
        => new { itemId, description, quantity, unit = (string?)null, unitPrice, taxCode };

    protected static async Task<Guid> CreateDraftAsync(HttpClient client, DocumentType type, Guid customerId, params object[] lines)
    {
        using var response = await client.PostAsJsonAsync($"{BasePath}/documents", new { type = type.ToString(), customerId, lines });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }

    private static async Task<Guid> CreateCreditNoteAsync(HttpClient client, Guid customerId, Guid invoiceId, decimal unitPrice)
    {
        using var response = await client.PostAsJsonAsync($"{BasePath}/documents", new
        {
            type = "CreditNote", customerId, referenceDocumentId = invoiceId,
            lines = new[] { Line(null, 1, "Refund", unitPrice, "S") },
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.DeserializeAsync<Guid>();
    }

    protected static async Task<DocumentDto> GetDocumentAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"{BasePath}/documents/{id}");
        return await response.DeserializeAsync<DocumentDto>();
    }

    private static Task<PagedResponse<CustomerDto>> ListCustomersAsync(HttpClient client, string search)
        => ListAsync<CustomerDto>(client, $"{BasePath}/customers?search={Uri.EscapeDataString(search)}");

    protected static async Task<PagedResponse<T>> ListAsync<T>(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        return await response.DeserializeAsync<PagedResponse<T>>();
    }

    protected static JsonSerializerOptions Json => JsonOptions;
}

[Collection(TrislabCollectionDefinition.Name)]
public sealed class InvoiceDocumentsEndpointTests(TrislabWebApplicationFactory factory) : InvoiceDocumentsEndpointTestsBase(factory);

[Collection(SingleTenantCollectionDefinition.Name)]
public sealed class InvoiceDocumentsEndpointTestsInSingleTenantMode(SingleTenantWebApplicationFactory factory)
    : InvoiceDocumentsEndpointTestsBase(factory);
