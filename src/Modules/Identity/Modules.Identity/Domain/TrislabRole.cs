using Microsoft.AspNetCore.Identity;

namespace Trislab.Modules.Identity.Domain;

public class TrislabRole : IdentityRole
{
    public string? Description { get; set; }

    public TrislabRole(string name, string? description = null)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(name);

        Description = description;
        NormalizedName = name.ToUpperInvariant();
    }
}