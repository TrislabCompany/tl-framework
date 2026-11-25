using System.Text.Json.Serialization;
using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Invitations.AcceptInvitation;

/// <summary>
/// Accepts an invitation. An account its owner has not proven yet (no password, or an unconfirmed e-mail)
/// chooses its <see cref="Password"/> here; a confirmed account with a password must be signed in, and its
/// password is never changed here.
/// </summary>
/// <param name="Token">The token from the e-mailed link.</param>
/// <param name="Password">The password to set, when the account needs one; ignored otherwise.</param>
/// <param name="FirstName">Optional, when the account needs a password.</param>
/// <param name="LastName">Optional, when the account needs a password.</param>
/// <param name="CallerUserId">From the access token, never the body.</param>
public sealed record AcceptInvitationCommand(
    string Token,
    string? Password = null,
    string? FirstName = null,
    string? LastName = null,
    [property: JsonIgnore] string? CallerUserId = null)
    : ICommand<AcceptInvitationResponse>;

/// <summary>The tenant the accepted invitation opened; sign in, or switch to it.</summary>
public sealed record AcceptInvitationResponse(string Tenant, string? TenantName);
