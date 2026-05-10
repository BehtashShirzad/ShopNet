using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;

namespace Application.Abstractions.Contracts
{
    public interface ICommandHandler<TRequest>:IRequestHandler<TRequest> where TRequest:ICommand
    {
        
    }

      public interface ICommandHandler<TRequest,TResponse>:IRequestHandler<TRequest,TResponse> 
      where TRequest:ICommand<TResponse>
    {
        
    }
}