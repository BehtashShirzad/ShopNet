using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Application.Abstractions.Contracts
{
   public interface IBus
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class;

        Task PublishIntegratedMessage<T>(T message, CancellationToken cancellationToken = default)
        where T : class;
}

}