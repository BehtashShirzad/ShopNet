using CatalogService.Domain.Entities;

namespace CatalogService.Domain;

public interface ICategoryWriteRepository
{
    public Task AddCategory(CategoryEntity category);
    public void UpdateCategory(CategoryEntity category);
}