using System.Text.Json.Serialization;
using Trislab.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Tokens.TenantSelection;

/// <summary>Finishes a sign-in that answered with a tenant list: issues the tokens for the chosen tenant.</summary>
/// <param name="SelectionToken">The <see cref="SignInResponse.SelectionToken"/> of the sign-in.</param>
/// <param name="Tenant">The chosen tenant's id.</param>
/// <param name="SetDefault">True to open this tenant by default at the next sign-in.</param>
/// <param name="App">From the <c>X-Trislab-App</c> header, never the body.</param>
public record SelectTenantCommand(
    string SelectionToken,
    string Tenant,
    bool SetDefault = false,
    [property: JsonIgnore] string? App = null)
    : ICommand<SignInResponse>;
