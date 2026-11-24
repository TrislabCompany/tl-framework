using System.Collections.ObjectModel;
using Trislab.Modules.Identity.Contracts.DTOs;
using Mediator;

namespace Trislab.Modules.Identity.Contracts.v1.Users.GetUserLookup;

/// <summary>
/// Finds active users for a picker (<see cref="Search"/>) or for showing names (<see cref="Ids"/>).
/// Give exactly one of the two. Needs only <c>Users.Pick</c>, a Basic permission, so it returns
/// no e-mail, phone or status: only what a picker shows.
/// </summary>
public sealed class GetUserLookupQuery : IQuery<ReadOnlyCollection<UserLookupDto>>
{
    /// <summary>Part of a first name, last name or user name, at least <see cref="MinSearchLength"/> characters.</summary>
    public string? Search { get; set; }

    /// <summary>The ids of the users to show, at most <see cref="MaxIds"/>.</summary>
    public string[]? Ids { get; set; }

    /// <summary>How many matches a search returns, 1 to <see cref="MaxResults"/>; 20 when not given.</summary>
    public int? Limit { get; set; }

    public const int MaxIds = 100;

    public const int MinSearchLength = 2;

    public const int MaxResults = 50;
}
