using FluentValidation;
using MediatR;

namespace SkyScan.Application.Common.Behaviors
{
    /// <summary>
    /// Runs every registered FluentValidation validator for TRequest before the handler
    /// executes. On failure it throws FluentValidation's own ValidationException rather
    /// than a bespoke result type, so it flows through GlobalExceptionMiddleware's
    /// existing catch-all exactly like any other unhandled exception: Development sees
    /// the aggregated failure messages via exception.Message, every other environment
    /// gets the same generic message everything else gets. No new error-response shape
    /// is introduced.
    /// </summary>
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (!_validators.Any())
            {
                return await next();
            }

            var context = new ValidationContext<TRequest>(request);

            var failures = (await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken))))
                .SelectMany(result => result.Errors)
                .Where(failure => failure != null)
                .ToList();

            if (failures.Count != 0)
            {
                throw new ValidationException(failures);
            }

            return await next();
        }
    }
}
