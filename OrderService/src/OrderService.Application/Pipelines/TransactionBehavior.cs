
using System.Runtime.CompilerServices;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Application.Abstractions;
using Application.Abstractions.Contracts;

namespace OrderService.Application.Pipelines
{


public class TransactionBehavior<TRequest, TResponse>(IApplicationDbContext context)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IBaseCommand)
            return await next();
        
        if (context.Database.CurrentTransaction != null)
            return await next();

        await using var transaction = 
            await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next();

            await context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return response;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
 
}

}