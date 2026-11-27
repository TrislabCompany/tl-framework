using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Impersonation.AnswerImpersonationRequest;

/// <summary>The requested user approves an operator's pending request.</summary>
public sealed record ApproveImpersonationRequestCommand(Guid RequestId) : ICommand<ImpersonationRequestDto>;
