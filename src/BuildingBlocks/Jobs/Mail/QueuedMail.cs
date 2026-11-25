using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Trislab.Framework.Mailing;

namespace Trislab.Framework.Jobs.Mail;

/// <summary>
/// The stored form of a queued <see cref="MailRequest"/>: every field, serialized and then encrypted. The purpose
/// string is versioned; a change to this shape gets a new version, and the job keeps reading the old one until the
/// queue has drained.
/// </summary>
internal sealed record QueuedMail(
    IReadOnlyList<string> To,
    string Subject,
    string? Body,
    string? TextBody,
    string? From,
    string? DisplayName,
    string? ReplyTo,
    string? ReplyToName,
    IReadOnlyList<string> Bcc,
    IReadOnlyList<string> Cc,
    IReadOnlyDictionary<string, byte[]> AttachmentData,
    IReadOnlyDictionary<string, string> Headers)
{
    internal const string Purpose = "Trislab.Framework.Jobs.Mail.v1";

    public static string Protect(IDataProtectionProvider provider, MailRequest request)
    {
        var mail = new QueuedMail(
            [.. request.To],
            request.Subject,
            request.Body,
            request.TextBody,
            request.From,
            request.DisplayName,
            request.ReplyTo,
            request.ReplyToName,
            [.. request.Bcc],
            [.. request.Cc],
            new Dictionary<string, byte[]>(request.AttachmentData),
            new Dictionary<string, string>(request.Headers));

        return provider.CreateProtector(Purpose).Protect(JsonSerializer.Serialize(mail));
    }

    public static MailRequest Unprotect(IDataProtectionProvider provider, string protectedMail)
    {
        var mail = JsonSerializer.Deserialize<QueuedMail>(provider.CreateProtector(Purpose).Unprotect(protectedMail))
            ?? throw new InvalidOperationException("The queued e-mail is empty.");

        return new MailRequest(
            new Collection<string>([.. mail.To]),
            mail.Subject,
            mail.Body,
            mail.From,
            mail.DisplayName,
            mail.ReplyTo,
            mail.ReplyToName,
            new Collection<string>([.. mail.Bcc]),
            new Collection<string>([.. mail.Cc]),
            new Dictionary<string, byte[]>(mail.AttachmentData),
            new Dictionary<string, string>(mail.Headers),
            mail.TextBody);
    }
}
