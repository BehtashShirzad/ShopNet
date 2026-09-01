using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Abstractions
{
    public class DomainException:Exception
    {
        public DomainException(string exception):base(exception)
        {
            
        }
        
    }
}