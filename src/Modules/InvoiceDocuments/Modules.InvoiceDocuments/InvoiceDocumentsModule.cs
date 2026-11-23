using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Constants;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Web.Modules;
using Trislab.Modules.InvoiceDocuments.Contracts.Authorization;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Features.v1.Customers.CreateCustomer;
using Trislab.Modules.InvoiceDocuments.Features.v1.Customers.DeleteCustomer;
using Trislab.Modules.InvoiceDocuments.Features.v1.Customers.GetCustomers;
using Trislab.Modules.InvoiceDocuments.Features.v1.Customers.UpdateCustomer;
using Trislab.Modules.InvoiceDocuments.Features.v1.Documents.ConvertDocument;
using Trislab.Modules.InvoiceDocuments.Features.v1.Documents.CreateDocumentDraft;
using Trislab.Modules.InvoiceDocuments.Features.v1.Documents.DeleteDocumentDraft;
using Trislab.Modules.InvoiceDocuments.Features.v1.Documents.ExportDocuments;
using Trislab.Modules.InvoiceDocuments.Features.v1.Documents.GetDocumentById;
using Trislab.Modules.InvoiceDocuments.Features.v1.Documents.GetDocumentPdf;
using Trislab.Modules.InvoiceDocuments.Features.v1.Documents.GetDocuments;
using Trislab.Modules.InvoiceDocuments.Features.v1.Documents.IssueDocument;
using Trislab.Modules.InvoiceDocuments.Features.v1.Documents.MarkDocumentPaid;
using Trislab.Modules.InvoiceDocuments.Features.v1.Documents.SendDocument;
using Trislab.Modules.InvoiceDocuments.Features.v1.Documents.UpdateDocumentDraft;
using Trislab.Modules.InvoiceDocuments.Features.v1.Items.CreateItem;
using Trislab.Modules.InvoiceDocuments.Features.v1.Items.DeleteItem;
using Trislab.Modules.InvoiceDocuments.Features.v1.Items.GetItems;
using Trislab.Modules.InvoiceDocuments.Features.v1.Items.UpdateItem;
using Trislab.Modules.InvoiceDocuments.Features.v1.Settings.GetIssuerProfile;
using Trislab.Modules.InvoiceDocuments.Features.v1.Settings.UpdateIssuerProfile;
using Trislab.Modules.InvoiceDocuments.Services;

[assembly: TrislabModule(typeof(Trislab.Modules.InvoiceDocuments.InvoiceDocumentsModule), 520)]

namespace Trislab.Modules.InvoiceDocuments;

/// <summary>
/// The documents a tenant issues to its own customers: customers and items, quotes, pro formas, invoices, credit
/// notes and advance invoices, numbered by series that never change. Country law is not here: it is asked of a
/// country rule pack (<c>IInvoiceDocumentsRulePack</c>) the product installs. Not how a product charges its own
/// tenants: that is SubscriptionsUsage.
/// </summary>
public sealed class InvoiceDocumentsModule : IModule
{
    /// <summary>Works with and without a tenant catalog; data is stamped with the tenant in both modes.</summary>
    public TenancyModes SupportedTenancyModes => TenancyModes.All;

    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        PermissionConstants.Register(InvoiceDocumentsPermissions.All);

        builder.Services.AddTrislabDbContext<InvoiceDocumentsDbContext>(InvoiceDocumentsDbContext.Schema);
        builder.Services.AddScoped<IDbInitializer, InvoiceDocumentsDbInitializer>();

        // The packs are registered by their own packages; the resolver sees every one of them.
        builder.Services.AddSingleton<RulePackResolver>();
        builder.Services.AddScoped<DocumentSupport>();
        builder.Services.AddSingleton<IDocumentPdfRenderer, DocumentPdfRenderer>();

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<InvoiceDocumentsDbContext>(
                name: "db:invoicedocuments",
                failureStatus: HealthStatus.Unhealthy);
    }

    public void ConfigureMiddleware(IApplicationBuilder app)
    {
        // No custom middleware needed
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var versionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = endpoints
            .MapGroup("api/v{version:apiVersion}/invoice-documents")
            .WithTags("InvoiceDocuments")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();

        group.MapGetIssuerProfileEndpoint();
        group.MapUpdateIssuerProfileEndpoint();

        group.MapGetCustomersEndpoint();
        group.MapCreateCustomerEndpoint();
        group.MapUpdateCustomerEndpoint();
        group.MapDeleteCustomerEndpoint();

        group.MapGetItemsEndpoint();
        group.MapCreateItemEndpoint();
        group.MapUpdateItemEndpoint();
        group.MapDeleteItemEndpoint();

        // The literal /documents/export is registered before the id routes.
        group.MapExportDocumentsEndpoint();
        group.MapGetDocumentsEndpoint();
        group.MapCreateDocumentDraftEndpoint();
        group.MapGetDocumentByIdEndpoint();
        group.MapUpdateDocumentDraftEndpoint();
        group.MapDeleteDocumentDraftEndpoint();
        group.MapIssueDocumentEndpoint();
        group.MapMarkDocumentPaidEndpoint();
        group.MapConvertDocumentEndpoint();
        group.MapSendDocumentEndpoint();
        group.MapGetDocumentPdfEndpoint();
    }
}
