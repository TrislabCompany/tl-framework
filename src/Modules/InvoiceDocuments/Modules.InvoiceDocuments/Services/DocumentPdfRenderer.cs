using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.Dtos;

namespace Trislab.Modules.InvoiceDocuments.Services;

public interface IDocumentPdfRenderer
{
    byte[] Render(DocumentDto document);
}

/// <summary>
/// QuestPDF-based renderer. The layout is country-neutral; the text the law requires comes from the
/// country rule pack, copied onto the document at issue time (<c>HeaderLines</c> and <c>FooterLines</c>).
/// QuestPDF's Community license is free for organisations under 1M USD a year; the dependency is isolated
/// behind <see cref="IDocumentPdfRenderer"/> so it can be swapped.
/// </summary>
public sealed class DocumentPdfRenderer : IDocumentPdfRenderer
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    static DocumentPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static string Title(DocumentType type) => type switch
    {
        DocumentType.Quote => "QUOTE",
        DocumentType.ProForma => "PRO FORMA INVOICE",
        DocumentType.Invoice => "INVOICE",
        DocumentType.CreditNote => "CREDIT NOTE",
        DocumentType.AdvanceInvoice => "ADVANCE INVOICE",
        _ => "DOCUMENT",
    };

    public byte[] Render(DocumentDto document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Column(col =>
                {
                    col.Item().Text(Title(document.Type)).FontSize(22).Bold();
                    col.Item().Text(document.Number ?? "DRAFT").FontSize(12).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c => Party(c, "From", document.Issuer));
                        row.RelativeItem().Column(c => Party(c, "To", document.Customer));
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            if (document.IssueDate is { } issued)
                            {
                                c.Item().Text($"Date: {issued:yyyy-MM-dd}");
                            }
                            if (document.DueDate is { } due)
                            {
                                c.Item().Text($"Due: {due:yyyy-MM-dd}");
                            }
                            c.Item().Text($"Currency: {document.Currency}");
                        });
                    });

                    foreach (var line in document.HeaderLines)
                    {
                        col.Item().Text(line);
                    }

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(24);
                            c.RelativeColumn(4);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.4f);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("#").SemiBold();
                            h.Cell().Text("Description").SemiBold();
                            h.Cell().AlignRight().Text("Qty").SemiBold();
                            h.Cell().AlignRight().Text("Unit price").SemiBold();
                            h.Cell().AlignRight().Text("Tax").SemiBold();
                            h.Cell().AlignRight().Text("Total").SemiBold();
                        });

                        foreach (var line in document.Lines)
                        {
                            table.Cell().Text(line.Position.ToString(Culture));
                            table.Cell().Column(c =>
                            {
                                c.Item().Text(line.Description);
                                if (!string.IsNullOrWhiteSpace(line.TaxNote))
                                {
                                    c.Item().Text(line.TaxNote).FontSize(8).FontColor(Colors.Grey.Darken1);
                                }
                            });
                            table.Cell().AlignRight().Text($"{line.Quantity.ToString("0.####", Culture)} {line.Unit}".TrimEnd());
                            table.Cell().AlignRight().Text(line.UnitPrice.ToString("N2", Culture));
                            table.Cell().AlignRight().Text($"{(line.TaxRate * 100).ToString("0.##", Culture)}%");
                            table.Cell().AlignRight().Text(line.Gross.ToString("N2", Culture));
                        }
                    });

                    col.Item().AlignRight().Column(c =>
                    {
                        c.Item().Text($"Net: {document.TotalNet.ToString("N2", Culture)} {document.Currency}");
                        c.Item().Text($"Tax: {document.TotalTax.ToString("N2", Culture)} {document.Currency}");
                        c.Item().Text($"Total: {document.TotalGross.ToString("N2", Culture)} {document.Currency}").Bold().FontSize(12);
                    });

                    if (!string.IsNullOrWhiteSpace(document.IssuerIban) && DocumentIsPayable(document.Type))
                    {
                        col.Item().Text($"Pay to: {document.IssuerIban}");
                    }
                    if (!string.IsNullOrWhiteSpace(document.Notes))
                    {
                        col.Item().Text(document.Notes);
                    }
                    foreach (var line in document.FooterLines)
                    {
                        col.Item().Text(line).FontSize(8).FontColor(Colors.Grey.Darken1);
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ").FontSize(8);
                    t.CurrentPageNumber().FontSize(8);
                });
            });
        }).GeneratePdf();
    }

    private static bool DocumentIsPayable(DocumentType type) => type is DocumentType.Invoice or DocumentType.AdvanceInvoice or DocumentType.ProForma;

    private static void Party(ColumnDescriptor column, string label, DocumentPartyDto? party)
    {
        column.Item().Text(label).SemiBold();
        if (party is null)
        {
            return;
        }

        column.Item().Text(party.Name);
        column.Item().Text(party.AddressLine);
        column.Item().Text($"{party.PostalCode} {party.City}".Trim());
        column.Item().Text(party.CountryCode);
        if (!string.IsNullOrWhiteSpace(party.TaxNumber))
        {
            column.Item().Text($"Tax no.: {party.TaxNumber}");
        }
    }
}
