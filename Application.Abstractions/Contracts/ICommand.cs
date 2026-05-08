using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;

namespace Application.Abstractions;

public interface ICommand { }

public interface ICommand<TResponse> : IRequest<TResponse>, ICommand { }

public interface ICommandVoid : IRequest, ICommand { }


