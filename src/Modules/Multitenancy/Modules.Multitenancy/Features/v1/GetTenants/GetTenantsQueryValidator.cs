using FluentValidation;
using Trislab.Framework.Core.Localization;
using Trislab.Framework.Web.Validation;
using Trislab.Modules.Multitenancy.Contracts.v1.GetTenants;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Multitenancy.Features.v1.GetTenants;

public sealed class GetTenantsQueryValidator : AbstractValidator<GetTenantsQuery>
{
    public GetTenantsQueryValidator(IStringLocalizer<SharedResources> localizer)
    {
        Include(new PagedQueryValidator<GetTenantsQuery>(localizer));
    }
}