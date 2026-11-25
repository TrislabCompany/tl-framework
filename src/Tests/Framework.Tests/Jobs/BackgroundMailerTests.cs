using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Trislab.Framework.Jobs.Mail;
using Trislab.Framework.Jobs.Services;
using Trislab.Framework.Mailing;
using Trislab.Framework.Mailing.Services;

namespace Framework.Tests.Jobs;

/// <summary>
/// Queued e-mail carries invitation, reset and confirmation links. The job store keeps a job's arguments until the
/// job expires, so the message is stored encrypted, and the job sends exactly the message that was queued.
/// </summary>
public sealed class BackgroundMailerTests
{
    private const string Link = "https://app.example.com/accept-invitation?token=secret-token-value";
    private const string Address = "owner@example.com";

    private readonly IJobService _jobs = Substitute.For<IJobService>();
    private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();
    private string? _queue;
    private Expression<Func<SendQueuedMailJob, Task>>? _call;

    public BackgroundMailerTests()
    {
        _jobs.Enqueue(Arg.Any<string>(), Arg.Any<Expression<Func<SendQueuedMailJob, Task>>>())
            .Returns(ci =>
            {
                _queue = ci.ArgAt<string>(0);
                _call = ci.ArgAt<Expression<Func<SendQueuedMailJob, Task>>>(1);
                return "job-1";
            });
    }

    private static MailRequest Request() => new(
        new Collection<string> { Address },
        "Invitation",
        body: $"<a href=\"{Link}\">Accept</a>",
        from: "noreply@example.com",
        displayName: "Trislab",
        replyTo: "support@example.com",
        replyToName: "Support",
        bcc: new Collection<string> { "bcc@example.com" },
        cc: new Collection<string> { "cc@example.com" },
        attachmentData: new Dictionary<string, byte[]> { ["invoice.pdf"] = [1, 2, 3] },
        headers: new Dictionary<string, string> { ["X-Trace"] = "abc" },
        textBody: $"Accept: {Link}");

    // The one argument Hangfire stores with the job.
    private string StoredArgument() =>
        (string)Expression.Lambda(((MethodCallExpression)_call!.Body).Arguments[0]).Compile().DynamicInvoke()!;

    [Fact]
    public void The_Job_Store_Never_Sees_The_Link_Or_The_Address()
    {
        new ProtectedBackgroundMailer(_jobs, _protection).Enqueue(Request()).ShouldBe("job-1");

        _queue.ShouldBe("email");
        var stored = StoredArgument();
        stored.ShouldNotContain("secret-token-value");
        stored.ShouldNotContain(Address);
        stored.ShouldNotContain("Invitation");
    }

    [Fact]
    public async Task The_Job_Sends_The_Message_That_Was_Queued()
    {
        new ProtectedBackgroundMailer(_jobs, _protection).Enqueue(Request());
        var mail = Substitute.For<IMailService>();

        await _call!.Compile()(new SendQueuedMailJob(mail, _protection));

        var sent = (MailRequest)mail.ReceivedCalls().Single().GetArguments()[0]!;
        var expected = Request();
        sent.To.ShouldBe(expected.To);
        sent.Subject.ShouldBe(expected.Subject);
        sent.Body.ShouldBe(expected.Body);
        sent.TextBody.ShouldBe(expected.TextBody);
        sent.From.ShouldBe(expected.From);
        sent.DisplayName.ShouldBe(expected.DisplayName);
        sent.ReplyTo.ShouldBe(expected.ReplyTo);
        sent.ReplyToName.ShouldBe(expected.ReplyToName);
        sent.Bcc.ShouldBe(expected.Bcc);
        sent.Cc.ShouldBe(expected.Cc);
        sent.AttachmentData["invoice.pdf"].ShouldBe(expected.AttachmentData["invoice.pdf"]);
        sent.Headers.ShouldBe(expected.Headers);
    }

    [Fact]
    public async Task A_Payload_From_Another_Key_Ring_Is_Refused()
    {
        new ProtectedBackgroundMailer(_jobs, _protection).Enqueue(Request());
        var mail = Substitute.For<IMailService>();

        await Should.ThrowAsync<CryptographicException>(
            () => _call!.Compile()(new SendQueuedMailJob(mail, new EphemeralDataProtectionProvider())));
        mail.ReceivedCalls().ShouldBeEmpty();
    }
}
