using Trislab.Framework.Tenancy;
using Microsoft.AspNetCore.Builder;

namespace Trislab.Modules.Multitenancy;

public static class Extensions
{
    /// <summary>Resolves the tenant of each request. Superseded by <c>UseTrislabTenancy</c> of the Tenancy building block.</summary>
#pragma warning disable S1133 // Marked obsolete first and removed one MINOR later (docs/packages.md section 2).
    [Obsolete("Use app.UseTrislabTenancy() from Trislab.Framework.Tenancy; this forwarder is removed one MINOR release later.")]
#pragma warning restore S1133
    public static WebApplication UseTrislabMultiTenantDatabases(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseTrislabTenancy();

        return app;
    }
}
