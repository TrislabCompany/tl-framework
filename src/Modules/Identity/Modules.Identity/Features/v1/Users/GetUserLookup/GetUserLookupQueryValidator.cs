using FluentValidation;
using Trislab.Modules.Identity.Contracts.v1.Users.GetUserLookup;
using Trislab.Modules.Identity.Localization;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Identity.Features.v1.Users.GetUserLookup;

public sealed class GetUserLookupQueryValidator : AbstractValidator<GetUserLookupQuery>
{
    public GetUserLookupQueryValidator(IStringLocalizer<IdentityResources> localizer)
    {
        RuleFor(q => q)
            .Must(q => string.IsNullOrWhiteSpace(q.Search) != (q.Ids is null || q.Ids.Length == 0))
            .WithName("Search")
            .WithMessage(_ => localizer["Validation.LookupSearchOrIds"]);

        // Two characters at least, so a Basic user can't page through the whole tenant with "a", "b", ...
        RuleFor(q => q.Search!)
            .Must(s => s.Trim().Length >= GetUserLookupQuery.MinSearchLength)
            .WithMessage(_ => localizer["Validation.LookupSearchTooShort", GetUserLookupQuery.MinSearchLength])
            .MaximumLength(200)
            .When(q => !string.IsNullOrWhiteSpace(q.Search));

        RuleFor(q => q.Ids!)
            .Must(ids => ids.Length <= GetUserLookupQuery.MaxIds)
            .WithMessage(_ => localizer["Validation.LookupTooManyIds", GetUserLookupQuery.MaxIds])
            .Must(ids => ids.All(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 450))
            .WithMessage(_ => localizer["Validation.LookupIdInvalid"])
            .When(q => q.Ids is { Length: > 0 });

        RuleFor(q => q.Limit)
            .InclusiveBetween(1, GetUserLookupQuery.MaxResults)
            .When(q => q.Limit.HasValue);
    }
}
