using FluentValidation;
using Trislab.Framework.Core.Localization;
using Trislab.Framework.Web.Validation;
using Trislab.Modules.Identity.Contracts.v1.Invitations.GetInvitations;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Identity.Features.v1.Invitations.GetInvitations;

public sealed class GetInvitationsQueryValidator : AbstractValidator<GetInvitationsQuery>
{
    public GetInvitationsQueryValidator(IStringLocalizer<SharedResources> localizer)
    {
        Include(new PagedQueryValidator<GetInvitationsQuery>(localizer));
    }
}
