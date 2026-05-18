
using System.Runtime.CompilerServices;
 
using MediatR;
using Microsoft.EntityFrameworkCore;
using Application.Abstractions;
using Application.Abstractions.Contracts;

namespace CatalogService.Application.Pipelines;
 


public class TransactionBehavior<TRequest, TResponse>(IApplicationDbContext context)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IApplicationDbContext _context = context;
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
         
        if (request is not IBaseCommand)
            return await next();

        if (_context.Database.CurrentTransaction != null)
            return await next();

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next();

            await _context.SaveChangesAsync(cancellationToken);

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

 