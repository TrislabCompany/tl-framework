using System.Text.Json.Serialization;
using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Users.ConfirmEmail;

/// <summary>
/// Confirms an address with the code from the e-mail and the account's password. The code proves the mailbox, the
/// password proves the person who signed up: someone who registered another person's address can't get the
/// account confirmed by its owner clicking the link.
/// </summary>
public sealed record ConfirmEmailCommand(string UserId, string Code, string Password) : ICommand<string>
{
    /// <summary>The tenant from the request's <c>tenant</c> header, never from the body.</summary>
    [JsonIgnore]
    public string Tenant { get; init; } = string.Empty;
}
