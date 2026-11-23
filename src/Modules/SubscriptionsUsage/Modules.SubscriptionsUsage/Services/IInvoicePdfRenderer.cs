using Trislab.Modules.SubscriptionsUsage.Contracts.Dtos;

namespace Trislab.Modules.SubscriptionsUsage.Services;

/// <summary>Renders an invoice to a self-contained PDF document (on-demand, no stored artifact).</summary>
public interface IInvoicePdfRenderer
{
    byte[] Render(InvoiceDto invoice);
}
