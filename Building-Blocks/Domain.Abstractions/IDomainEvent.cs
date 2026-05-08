using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
 

namespace Domain.Abstractions
{
    
   public interface IDomainEvent  
    {
        Guid Id { get; }
        DateTime OccurredOn { get; }
    }
    
}