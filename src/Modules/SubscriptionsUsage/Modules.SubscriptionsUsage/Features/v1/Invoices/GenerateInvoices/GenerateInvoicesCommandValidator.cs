using FluentValidation;
using Trislab.Modules.SubscriptionsUsage.Contracts.v1.Invoices;

namespace Trislab.Modules.SubscriptionsUsage.Features.v1.Invoices.GenerateInvoices;

public sealed class GenerateInvoicesCommandValidator : AbstractValidator<GenerateInvoicesCommand>
{
    public GenerateInvoicesCommandValidator()
    {
        RuleFor(x => x.PeriodYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.PeriodMonth).InclusiveBetween(1, 12);
    }
}
