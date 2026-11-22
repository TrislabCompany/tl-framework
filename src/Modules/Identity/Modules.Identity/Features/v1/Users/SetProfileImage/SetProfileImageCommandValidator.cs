using FluentValidation;
using Trislab.Framework.Core.Localization;
using Trislab.Framework.Storage;
using Trislab.Modules.Identity.Contracts.v1.Users.SetProfileImage;
using Microsoft.Extensions.Localization;

namespace Trislab.Modules.Identity.Features.v1.Users.SetProfileImage;

public sealed class SetProfileImageCommandValidator : AbstractValidator<SetProfileImageCommand>
{
    public SetProfileImageCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        // Empty/null is allowed (clears the image). When set, it must be an http(s) URL or a
        // server-relative path — never a file path, which the profile service would otherwise store and
        // later hand to the storage layer.
        RuleFor(x => x.ImageUrl)
            .Must(ClientImageUrl.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.ImageUrl))
            .WithMessage(_ => localizer["Validation.ImageUrlInvalid"]);
    }
}
