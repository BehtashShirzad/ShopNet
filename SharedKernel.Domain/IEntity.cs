using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SharedKernel.Domain
{
   public interface IEntity<out TId>
        where TId : notnull
    {
        TId Id { get; }
    }
}