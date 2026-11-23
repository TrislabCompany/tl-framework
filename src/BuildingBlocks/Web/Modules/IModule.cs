using Trislab.Framework.Shared.Multitenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Trislab.Framework.Web.Modules;

public interface IModule
{
    /// <summary>
    /// The tenancy modes this module supports. Every module declares them in code, and start-up refuses a
    /// module registered in a mode it does not support (<see cref="ModuleLoader.AddModules"/>).
    /// <see cref="TenancyModes.All"/> when the module works with and without a tenant catalog;
    /// <see cref="TenancyModes.Multitenant"/> when it needs one (tenants, plans, subscriptions).
    /// </summary>
    TenancyModes SupportedTenancyModes { get; }

    // DI/Options/Health/etc. — don’t depend on ASP.NET types here
    void ConfigureServices(IHostApplicationBuilder builder);

    // HTTP wiring — Minimal APIs only
    void MapEndpoints(IEndpointRouteBuilder endpoints);

    // Middleware wiring — called during pipeline configuration.
    // Default implementation is a no-op so existing modules are not forced to implement this.
    void ConfigureMiddleware(IApplicationBuilder app) { }
}