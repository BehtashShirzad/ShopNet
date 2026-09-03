using CatalogService.Domain.Aggregates;
using CatalogService.Domain.Entities;

namespace CatalogService.Domain;

public interface IProductWriteRepository
{
    void AddProduct(ProductAggregate product);
    void UpdateProduct(ProductAggregate product);
 
}
