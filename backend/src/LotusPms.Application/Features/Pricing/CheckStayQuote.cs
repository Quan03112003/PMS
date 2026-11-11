using FluentValidation;
using MediatR;

namespace HotelPms.Api;

public sealed record CheckStayQuote(string Mode, DateOnly CheckIn, DateOnly CheckOut) : IRequest<string?>;

public sealed class CheckStayQuoteValidator : AbstractValidator<CheckStayQuote>
{
    public CheckStayQuoteValidator()
    {
        RuleFor(x => x).Custom((request, context) =>
        {
            var error = StayRules.PackageLength(request.Mode, request.CheckIn, request.CheckOut);
            if (error != null) context.AddFailure(error);
        });
    }
}

public sealed class CheckStayQuoteHandler : IRequestHandler<CheckStayQuote, string?>
{
    public Task<string?> Handle(CheckStayQuote request, CancellationToken cancellationToken) =>
        Task.FromResult(StayRules.PackageLength(request.Mode, request.CheckIn, request.CheckOut));
}
