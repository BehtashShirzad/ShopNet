using Application.Abstractions.Contracts;
using CatalogService.Domain;
using CatalogService.Domain.Aggregates;
using CatalogService.Domain.Entities;

namespace CatalogService.Infrastructure;

public class ProductWriteRepository(IApplicationDbContext dbContext):IProductWriteRepository
{
    readonly IApplicationDbContext _dbContext=dbContext;
    
    public   void AddProduct(ProductAggregate product)
    {
        _dbContext.Set<ProductAggregate>().Add(product);
    }

  
    public void UpdateProduct(ProductAggregate product)
    {
        _dbContext.Set<ProductAggregate>().Update(product);
        
    }

  
}