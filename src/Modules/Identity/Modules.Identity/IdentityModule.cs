using Trislab.Framework.Shared.Multitenancy;
using Asp.Versioning;
using Trislab.Framework.Core.Context;
using Trislab.Framework.Eventing;
using Trislab.Framework.Persistence;
using Trislab.Framework.Quota;
using Trislab.Framework.Storage;
using Trislab.Framework.Storage.Local;
using Trislab.Framework.Storage.Services;
using Trislab.Framework.Web.Modules;
using Trislab.Modules.Identity.Authorization;
using Trislab.Modules.Identity.Authorization.Jwt;
using Trislab.Modules.Identity.Contracts.Services;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Features.v1.Groups.AddUsersToGroup;
using Trislab.Modules.Identity.Features.v1.Groups.CreateGroup;
using Trislab.Modules.Identity.Features.v1.Groups.DeleteGroup;
using Trislab.Modules.Identity.Features.v1.Groups.GetGroupById;
using Trislab.Modules.Identity.Features.v1.Groups.GetGroupMembers;
using Trislab.Modules.Identity.Features.v1.Groups.GetGroups;
using Trislab.Modules.Identity.Features.v1.Groups.RemoveUserFromGroup;
using Trislab.Modules.Identity.Features.v1.Groups.UpdateGroup;
using Trislab.Modules.Identity.Features.v1.Impersonation.EndImpersonation;
using Trislab.Modules.Identity.Features.v1.Impersonation.GetImpersonationGrants;
using Trislab.Modules.Identity.Features.v1.Impersonation.RevokeImpersonationGrant;
using Trislab.Modules.Identity.Features.v1.Impersonation.StartImpersonation;
using Trislab.Modules.Identity.Features.v1.Permissions.GetPermissionCatalog;
using Trislab.Modules.Identity.Features.v1.Roles;
using Trislab.Modules.Identity.Features.v1.Roles.DeleteRole;
using Trislab.Modules.Identity.Features.v1.Roles.GetRoleById;
using Trislab.Modules.Identity.Features.v1.Roles.GetRoles;
using Trislab.Modules.Identity.Features.v1.Roles.GetRoleWithPermissions;
using Trislab.Modules.Identity.Features.v1.Roles.UpdateRolePermissions;
using Trislab.Modules.Identity.Features.v1.Roles.UpsertRole;
using Trislab.Modules.Identity.Features.v1.Sessions.AdminRevokeAllSessions;
using Trislab.Modules.Identity.Features.v1.Sessions.AdminRevokeSession;
using Trislab.Modules.Identity.Features.v1.Sessions.GetMySessions;
using Trislab.Modules.Identity.Features.v1.Sessions.GetTenantSessions;
using Trislab.Modules.Identity.Features.v1.Sessions.GetUserSessions;
using Trislab.Modules.Identity.Features.v1.Sessions.RevokeAllSessions;
using Trislab.Modules.Identity.Features.v1.Sessions.RevokeSession;
using Trislab.Modules.Identity.Features.v1.Tokens.RefreshToken;
using Trislab.Modules.Identity.Features.v1.Tokens.TenantSelection;
using Trislab.Modules.Identity.Features.v1.Tokens.TenantSwitch;
using Trislab.Modules.Identity.Features.v1.Users.GetMyTenants;
using Trislab.Modules.Identity.Features.v1.Tokens.TokenGeneration;
using Trislab.Modules.Identity.Features.v1.TwoFactor.Disable;
using Trislab.Modules.Identity.Features.v1.TwoFactor.Enroll;
using Trislab.Modules.Identity.Features.v1.TwoFactor.VerifyEnroll;
using Trislab.Modules.Identity.Features.v1.Users.AssignUserRoles;
using Trislab.Modules.Identity.Features.v1.Users.ChangePassword;
using Trislab.Modules.Identity.Features.v1.Users.AdminConfirmEmail;
using Trislab.Modules.Identity.Features.v1.Users.ConfirmEmail;
using Trislab.Modules.Identity.Features.v1.Users.ResendConfirmationEmail;
using Trislab.Modules.Identity.Features.v1.Users.DeleteUser;
using Trislab.Modules.Identity.Features.v1.Users.ForgotPassword;
using Trislab.Modules.Identity.Features.v1.Users.GetUserById;
using Trislab.Modules.Identity.Features.v1.Users.GetUserGroups;
using Trislab.Modules.Identity.Features.v1.Users.GetUserPermissions;
using Trislab.Modules.Identity.Features.v1.Users.GetUserProfile;
using Trislab.Modules.Identity.Features.v1.Users.GetUserRoles;
using Trislab.Modules.Identity.Features.v1.Users.GetUsers;
using Trislab.Modules.Identity.Features.v1.Users.RegisterUser;
using Trislab.Modules.Identity.Features.v1.Users.ResetPassword;
using Trislab.Modules.Identity.Features.v1.Users.SearchUsers;
using Trislab.Modules.Identity.Features.v1.Users.GetUserLookup;
using Trislab.Modules.Identity.Features.v1.Users.SelfRegistration;
using Trislab.Modules.Identity.Features.v1.Users.SetProfileImage;
using Trislab.Modules.Identity.Features.v1.Users.ToggleUserStatus;
using Trislab.Modules.Identity.Features.v1.Users.UpdateUser;
using Trislab.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Trislab.Modules.Identity;

public class IdentityModule : IModule
{
    /// <summary>Works with and without a tenant catalog; data is stamped with the tenant in both modes.</summary>
    public TenancyModes SupportedTenancyModes => TenancyModes.All;

    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        Trislab.Framework.Shared.Constants.PermissionConstants.Register(
            Trislab.Modules.Identity.Contracts.Authorization.IdentityPermissions.All);

        var services = builder.Services;
        services.Configure<PermissionSyncOptions>(builder.Configuration.GetSection(PermissionSyncOptions.SectionName));
        services.AddScoped<RolePermissionSyncer>();
        services.AddHostedService<RolePermissionSyncHostedService>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, PathAwareAuthorizationHandler>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<ICurrentUserService>());
        services.AddScoped<ICurrentUserInitializer>(sp => sp.GetRequiredService<ICurrentUserService>());
        services.AddScoped<IRequestContextService, RequestContextService>();
        services.AddScoped<IRequestContext>(sp => sp.GetRequiredService<IRequestContextService>());
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IImpersonationGrantService, ImpersonationGrantService>();

        // User services - focused single-responsibility services
        services.AddTransient<IUserRegistrationService, UserRegistrationService>();
        services.AddTransient<IUserProfileService, UserProfileService>();
        services.AddTransient<IUserStatusService, UserStatusService>();
        services.AddTransient<IUserRoleService, UserRoleService>();
        services.AddTransient<IUserPasswordService, UserPasswordService>();
        services.AddTransient<IUserPermissionService, UserPermissionService>();

        // Facade for backward compatibility
        services.AddTransient<IUserService, UserService>();

        services.AddTransient<IRoleService, RoleService>();
        services.AddTrislabStorage(builder.Configuration);
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<TenantSignInService>();
        services.AddTrislabDbContext<IdentityDbContext>(IdentityModuleConstants.SchemaName);
        // Eventing itself is bootstrapped by the host (AddEventingCore) — the outbox is framework
        // infrastructure, not Identity's. Handler registration stays per module.
        services.AddIntegrationEventHandlers(typeof(IdentityModule).Assembly);
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<IdentityDbContext>(
                name: "db:identity",
                failureStatus: HealthStatus.Unhealthy);
        services.AddScoped<IDbInitializer, IdentityDbInitializer>();

        // Configure password policy options
        services.Configure<PasswordPolicyOptions>(builder.Configuration.GetSection("PasswordPolicy"));

        // Tenant subscription grace period (shared "Billing" section) — used by the login expiry check.
        services.Configure<TenantGraceOptions>(builder.Configuration.GetSection(TenantGraceOptions.SectionName));

        services.AddOptions<TwoFactorOptions>()
            .BindConfiguration(nameof(TwoFactorOptions))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register password history service
        services.AddScoped<IPasswordHistoryService, PasswordHistoryService>();

        // Register password expiry service
        services.AddScoped<IPasswordExpiryService, PasswordExpiryService>();

        // Register session service and background cleanup
        services.AddScoped<ISessionService, SessionService>();
        services.AddHostedService<SessionCleanupHostedService>();

        // Register group role service for group-derived permissions
        services.AddScoped<IGroupRoleService, GroupRoleService>();

        // Quota gauge: reports live user count per tenant for the Users quota.
        services.AddScoped<IQuotaGaugeProvider, UserCountQuotaGaugeProvider>();

        services.AddIdentity<TrislabUser, TrislabRole>(options =>
        {
            options.Password.RequiredLength = IdentityModuleConstants.PasswordLength;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.User.RequireUniqueEmail = true;

            // Account lockout: 5 consecutive failed logins → 15-minute lockout (applies to new users by default).
            // IdentityService's login flow drives AccessFailedAsync / IsLockedOutAsync.
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
           .AddEntityFrameworkStores<IdentityDbContext>()
           .AddUserStore<TrislabUserStore>()
           .AddDefaultTokenProviders();

        //metrics
        services.AddSingleton<IdentityMetrics>();

        services.ConfigureJwtAuth();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var apiVersionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = endpoints
            .MapGroup("api/v{version:apiVersion}/identity")
            .WithTags("Identity")
            .WithApiVersionSet(apiVersionSet);

        // tokens
        group.MapGenerateTokenEndpoint().AllowAnonymous().RequireRateLimiting("auth");
        group.MapRefreshTokenEndpoint().AllowAnonymous().RequireRateLimiting("auth");
        group.MapTenantSelectionEndpoint().RequireRateLimiting("auth");
        group.MapTenantSwitchEndpoint().RequireRateLimiting("auth");

        // The outbox is dispatched by the framework's OutboxDispatcherHostedService (on by default), which now claims
        // rows with FOR UPDATE SKIP LOCKED so several instances can drain safely. This module still registers no
        // dispatcher of its own: the outbox is framework infrastructure, not Identity's.

        // roles
        group.MapGetRolesEndpoint();
        group.MapGetRoleByIdEndpoint();
        group.MapDeleteRoleEndpoint();
        group.MapGetRolePermissionsEndpoint();
        group.MapUpdateRolePermissionsEndpoint();
        group.MapCreateOrUpdateRoleEndpoint();

        // permission catalog — every permission registered with the host,
        // filtered to the caller's tenant context (root vs admin set)
        group.MapGetPermissionCatalogEndpoint();

        // users
        group.MapAssignUserRolesEndpoint();
        group.MapChangePasswordEndpoint();
        group.MapAdminConfirmEmailEndpoint();
        group.MapResendConfirmationEmailEndpoint().RequireRateLimiting("auth");
        group.MapConfirmEmailEndpoint().RequireRateLimiting("auth");
        group.MapDeleteUserEndpoint();
        group.MapGetUserByIdEndpoint();
        group.MapGetCurrentUserPermissionsEndpoint();
        group.MapGetMeEndpoint();
        group.MapGetMyTenantsEndpoint();
        group.MapGetUserRolesEndpoint();
        group.MapGetUsersListEndpoint();
        group.MapSearchUsersEndpoint();
        group.MapGetUserLookupEndpoint();
        group.MapRegisterUserEndpoint();
        group.MapForgotPasswordEndpoint().RequireRateLimiting("auth");
        group.MapResetPasswordEndpoint().RequireRateLimiting("auth");
        group.MapSelfRegisterUserEndpoint().RequireRateLimiting("auth");
        group.MapToggleUserStatusEndpoint();
        group.MapUpdateUserEndpoint();
        group.MapSetProfileImageEndpoint();

        // sessions - user endpoints
        group.MapGetMySessionsEndpoint();
        group.MapRevokeSessionEndpoint();
        group.MapRevokeAllSessionsEndpoint();

        // sessions - admin endpoints
        group.MapGetTenantSessionsEndpoint();
        group.MapGetUserSessionsEndpoint();
        group.MapAdminRevokeSessionEndpoint();
        group.MapAdminRevokeAllSessionsEndpoint();

        // groups
        group.MapGetGroupsEndpoint();
        group.MapGetGroupByIdEndpoint();
        group.MapCreateGroupEndpoint();
        group.MapUpdateGroupEndpoint();
        group.MapDeleteGroupEndpoint();
        group.MapGetGroupMembersEndpoint();
        group.MapAddUsersToGroupEndpoint();
        group.MapRemoveUserFromGroupEndpoint();

        // user groups
        group.MapGetUserGroupsEndpoint();

        // impersonation
        group.MapStartImpersonationEndpoint();
        group.MapEndImpersonationEndpoint();
        group.MapGetImpersonationGrantsEndpoint();
        group.MapRevokeImpersonationGrantEndpoint();

        // two-factor authentication (TOTP)
        group.MapEnrollTwoFactorEndpoint();
        group.MapVerifyEnrollTwoFactorEndpoint();
        group.MapDisableTwoFactorEndpoint();
    }
}