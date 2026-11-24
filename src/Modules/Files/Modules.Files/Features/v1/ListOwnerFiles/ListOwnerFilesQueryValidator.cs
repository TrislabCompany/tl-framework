using FluentValidation;
using Trislab.Modules.Files.Contracts.v1.Queries;

namespace Trislab.Modules.Files.Features.v1.ListOwnerFiles;

public sealed class ListOwnerFilesQueryValidator : AbstractValidator<ListOwnerFilesQuery>
{
    public ListOwnerFilesQueryValidator()
    {
        RuleFor(x => x.OwnerType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.OwnerId).NotEmpty();
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
