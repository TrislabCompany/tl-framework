using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Impersonation.StartImpersonation;

// RequestId: an approved request of the caller (RequestImpersonationCommand); it starts once.
// DurationMinutes: requested token lifetime, capped server-side at
// StartImpersonationCommandValidator.MaxImpersonationMinutes (60); null → JwtOptions.AccessTokenMinutes.
public sealed record StartImpersonationCommand(
    Guid RequestId,
    int? DurationMinutes = null)
    : ICommand<ImpersonationResponse>;
