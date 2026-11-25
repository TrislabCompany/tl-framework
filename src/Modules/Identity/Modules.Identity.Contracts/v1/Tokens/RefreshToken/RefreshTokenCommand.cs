using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Tokens.RefreshToken;

// Token is the (possibly expired) access token, optional. When present, the handler cross-checks its
// subject against the refresh token's as a safeguard; when absent, refresh relies on refresh-token validation alone.
// App is the X-Trislab-App header, never the body: the app boundary is checked on every refresh.
public record RefreshTokenCommand(string? Token, string RefreshToken, [property: System.Text.Json.Serialization.JsonIgnore] string? App = null)
    : ICommand<RefreshTokenCommandResponse>;