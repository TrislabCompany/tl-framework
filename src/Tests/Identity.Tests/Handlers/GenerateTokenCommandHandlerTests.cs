using Trislab.Framework.Core.Context;
using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.Auditing.Contracts;
using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Contracts.v1.Tokens.TokenGeneration;
using Trislab.Modules.Identity.Features.v1.Tokens.TokenGeneration;
using Trislab.Modules.Identity.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Identity.Tests.Handlers;

/// <summary>
/// Tests for GenerateTokenCommandHandler: it checks the global account's credentials and audits a failure.
/// Choosing the tenant and issuing tokens is TenantSignInService's job, covered by the integration tests
/// (GlobalUserSignInTests, TokenGenerationTests, AppBoundaryTests).
/// </summary>
public sealed class GenerateTokenCommandHandlerTests
{
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly ISecurityAudit _securityAudit = Substitute.For<ISecurityAudit>();
    private readonly IRequestContext _requestContext = Substitute.For<IRequestContext>();
    private readonly GenerateTokenCommandHandler _sut;

    public GenerateTokenCommandHandlerTests()
    {
        _requestContext.IpAddress.Returns("203.0.113.5");
        _requestContext.ClientId.Returns("web");

        // The sign-in service is never reached when the credentials fail.
        var signIn = new TenantSignInService(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        _sut = new GenerateTokenCommandHandler(_identityService, signIn, _securityAudit, _requestContext);
    }

    [Fact]
    public async Task Handle_Should_AuditAndRethrow_When_CredentialsAreInvalid()
    {
        var command = new GenerateTokenCommand("user@example.com", "wrong");
        _identityService.ValidateCredentialsAsync(command.Email, command.Password, null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedException());

        await Should.ThrowAsync<UnauthorizedException>(async () => await _sut.Handle(command, CancellationToken.None));

        await _securityAudit.Received(1).LoginFailedAsync(
            "user@example.com", "web", "InvalidCredentials", "203.0.113.5", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ForwardTheCancellationToken_ToTheCredentialCheck()
    {
        using var cts = new CancellationTokenSource();
        var command = new GenerateTokenCommand("user@example.com", "wrong", "123456");
        _identityService.ValidateCredentialsAsync(command.Email, command.Password, "123456", cts.Token)
            .ThrowsAsync(new UnauthorizedException());

        await Should.ThrowAsync<UnauthorizedException>(async () => await _sut.Handle(command, cts.Token));

        await _identityService.Received(1).ValidateCredentialsAsync(command.Email, command.Password, "123456", cts.Token);
    }

    [Fact]
    public async Task Handle_Should_ThrowArgumentNullException_When_CommandIsNull()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () => await _sut.Handle(null!, CancellationToken.None));
    }
}
