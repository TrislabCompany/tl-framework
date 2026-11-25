using System.Text.Json.Serialization;
using Trislab.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Tokens.TenantSwitch;

/// <summary>Switches the signed-in user to another of their tenants: issues tokens for that tenant.</summary>
/// <param name="Tenant">The tenant to switch to; the user must be an active member there.</param>
/// <param name="SetDefault">True to open this tenant by default at the next sign-in.</param>
/// <param name="App">From the <c>X-Trislab-App</c> header, never the body.</param>
public record SwitchTenantCommand(
    string Tenant,
    bool SetDefault = false,
    [property: JsonIgnore] string? App = null)
    : ICommand<SignInResponse>;
