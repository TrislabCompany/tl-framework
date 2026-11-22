using FluentValidation;
using Trislab.Modules.Files.Contracts.v1.Commands;

namespace Trislab.Modules.Files.Features.v1.RestoreFile;

public sealed class RestoreFileCommandValidator : AbstractValidator<RestoreFileCommand>
{
    public RestoreFileCommandValidator()
    {
        RuleFor(x => x.FileAssetId).NotEmpty();
    }
}
