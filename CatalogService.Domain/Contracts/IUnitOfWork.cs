using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CatalogService.Domain.Contracts
{
  public interface IUnitOfWork
{
    public   Task<int> PersistAsync(CancellationToken cancellationToken = default);
    public   Task<int> PersistTransactionalAsync(CancellationToken cancellationToken = default);
}

}