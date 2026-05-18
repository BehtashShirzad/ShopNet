using MediatR;

namespace Application.Abstractions.Contracts;

 
 
 
public interface IBaseCommand { } 

 
public interface ICommand : IRequest, IBaseCommand { }

 
public interface ICommand<out TResponse> : IRequest<TResponse>, IBaseCommand { }
 


