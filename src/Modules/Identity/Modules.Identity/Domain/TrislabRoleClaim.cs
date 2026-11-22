using Microsoft.AspNetCore.Identity;

namespace Trislab.Modules.Identity.Domain;

public class TrislabRoleClaim : IdentityRoleClaim<string>
{
    public string? CreatedBy { get; init; }
    public DateTimeOffset CreatedOn { get; init; }
}