using System.Linq.Expressions;
using CatalogService.Domain;
using CatalogService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Infrastructure;

public class CategoryReadRepository(QueryDbContext context):ICategoryReadRepository
{
    public Task<bool> CategoryExists(Expression<Func<CategoryEntity,bool>> expression, CancellationToken cancellationToken)
    {
       return context.Categories.AsNoTracking().AnyAsync(expression, cancellationToken);
    }
}