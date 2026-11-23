using Trislab.Modules.Notifications.IntegrationEventHandlers;

namespace Notifications.Tests.IntegrationEventHandlers;

/// <summary>
/// The invoice e-mail is built here, and the pair it returns is the whole contract: a
/// <c>Body</c> the provider sends as text/html and a <c>TextBody</c> the provider sends as the
/// text/plain alternative. Passing the markup as both is what this PR fixes downstream, and the only
/// thing that keeps the plain part honest is that it is written separately - so a template that
/// forgot it, or that reused the HTML, has to be caught here.
/// </summary>
public sealed class BillingEmailBodiesTests
{
    private static readonly DateTime ValidUpto = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    public static TheoryData<string, (string Subject, string Body, string TextBody)> AllTemplates() => new()
    {
        { "InvoiceIssued", BillingEmailBodies.InvoiceIssued("INV-2026-0001", 129.50m, "USD", ValidUpto) },
    };

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Template_Should_ReturnAnHtmlBodyAndAPlainTextTwin(
        string name,
        (string Subject, string Body, string TextBody) built)
    {
        // Assert
        built.Subject.ShouldNotBeNullOrWhiteSpace(name);

        built.Body.ShouldContain("<p", Case.Sensitive, name);
        built.Body.ShouldContain(built.Subject, Case.Sensitive, name);

        // The plain part is not allowed to be the markup, nor to be empty because someone passed the
        // HTML twice and trimmed it. No angle bracket survives the text twin.
        built.TextBody.ShouldNotBeNullOrWhiteSpace(name);
        built.TextBody.ShouldNotContain("<", Case.Sensitive, name);
        built.TextBody.ShouldContain(built.Subject, Case.Sensitive, name);
        built.TextBody.ShouldContain("This is an automated message.", Case.Sensitive, name);
    }

    [Fact]
    public void InvoiceIssued_Should_DropTheDueLine_When_ThereIsNoDueDate()
    {
        // Act
        var (_, body, text) = BillingEmailBodies.InvoiceIssued("INV-1", 10m, "USD", dueAtUtc: null);

        // Assert — no orphan label, and no blank paragraph where the optional line would have been.
        body.ShouldNotContain("Due by");
        text.ShouldNotContain("Due by");
        text.ShouldNotContain($"{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}");
    }
}
