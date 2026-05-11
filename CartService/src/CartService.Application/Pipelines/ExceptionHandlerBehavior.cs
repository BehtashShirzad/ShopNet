using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CartService.Application.Pipelines
{
    public class ExceptionHandlerBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly ILogger<ExceptionHandlerBehavior<TRequest, TResponse>> _logger;

        public ExceptionHandlerBehavior(
            ILogger<ExceptionHandlerBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("New Request");
                return await next();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unhandled exception for request {RequestName} {@Request}",
                    typeof(TRequest).Name,
                    request);

                throw;
            }
        }
    }
}