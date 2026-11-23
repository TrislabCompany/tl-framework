using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Mailing.Services;
using Trislab.Modules.Multitenancy.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace Trislab.Modules.Multitenancy.IntegrationEventHandlers;

/// <summary>Emails the tenant admin that their subscription is nearing expiry.</summary>
public sealed class TenantNearingExpiryEmailHandler(
    IMailService mailService,
    ILogger<TenantNearingExpiryEmailHandler> logger)
    : IIntegrationEventHandler<TenantNearingExpiryIntegrationEvent>
{
    public async Task HandleAsync(TenantNearingExpiryIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        var (subject, body, textBody) = TenantExpiryEmailBodies.NearingExpiry(
            @event.TenantName, @event.PlanKey, @event.ValidUpto, @event.DaysRemaining);
        await TenantExpiryEmailSender.SendAsync(mailService, logger, @event.AdminEmail, subject, body, textBody, "nearing-expiry", ct)
            .ConfigureAwait(false);
    }
}
