using System.ComponentModel.DataAnnotations;

namespace Trislab.Framework.Shared.Persistence;

/// <summary>
/// Configuration options for database provider selection and connection information.
/// </summary>
public sealed class DatabaseOptions : IValidatableObject
{
    /// <summary>
    /// The database provider to use. Valid values are <see cref="DbProviders.PostgreSQL"/> or <see cref="DbProviders.MSSQL"/>.
    /// Defaults to PostgreSQL.
    /// </summary>
    public string Provider { get; set; } = DbProviders.PostgreSQL;

    /// <summary>
    /// The connection string used by EF Core DbContexts and related services.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(ConnectionString))
        {
            yield return new ValidationResult("connection string cannot be empty.", new[] { nameof(ConnectionString) });
        }
    }
}