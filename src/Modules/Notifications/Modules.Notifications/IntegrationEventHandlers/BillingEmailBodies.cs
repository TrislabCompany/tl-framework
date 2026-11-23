// TODO(i18n): email bodies are localized in a follow-up PR — recipient locale must be propagated (no HTTP request culture in background handlers).
using System.Globalization;
using Trislab.Framework.Mailing;

namespace Trislab.Modules.Notifications.IntegrationEventHandlers;

/// <summary>
/// Builds the subject + HTML body + text/plain alternative for the invoice e-mail. Plain interpolated
/// HTML (the framework has no template engine); kept here so the handlers stay thin and the copy is easy
/// to review. Every message carries both parts: HTML-only mail leaves text-only clients with nothing and
/// scores worse with spam filters. The document shell and the encoder come from
/// <see cref="HtmlEmail"/> so every module escapes the same way.
/// </summary>
internal static class BillingEmailBodies
{
    private static string Date(DateTime utc) => utc.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);

    public static (string Subject, string Body, string TextBody) InvoiceIssued(string invoiceNumber, decimal amount, string currency, DateTime? dueAtUtc)
    {
        var subject = $"Invoice {invoiceNumber} issued";
        var amountText = $"{amount.ToString("0.00", CultureInfo.InvariantCulture)} {currency}";
        var due = dueAtUtc is null ? string.Empty : $"<p>Due by <strong>{Date(dueAtUtc.Value)}</strong>.</p>";
        var body = Wrap(subject,
            // amountText embeds the currency, which is data rather than a literal, so it is encoded
            // for the HTML part while the text/plain twin below keeps it verbatim.
            $"<p>A new invoice <strong>{HtmlEmail.Encode(invoiceNumber)}</strong> for <strong>{HtmlEmail.Encode(amountText)}</strong> has been issued.</p>" +
            due +
            "<p>You can view and download this invoice from your dashboard.</p>");
        var text = Text(subject,
            $"A new invoice {invoiceNumber} for {amountText} has been issued.",
            dueAtUtc is null ? string.Empty : $"Due by {Date(dueAtUtc.Value)}.",
            "You can view and download this invoice from your dashboard.");
        return (subject, body, text);
    }

    /// <summary>
    /// The billing copy inside the shared document shell. Only the automated-message footer is
    /// specific to these e-mails; the doctype, charset and card come from <see cref="HtmlEmail.Shell"/>.
    /// </summary>
    private static string Wrap(string heading, string innerHtml) =>
        HtmlEmail.Shell(
            heading,
            "<div style=\"font-size: 15px; line-height: 1.6; color: #334155;\">" +
            innerHtml +
            "<p style=\"margin-top:24px;color:#6b7280;font-size:12px\">This is an automated message.</p>" +
            "</div>");

    /// <summary>
    /// The text/plain twin of <see cref="Wrap"/>: same copy, no markup, empty paragraphs dropped so an
    /// optional line (an absent due date) does not leave a blank gap.
    /// </summary>
    private static string Text(string heading, params string[] paragraphs)
    {
        // The blank entries this used to add were filtered straight back out by the Join, which is
        // what actually separates the paragraphs.
        var lines = new List<string> { heading };
        lines.AddRange(paragraphs.Where(p => !string.IsNullOrWhiteSpace(p)));
        lines.Add("This is an automated message.");
        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }
}
