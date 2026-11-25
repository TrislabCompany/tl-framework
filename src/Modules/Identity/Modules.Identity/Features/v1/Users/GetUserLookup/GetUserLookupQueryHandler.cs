using System.Collections.ObjectModel;
using Trislab.Framework.Core.Context;
using Trislab.Modules.Identity.Contracts.DTOs;
using Trislab.Modules.Identity.Contracts.v1.Users.GetUserLookup;
using Trislab.Modules.Identity.Domain;
using Mediator;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Trislab.Modules.Identity.Features.v1.Users.GetUserLookup;

/// <summary>
/// Active users only, and only what a picker shows. A search matches names and user names, never
/// e-mail addresses, so a Basic user can't use it to find out who has an account under an address.
/// </summary>
public sealed class GetUserLookupQueryHandler(
    UserManager<TrislabUser> userManager,
    IRequestContext requestContext)
    : IQueryHandler<GetUserLookupQuery, ReadOnlyCollection<UserLookupDto>>
{
    private const int DefaultLimit = 20;

    public async ValueTask<ReadOnlyCollection<UserLookupDto>> Handle(GetUserLookupQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<TrislabUser> users = userManager.Users.AsNoTracking().Where(u => u.Memberships.Any(m => m.IsActive));

        if (query.Ids is { Length: > 0 })
        {
            var ids = query.Ids.Distinct(StringComparer.Ordinal).ToArray();
            users = users.Where(u => ids.Contains(u.Id));
        }
        else
        {
            string term = query.Search!.Trim().ToLowerInvariant();
            users = users
                .Where(u =>
                    (u.FirstName != null && u.FirstName.ToLower().Contains(term)) ||
                    (u.LastName != null && u.LastName.ToLower().Contains(term)) ||
                    (u.UserName != null && u.UserName.ToLower().Contains(term)))
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ThenBy(u => u.Id)
                .Take(Math.Clamp(query.Limit ?? DefaultLimit, 1, GetUserLookupQuery.MaxResults));
        }

        var rows = await users
            .Select(u => new { u.Id, u.UserName, u.FirstName, u.LastName, ImageUrl = u.ImageUrl != null ? u.ImageUrl.ToString() : null })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(u => new UserLookupDto(u.Id, u.UserName, u.FirstName, u.LastName, ResolveImageUrl(u.ImageUrl)))
            .ToList()
            .AsReadOnly();
    }

    // The same rule as SearchUsers: a stored relative path becomes an absolute URL on the request's origin.
    private string? ResolveImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
        {
            return imageUrl;
        }

        var origin = requestContext.Origin;
        return string.IsNullOrEmpty(origin) ? imageUrl : $"{origin}/{imageUrl.TrimStart('/')}";
    }
}
