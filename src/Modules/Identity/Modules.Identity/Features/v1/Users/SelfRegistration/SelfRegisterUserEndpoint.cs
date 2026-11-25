using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Web.Frontend;
using Trislab.Framework.Web.Idempotency;
using Trislab.Modules.Identity.Contracts.v1.Users.RegisterUser;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Trislab.Modules.Identity.Features.v1.Users.SelfRegistration;

public static class SelfRegisterUserEndpoint
{
    internal static RouteHandlerBuilder MapSelfRegisterUserEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/self-register", async (RegisterUserCommand command,
            [FromHeader(Name = MultitenancyConstants.Identifier)] string? tenant,
            IFrontendOriginResolver originResolver,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            // Self-service flow: the confirmation link lands on the SPA the user registered from.
            command.Origin = originResolver.ResolveForCurrentRequest();
            // 202 with the same body whether or not the address had an account: never an account id.
            return TypedResults.Accepted((string?)null, await mediator.Send(command, cancellationToken));
        })
        .WithName("SelfRegisterUser")
        .WithSummary("Self register user")
        .WithDescription("Self-registration. Anonymous; in multi-tenant mode the tenant header names the tenant (not root), in single-tenant mode it is the fixed tenant. The answer (202, check_your_email) and its timing are the same whether the address has an account: a new address gets a confirmation e-mail, an existing account's owner an e-mail saying so. Rate limited.")
        // Deliberately NOT .WithIdempotency(): the entry key scopes by caller, and every unauthenticated
        // caller is the same "anon" caller, so two people registering on one tenant with the same
        // low-entropy key would collide — the second would replay the first's 201 and never get an
        // account. Retries are already safe here, the unique-email constraint rejects the duplicate.
        .AllowAnonymous()
        .RequireTenantHeaderWhenMultitenant()
        .Produces<RegisterUserResponse>(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status400BadRequest);
    }
}
