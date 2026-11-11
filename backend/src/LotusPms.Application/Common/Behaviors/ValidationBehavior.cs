using FluentValidation;
using MediatR;

namespace HotelPms.Api;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var failures = (await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken))))
                .SelectMany(result => result.Errors)
                .Where(error => !string.IsNullOrWhiteSpace(error.ErrorMessage))
                .ToList();
            if (failures.Count > 0 && typeof(TResponse) == typeof(string))
                return (TResponse)(object)failures[0].ErrorMessage;
        }
        return await next();
    }
}
