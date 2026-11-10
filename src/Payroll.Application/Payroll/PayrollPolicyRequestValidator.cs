using FluentValidation;
using Payroll.Application.Common;

namespace Payroll.Application.Payroll;

/// <summary>Input rules (400). The scheme-specific bracket shape is checked in the service once the scheme is loaded.</summary>
public sealed class PayrollPolicyRequestValidator : AbstractValidator<PayrollPolicyRequest>
{
    public PayrollPolicyRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.TaxSchemeId).GreaterThan(0).WithMessage("Tax scheme is required.");
        RuleFor(x => x.EffectiveFrom)
            .Must(date => date.Day == 1).WithMessage("A payroll policy must take effect on the first day of a month.")
            .Must(date => date >= clock.CurrentMonth().FirstDay)
            .WithMessage("A payroll policy can only take effect from the current month or later; past months are closed.");
        RuleFor(x => x.InsuranceRatePercent).InclusiveBetween(0, 100).PrecisionScale(5, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.InsuranceCap)
            .GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .When(x => x.InsuranceCap is not null);

        RuleFor(x => x.TaxBrackets).NotNull();
        RuleForEach(x => x.TaxBrackets).NotNull().ChildRules(bracket =>
        {
            bracket.RuleFor(b => b.RatePercent).InclusiveBetween(0, 100).PrecisionScale(5, 2, ignoreTrailingZeros: true);
            bracket.RuleFor(b => b.UpperBound)
                .GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true)
                .When(b => b.UpperBound is not null);
        });
        RuleFor(x => x.TaxBrackets).Custom((brackets, context) =>
        {
            if (brackets is null || brackets.Count == 0 || brackets.Any(b => b is null))
            {
                return;
            }

            for (var i = 0; i < brackets.Count; i++)
            {
                var isLast = i == brackets.Count - 1;
                var path = $"{nameof(PayrollPolicyRequest.TaxBrackets)}[{i}].{nameof(TaxBracketDto.UpperBound)}";
                if (isLast && brackets[i].UpperBound is not null)
                {
                    context.AddFailure(path, "The last tax bracket must be open-ended (no upper bound).");
                }
                else if (!isLast && brackets[i].UpperBound is null)
                {
                    context.AddFailure(path, "Only the last tax bracket can be open-ended.");
                }
                else if (i > 0 && brackets[i].UpperBound is { } upper && brackets[i - 1].UpperBound is { } previous && upper <= previous)
                {
                    context.AddFailure(path, "Upper bounds must be strictly ascending.");
                }
            }
        });
    }
}
