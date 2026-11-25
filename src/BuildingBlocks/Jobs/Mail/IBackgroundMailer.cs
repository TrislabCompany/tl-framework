using Trislab.Framework.Mailing;

namespace Trislab.Framework.Jobs.Mail;

/// <summary>
/// Sends an e-mail in the background, on the "email" job queue. The message is encrypted with ASP.NET Data
/// Protection before it is stored, so the links and tokens it carries (invitations, password resets, e-mail
/// confirmations) are never readable in the job tables or the Hangfire dashboard. Queue mail through this, never
/// through <c>IJobService.Enqueue(() => mailService.SendAsync(request, …))</c>, which stores the message in clear.
/// </summary>
public interface IBackgroundMailer
{
    /// <summary>Queues <paramref name="request"/> and returns the job id.</summary>
    string Enqueue(MailRequest request);
}
