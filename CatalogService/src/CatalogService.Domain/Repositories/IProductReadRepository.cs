using System.Linq.Expressions;
using CatalogService.Domain.Aggregates;

namespace CatalogService.Domain;

public interface IProductReadRepository
{
    public Task<(bool CategoryExsits,int ProductNameCount)> ValidateProductExists(string productName,Guid categoryId,CancellationToken ctx);

    public Task<ProductAggregate?> GetProductAsync(
        Expression<Func<ProductAggregate, bool>> predicate,
        CancellationToken cancellationToken = default);
    public Task<List<ProductAggregate>> GetProductsAsync();

}
