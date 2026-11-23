using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Mailing.Services;
using Trislab.Modules.Multitenancy.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace Trislab.Modules.Multitenancy.IntegrationEventHandlers;

/// <summary>Emails the tenant admin that their subscription expired and access is suspended.</summary>
public sealed class TenantExpiredEmailHandler(
    IMailService mailService,
    ILogger<TenantExpiredEmailHandler> logger)
    : IIntegrationEventHandler<TenantExpiredIntegrationEvent>
{
    public async Task HandleAsync(TenantExpiredIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        var (subject, body, textBody) = TenantExpiryEmailBodies.Expired(@event.TenantName, @event.PlanKey, @event.ValidUpto);
        await TenantExpiryEmailSender.SendAsync(mailService, logger, @event.AdminEmail, subject, body, textBody, "expired", ct)
            .ConfigureAwait(false);
    }
}
