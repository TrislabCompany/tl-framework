using Microsoft.AspNetCore.Builder;

namespace Trislab.Framework.Web.Security;

public static class SecurityExtensions
{
    public static IApplicationBuilder UseTrislabSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}