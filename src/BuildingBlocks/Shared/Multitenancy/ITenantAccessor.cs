namespace Trislab.Framework.Shared.Multitenancy;

/// <summary>
/// The tenant of the current asynchronous flow: the request's resolved tenant, or the tenant a background job,
/// event dispatch or seeder set with <see cref="Use"/>. Every module reads and sets the tenant through this
/// interface, never through Finbuckle's types, so the tenancy library stays behind the Tenancy building block.
/// </summary>
public interface ITenantAccessor
{
    /// <summary>
    /// The current tenant, or <see langword="null"/> when none is set: outside a request, or in a request whose
    /// tenant didn't resolve. Never throws.
    /// </summary>
    AppTenantInfo? TenantInfo { get; }

    /// <summary>Whether a tenant is set for the current flow.</summary>
    bool IsResolved { get; }

    /// <summary>
    /// Makes <paramref name="tenant"/> the current tenant of this asynchronous flow until the result is disposed,
    /// then restores the tenant that was current before, or none. <see langword="null"/> clears the tenant for the
    /// duration. Set the tenant before resolving a <c>DbContext</c>: a context captures the tenant when it is
    /// created.
    /// </summary>
    IDisposable Use(AppTenantInfo? tenant);
}
