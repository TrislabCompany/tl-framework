using Trislab.Framework.Core.Exceptions;
using Trislab.Framework.Jobs.Mail;
using Trislab.Framework.Jobs.Services;
using Trislab.Framework.Shared.Persistence;
using Hangfire;
using Hangfire.Common;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
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

        // Hangfire's global configuration (JobStorage.Current, JobActivator.Current, GlobalJobFilters) is
        // process-wide. Everything this host uses is its own instead: the storage, the activator and the filters
        // are DI singletons, which the server, IBackgroundJobClient, IRecurringJobManager and the dashboard
        // resolve from this host's container. Nothing reads the statics (JobsDependencyInjectionTests).
        services.AddHangfire(_ => { });
        services.AddSingleton<JobStorage>(provider => CreateStorage(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton<JobActivator>(provider => new TrislabJobActivator(provider.GetRequiredService<IServiceScopeFactory>()));
        services.AddSingleton<IJobFilterProvider>(provider =>
        {
            var hostFilters = new JobFilterCollection
            {
                new TrislabJobFilter(provider),
                new LogJobFilter(),
                new HangfireTelemetryFilter(),
            };

            // Hangfire's own providers keep the built-in filters (retries, continuations, statistics) and the
            // attributes on job methods; this host's filters come on top.
            return new JobFilterProviderCollection(JobFilterProviders.Providers, hostFilters);
        });

        // Deferred stale lock cleanup — runs after app starts accepting requests
        services.AddHostedService<HangfireStaleLockCleanupService>();

        services.AddTransient<IJobService, HangfireService>();

        // Queued e-mail is encrypted before it reaches the job store (IBackgroundMailer). The key ring is the host's:
        // Redis when caching is configured (shared by every replica), otherwise the default local one.
        services.AddDataProtection();
        services.AddTransient<IBackgroundMailer, ProtectedBackgroundMailer>();
        services.AddTransient<SendQueuedMailJob>();

        return services;
    }

    private static PostgreSqlStorage CreatePostgreSqlStorage(string connectionString)
    {
        var options = new PostgreSqlStorageOptions();
        return new PostgreSqlStorage(new NpgsqlConnectionFactory(connectionString, options), options);
    }

    private static JobStorage CreateStorage(IConfiguration configuration)
    {
        var dbOptions = configuration.GetSection(nameof(DatabaseOptions)).Get<DatabaseOptions>()
            ?? throw new CustomException("Database options not found").Localized("Jobs.DatabaseOptionsNotFound");

        return dbOptions.Provider.ToUpperInvariant() switch
        {
            DbProviders.PostgreSQL => CreatePostgreSqlStorage(dbOptions.ConnectionString),
            DbProviders.MSSQL => new SqlServerStorage(dbOptions.ConnectionString),
            _ => throw new CustomException($"Hangfire storage provider {dbOptions.Provider} is not supported")
                .Localized("Jobs.UnsupportedStorageProvider", dbOptions.Provider),
        };
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