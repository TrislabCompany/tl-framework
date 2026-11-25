using Microsoft.AspNetCore.DataProtection;
using Trislab.Framework.Mailing;
using Trislab.Framework.Mailing.Services;

namespace Trislab.Framework.Jobs.Mail;

/// <summary>
/// The background job behind <see cref="IBackgroundMailer"/>: it decrypts the queued message and sends it. Hangfire passes
/// its own cancellation token in place of the one in the stored call.
/// </summary>
public sealed class SendQueuedMailJob(IMailService mailService, IDataProtectionProvider protection)
{
    public Task RunAsync(string protectedMail, CancellationToken ct) =>
        mailService.SendAsync(QueuedMail.Unprotect(protection, protectedMail), ct);
}
