using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;

namespace Application.Abstractions.Contracts
{
    public interface IQuery:IRequest 
    {
        
    }
     public interface IQuery<TResponse>:IRequest<TResponse>
    {
        
    }
}