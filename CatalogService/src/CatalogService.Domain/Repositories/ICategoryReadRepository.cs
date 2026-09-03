using System.Linq.Expressions;
using CatalogService.Domain.Entities;

namespace CatalogService.Domain;

public interface ICategoryReadRepository
{
    public Task<bool> CategoryExists(Expression<Func<CategoryEntity, bool>> expression,
        CancellationToken cancellationToken);

}
