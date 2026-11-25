using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.Auditing.Contracts;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Contracts.v1.Tokens.TokenGeneration;
using Trislab.Modules.Identity.Services;
using Mediator;

namespace Trislab.Modules.Identity.Features.v1.Tokens.TokenGeneration;

public sealed class GenerateTokenCommandHandler
    : ICommandHandler<GenerateTokenCommand, SignInResponse>
{
    private readonly IIdentityService _identityService;
    private readonly TenantSignInService _signIn;
    private readonly ISecurityAudit _securityAudit;
    private readonly IRequestContext _requestContext;
    private readonly AuditSubjectHasher _subjectHasher;

    public GenerateTokenCommandHandler(
        IIdentityService identityService,
        TenantSignInService signIn,
        ISecurityAudit securityAudit,
        IRequestContext requestContext,
        AuditSubjectHasher subjectHasher)
    {
        _subjectHasher = subjectHasher;
        _identityService = identityService;
        _signIn = signIn;
        _securityAudit = securityAudit;
        _requestContext = requestContext;
    }

    public async ValueTask<SignInResponse> Handle(
        GenerateTokenCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var caller = new SignInCaller(
            _requestContext.IpAddress ?? "unknown",
            _requestContext.UserAgent ?? "unknown",
            _requestContext.ClientId,
            request.Email);

        string userId;
        try
        {
            // Checks the global account: password, lockout, confirmed e-mail and the second factor.
            userId = await _identityService
                .ValidateCredentialsAsync(request.Email, request.Password, request.TwoFactorCode, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (UnauthorizedException)
        {
            await _securityAudit.LoginFailedAsync(
                // Never the address itself in the audit trail: a keyed hash still links repeated attempts.
                subjectIdOrName: _subjectHasher.Email(request.Email),
                clientId: caller.ClientId,
                reason: "InvalidCredentials",
                ip: caller.IpAddress,
                ct: cancellationToken).ConfigureAwait(false);
            throw;
        }

        return await _signIn
            .SignInAsync(userId, request.Tenant, request.App, caller, cancellationToken)
            .ConfigureAwait(false);
    }
}
