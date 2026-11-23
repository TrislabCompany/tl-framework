using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Mailing.Services;
using Trislab.Modules.Multitenancy.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace Trislab.Modules.Multitenancy.IntegrationEventHandlers;

/// <summary>Emails the tenant admin that their subscription lapsed and the grace period is counting down.</summary>
public sealed class TenantEnteredGraceEmailHandler(
    IMailService mailService,
    ILogger<TenantEnteredGraceEmailHandler> logger)
    : IIntegrationEventHandler<TenantEnteredGraceIntegrationEvent>
{
    public async Task HandleAsync(TenantEnteredGraceIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        var (subject, body, textBody) = TenantExpiryEmailBodies.EnteredGrace(
            @event.TenantName, @event.PlanKey, @event.ValidUpto, @event.GraceEndsUtc);
        await TenantExpiryEmailSender.SendAsync(mailService, logger, @event.AdminEmail, subject, body, textBody, "entered-grace", ct)
            .ConfigureAwait(false);
    }
}
