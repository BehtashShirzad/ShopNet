using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;

namespace Domain.Abstractions
{
    
   public interface IDomainEvent :INotification
    {
        Guid Id { get; }
        DateTime OccurredOn { get; }
    }
    
}