using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Impersonation.AnswerImpersonationRequest;

/// <summary>
/// The requested user denies a request, takes an approval back, or ends the impersonation it started.
/// </summary>
public sealed record DenyImpersonationRequestCommand(Guid RequestId) : ICommand<ImpersonationRequestDto>;
