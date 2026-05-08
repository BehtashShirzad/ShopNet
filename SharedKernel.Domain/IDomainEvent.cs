using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SharedKernel.Domain
{
    
   public interface IDomainEvent 
    {
        Guid Id { get; }
        DateTime OccurredOn { get; }
    }
   public interface IDomainEvent<TID>
    {
        TID Id { get; }
        DateTime OccurredOn { get; }
    }
}