using System.Collections.ObjectModel;
using System.Net;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Framework.Mailing;
using Trislab.Framework.Mailing.Services;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.SendDocument;

/// <summary>Sends an issued document to the customer, with its PDF attached.</summary>
public sealed class SendDocumentCommandHandler(
    InvoiceDocumentsDbContext dbContext,
    IDocumentPdfRenderer renderer,
    IMailService mailService)
    : ICommandHandler<SendDocumentCommand, Guid>
{
    public async ValueTask<Guid> Handle(SendDocumentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var document = await dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == command.DocumentId, cancellationToken).ConfigureAwait(false)
            ?? throw DocumentErrors.DocumentNotFound(command.DocumentId);
        if (document.Status == DocumentStatus.Draft)
        {
            throw DocumentErrors.NotIssued();
        }

        var to = command.ToEmail;
        if (string.IsNullOrWhiteSpace(to))
        {
            to = await dbContext.Customers.AsNoTracking()
                .Where(c => c.Id == document.CustomerId)
                .Select(c => c.Email)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }
        if (string.IsNullOrWhiteSpace(to))
        {
            throw DocumentErrors.CustomerEmailMissing();
        }

        var issuerName = WebUtility.HtmlEncode(document.Issuer?.Name ?? string.Empty);
        var title = DocumentPdfRenderer.Title(document.Type);
        var subject = $"{title} {document.Number}";
        var note = string.IsNullOrWhiteSpace(command.Message) ? string.Empty : $"<p>{WebUtility.HtmlEncode(command.Message)}</p>";
        var body = $"<p>Please find the attached {WebUtility.HtmlEncode(title)} {WebUtility.HtmlEncode(document.Number)}.</p>{note}<p>{issuerName}</p>";
        var textBody = $"Please find the attached {title} {document.Number}.\n{command.Message}\n{document.Issuer?.Name}";

        var pdf = renderer.Render(document.ToDto());
        var attachments = new Dictionary<string, byte[]> { [$"{DocumentExportBuilder.FileSafe(document.Number!)}.pdf"] = pdf };
        await mailService.SendAsync(
            new MailRequest(new Collection<string> { to }, subject, body, attachmentData: attachments, textBody: textBody),
            cancellationToken).ConfigureAwait(false);

        document.MarkSent(DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return document.Id;
    }
}
