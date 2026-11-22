using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Jobs.Services;
using Trislab.Framework.Shared.Persistence;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Trislab.Framework.Jobs;

public static class Extensions
{
    public static IServiceCollection AddTrislabJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<HangfireOptions>()
            .BindConfiguration(nameof(HangfireOptions))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHangfireServer(options =>
        {
            options.HeartbeatInterval = TimeSpan.FromSeconds(30);
            options.Queues = ["default", "email"];
            options.WorkerCount = 5;
            options.SchedulePollingInterval = TimeSpan.FromSeconds(30);
        });

        services.AddHangfire((provider, config) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var dbOptions = configuration.GetSection(nameof(DatabaseOptions)).Get<DatabaseOptions>()
                ?? throw new CustomException("Database options not found").Localized("Jobs.DatabaseOptionsNotFound");

            switch (dbOptions.Provider.ToUpperInvariant())
            {
                case DbProviders.PostgreSQL:
                    config.UsePostgreSqlStorage(o =>
                    {
                        o.UseNpgsqlConnection(dbOptions.ConnectionString);
                    });
                    break;

                case DbProviders.MSSQL:
                    config.UseSqlServerStorage(dbOptions.ConnectionString);
                    break;

                default:
                    throw new CustomException($"Hangfire storage provider {dbOptions.Provider} is not supported")
                        .Localized("Jobs.UnsupportedStorageProvider", dbOptions.Provider);
            }

            config.UseActivator(new TrislabJobActivator(provider.GetRequiredService<IServiceScopeFactory>()));
            config.UseFilter(new TrislabJobFilter(provider));
            config.UseFilter(new LogJobFilter());
            config.UseFilter(new HangfireTelemetryFilter());
        });

        // Deferred stale lock cleanup — runs after app starts accepting requests
        services.AddHostedService<HangfireStaleLockCleanupService>();

        services.AddTransient<IJobService, HangfireService>();

        return services;
    }


    public static IApplicationBuilder UseTrislabJobDashboard(this IApplicationBuilder app, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(config);

        var hangfireOptions = config.GetSection(nameof(HangfireOptions)).Get<HangfireOptions>() ?? new HangfireOptions();
        if (string.IsNullOrWhiteSpace(hangfireOptions.Password))
        {
            return app;
        }

        var dashboardOptions = new DashboardOptions();
        dashboardOptions.AppPath = "/";
        dashboardOptions.Authorization = new[]
        {
           new HangfireCustomBasicAuthenticationFilter
           {
                User = hangfireOptions.UserName,
                Pass = hangfireOptions.Password
           }
        };

        return app.UseHangfireDashboard(hangfireOptions.Route, dashboardOptions);
    }
}