using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Abstractions
{
  public static class IdGenerator
    {
        public static Guid New() => Guid.CreateVersion7();
    }
}