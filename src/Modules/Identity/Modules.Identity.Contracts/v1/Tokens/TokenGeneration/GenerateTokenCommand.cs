using System.Text.Json.Serialization;
using Trislab.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Tokens.TokenGeneration;

/// <summary>
/// Sign in with e-mail and password. No tenant is needed: the server opens the tenant named by the
/// <c>tenant</c> header when the user is a member there, else the user's default tenant, else their only
/// tenant, and otherwise answers with the list to choose from.
/// </summary>
/// <param name="Email">The account's e-mail address.</param>
/// <param name="Password">The account's password.</param>
/// <param name="TwoFactorCode">The authenticator code, when the account has a second factor.</param>
/// <param name="Tenant">From the <c>tenant</c> header, never the body.</param>
/// <param name="App">From the <c>X-Trislab-App</c> header, never the body.</param>
public record GenerateTokenCommand(
    string Email,
    string Password,
    string? TwoFactorCode = null,
    [property: JsonIgnore] string? Tenant = null,
    [property: JsonIgnore] string? App = null)
    : ICommand<SignInResponse>;
