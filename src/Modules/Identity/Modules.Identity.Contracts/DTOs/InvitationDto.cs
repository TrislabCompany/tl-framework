namespace Trislab.Modules.Identity.Contracts.DTOs;

/// <summary>
/// An invitation as the tenant's administrators see it: the address they typed and its state, nothing about
/// the account behind it.
/// </summary>
/// <param name="Id">The invitation id, for revoking it.</param>
/// <param name="Email">The address the invitation went to.</param>
/// <param name="Status">Pending, Accepted, Revoked or Expired.</param>
/// <param name="CreatedOnUtc">When it was sent.</param>
/// <param name="ExpiresOnUtc">When the link stops working.</param>
/// <param name="AcceptedOnUtc">When the owner accepted.</param>
/// <param name="RevokedOnUtc">When it was revoked.</param>
public sealed record InvitationDto(
    Guid Id,
    string Email,
    string Status,
    DateTime CreatedOnUtc,
    DateTime ExpiresOnUtc,
    DateTime? AcceptedOnUtc,
    DateTime? RevokedOnUtc);
