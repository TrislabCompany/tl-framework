using FluentValidation;
using Trislab.Framework.Core.Localization;
using Trislab.Framework.Web.Validation;
using Trislab.Modules.Identity.Contracts.v1.Users.SearchUsers;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Identity.Features.v1.Users.SearchUsers;

public sealed class SearchUsersQueryValidator : AbstractValidator<SearchUsersQuery>
{
    public SearchUsersQueryValidator(IStringLocalizer<SharedResources> localizer)
    {
        Include(new PagedQueryValidator<SearchUsersQuery>(localizer));

        RuleFor(q => q.Search)
            .MaximumLength(200)
            .When(q => !string.IsNullOrEmpty(q.Search));

        RuleFor(q => q.RoleId)
            .MaximumLength(450)
            .When(q => !string.IsNullOrEmpty(q.RoleId));
    }
}