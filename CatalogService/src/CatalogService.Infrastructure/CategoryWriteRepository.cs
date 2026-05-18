using Application.Abstractions.Contracts;
using CatalogService.Domain;
using CatalogService.Domain.Entities;

namespace CatalogService.Infrastructure;

public class CategoryWriteRepository(IApplicationDbContext  dbContext) : ICategoryWriteRepository
{
    readonly  IApplicationDbContext _dbContext=dbContext;
    public async  Task AddCategory(CategoryEntity category)
    {
       await _dbContext.Set<CategoryEntity>().AddAsync(category);
    }
    public void UpdateCategory(CategoryEntity category)
    {
        _dbContext.Set<CategoryEntity>().Update(category);
        
    }
}