using Trislab.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Invitations.InviteUser;

/// <summary>
/// Invites an address to the current tenant: the only way an administrator adds a user. The answer is the
/// same whether the address has an account; the membership stays pending, and invisible to the tenant, until
/// the account's owner accepts the e-mailed link.
/// </summary>
/// <param name="Email">The address to invite.</param>
/// <param name="FirstName">Shown for a new account until its owner changes it; ignored for an existing account.</param>
/// <param name="LastName">As <paramref name="FirstName"/>.</param>
public sealed record InviteUserCommand(string Email, string? FirstName = null, string? LastName = null)
    : ICommand<InvitationDto>;
