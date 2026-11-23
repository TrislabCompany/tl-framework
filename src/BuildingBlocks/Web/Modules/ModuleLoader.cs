using FluentValidation;
using Trislab.Framework.Shared.Multitenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Trislab.Framework.Web.Modules;

public static class ModuleLoader
{
    private static readonly List<IModule> _modules = new();
    private static readonly object _lock = new();
    private static bool _modulesLoaded;

    public static IHostApplicationBuilder AddModules(this IHostApplicationBuilder builder, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(builder);

        lock (_lock)
        {
            if (_modulesLoaded)
            {
                return builder;
            }

            builder.Services.AddValidatorsFromAssemblies(assemblies);

            var source = assemblies is { Length: > 0 }
                ? assemblies
                : AppDomain.CurrentDomain.GetAssemblies();

            var mode = TenancyOptions.ReadMode(builder.Configuration);

            foreach (var moduleType in DiscoverModuleTypes(source))
            {
                if (Activator.CreateInstance(moduleType) is not IModule module)
                {
                    throw new InvalidOperationException($"Unable to create module {moduleType.Name}.");
                }

                EnsureSupports(module, moduleType, mode);

                module.ConfigureServices(builder);
                _modules.Add(module);
            }

            _modulesLoaded = true;
        }

        return builder;
    }

    /// <summary>
    /// Returns the assemblies whose modules all support <paramref name="mode"/>, in the given order. A host that
    /// serves both modes lists every module it knows and passes the list through here, so a multi-tenant-only
    /// module (Multitenancy, Billing) drops out of a single-tenant run. A host with a fixed module list does not
    /// need it: <see cref="AddModules"/> refuses an unsupported module either way.
    /// </summary>
    public static Assembly[] SelectSupported(TenancyMode mode, IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return assemblies
            .Where(a => DiscoverModuleTypes([a])
                .All(t => (Activator.CreateInstance(t) as IModule)?.SupportedTenancyModes.Supports(mode) ?? false))
            .ToArray();
    }

    /// <summary>Throws when <paramref name="module"/> does not support <paramref name="mode"/>.</summary>
    public static void EnsureSupports(IModule module, Type moduleType, TenancyMode mode)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(moduleType);

        var supported = module.SupportedTenancyModes;
        if (supported == TenancyModes.None)
        {
            throw new InvalidOperationException(
                $"Module {moduleType.Name} declares no tenancy mode. Return {nameof(TenancyModes)}.{nameof(TenancyModes.All)}, " +
                $"{nameof(TenancyModes)}.{nameof(TenancyModes.Multitenant)} or {nameof(TenancyModes)}.{nameof(TenancyModes.SingleTenant)} " +
                "from SupportedTenancyModes.");
        }

        if (!supported.Supports(mode))
        {
            throw new InvalidOperationException(
                $"Module {moduleType.Name} supports {supported} mode only, but the host runs in {mode} mode " +
                $"({TenancyOptions.SectionName}:Mode). Remove the module from the host's module list, or run the host in a mode the module supports.");
        }
    }

    private static IEnumerable<Type> DiscoverModuleTypes(IEnumerable<Assembly> source) =>
        source
            .SelectMany(a => a.GetCustomAttributes<TrislabModuleAttribute>())
            .Where(r => typeof(IModule).IsAssignableFrom(r.ModuleType))
            .DistinctBy(r => r.ModuleType)
            .OrderBy(r => r.Order)
            .ThenBy(r => r.ModuleType.Name)
            .Select(r => r.ModuleType);

    public static IApplicationBuilder UseModuleMiddlewares(this IApplicationBuilder app)
    {
        foreach (var m in _modules)
            m.ConfigureMiddleware(app);

        return app;
    }

    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder endpoints)
    {
        foreach (var m in _modules)
            m.MapEndpoints(endpoints);

        return endpoints;
    }
}