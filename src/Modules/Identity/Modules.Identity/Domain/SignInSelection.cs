using Trislab.Framework.Core.Domain;

namespace Trislab.Modules.Identity.Domain;

/// <summary>
/// The server-side half of a sign-in's selection token: one row per issued token, deleted when the token is
/// used. Deleting is atomic, so a selection token works once. Global, because the sign-in has no tenant yet.
/// </summary>
public class SignInSelection : IGlobalEntity
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = default!;
    public DateTime ExpiresOnUtc { get; private set; }

    private SignInSelection() { } // EF Core

    public static SignInSelection Create(Guid id, string userId, DateTime expiresOnUtc) =>
        new() { Id = id, UserId = userId, ExpiresOnUtc = expiresOnUtc };
}
