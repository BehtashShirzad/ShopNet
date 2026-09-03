using System.Linq.Expressions;
using CatalogService.Domain;
using CatalogService.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Infrastructure;

public class ProductReadRepository(QueryDbContext queryDbContext):IProductReadRepository
{
    public async Task<(bool CategoryExsits,int ProductNameCount)> ValidateProductExists(string productName, Guid categoryId,CancellationToken ctx)
    {
     var    result = await queryDbContext.Categories
            .AsNoTracking()
            .Where(c => c.Id == categoryId)
            .Select(c => new 
            { 
                CategoryExists = true,
                ProductNameCount = queryDbContext.Products
                    .AsNoTracking()
                    .Count(p => p.Name == productName && p.CategoryId == categoryId)
            })
            .FirstOrDefaultAsync(ctx);
     return (result?.CategoryExists??false, result?.ProductNameCount??0);
    }

    public async Task<ProductAggregate?> GetProductAsync(
        Expression<Func<ProductAggregate, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var product = await queryDbContext.Products
            .FirstOrDefaultAsync(predicate, cancellationToken);

        return product;
    }

    public   Task<List<ProductAggregate>> GetProductsAsync()
    {
        return   queryDbContext.Products.ToListAsync();
    }
}
