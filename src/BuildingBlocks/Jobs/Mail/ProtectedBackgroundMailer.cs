using Microsoft.AspNetCore.DataProtection;
using Trislab.Framework.Jobs.Services;
using Trislab.Framework.Mailing;

namespace Trislab.Framework.Jobs.Mail;

internal sealed class ProtectedBackgroundMailer(IJobService jobs, IDataProtectionProvider protection) : IBackgroundMailer
{
    internal const string Queue = "email";

    public string Enqueue(MailRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = QueuedMail.Protect(protection, request);
        return jobs.Enqueue<SendQueuedMailJob>(Queue, job => job.RunAsync(payload, CancellationToken.None));
    }
}
