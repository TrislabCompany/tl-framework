using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Impersonation.RequestImpersonation;

/// <summary>
/// An operator asks a user for read-only access as that user (D-44). The user approves or denies it; only an
/// approved request starts the impersonation (<c>StartImpersonationCommand</c>).
/// </summary>
public sealed record RequestImpersonationCommand(
    string TargetUserId,
    string TargetTenantId,
    string Reason)
    : ICommand<ImpersonationRequestDto>;
