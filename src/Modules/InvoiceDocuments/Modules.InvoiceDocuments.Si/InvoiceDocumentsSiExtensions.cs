using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trislab.Modules.InvoiceDocuments.Contracts.RulePack;

namespace Trislab.Modules.InvoiceDocuments.Si;

public static class InvoiceDocumentsSiExtensions
{
    /// <summary>
    /// Installs the Slovenian rule pack: tenants whose business details say country <c>SI</c> are issued documents
    /// under Slovenian law. Call it in the host's service registration, next to the InvoiceDocuments module.
    /// </summary>
    public static IServiceCollection AddInvoiceDocumentsSi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IInvoiceDocumentsRulePack, SlovenianRulePack>());
        return services;
    }
}
