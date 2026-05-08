using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;

namespace SharedKernel.Application;

public interface ICommand: IRequest
{
}

public interface ICommand<TResponse> : ICommand, IRequest<TResponse>
{
}

