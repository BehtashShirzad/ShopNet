using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;

namespace Application.Abstractions.Contracts
{
     public interface IQueryHandler<TRequest>:IRequestHandler<TRequest> where TRequest:IQuery
    {
        
    }

      public interface IQueryHandler<TRequest,TResponse>:IRequestHandler<TRequest,TResponse> 
      where TRequest:IQuery<TResponse>
    {
        
    }
}