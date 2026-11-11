using MediatR;

namespace HotelPms.Api;

public sealed record ComputeLineTax(decimal Amount, decimal RatePercent, bool Inclusive) : IRequest<decimal>;

public sealed class ComputeLineTaxHandler : IRequestHandler<ComputeLineTax, decimal>
{
    public Task<decimal> Handle(ComputeLineTax request, CancellationToken cancellationToken) =>
        Task.FromResult(TaxMath.Amount(request.Amount, request.RatePercent, request.Inclusive));
}
