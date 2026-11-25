using Trislab.Framework.Core.Common;
using Trislab.Framework.Core.Context;
using Trislab.Framework.Shared.Multitenancy;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Trislab.Framework.Jobs;

public class TrislabJobActivator : JobActivator
{
    private readonly IServiceScopeFactory _scopeFactory;

    public TrislabJobActivator(IServiceScopeFactory scopeFactory) =>
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

    public override JobActivatorScope BeginScope(PerformContext context) =>
        new Scope(context, _scopeFactory.CreateScope());

    private sealed class Scope : JobActivatorScope, IServiceProvider
    {
        private readonly PerformContext _context;
        private readonly IServiceScope _scope;
        private IDisposable? _tenantUse;

        public Scope(PerformContext context, IServiceScope scope)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            ReceiveParameters();
        }

        private void ReceiveParameters()
        {
            var tenantInfo = _context.GetJobParameter<AppTenantInfo>(MultitenancyConstants.Identifier);

            // A job created outside a request (a recurring job, one enqueued by a hosted service) carries no
            // tenant. A single-tenant host has exactly one, so the job runs in it; a multi-tenant host has no
            // provider, and such a job runs without a tenant context, as before.
            tenantInfo ??= _scope.ServiceProvider.GetService<ISingleTenantProvider>()?.Tenant;

            if (tenantInfo is not null)
            {
                // Kept until the job's scope is disposed, so every service the job resolves sees the tenant, and
                // the worker thread doesn't carry it into the next job.
                _tenantUse = _scope.ServiceProvider.GetRequiredService<ITenantAccessor>().Use(tenantInfo);
            }

            string userId = _context.GetJobParameter<string>(QueryStringKeys.UserId);
            if (!string.IsNullOrEmpty(userId))
            {
                _scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>()
                    .SetCurrentUserId(userId);
            }
        }

        public override void DisposeScope()
        {
            _tenantUse?.Dispose();
            _scope.Dispose();
        }

        public override object Resolve(Type type) =>
            ActivatorUtilities.GetServiceOrCreateInstance(this, type);

        object? IServiceProvider.GetService(Type serviceType) =>
            serviceType == typeof(PerformContext)
                ? _context
                : _scope.ServiceProvider.GetService(serviceType);
    }
}