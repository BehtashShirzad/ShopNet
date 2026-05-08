using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Application.Abstractions.Contracts
{
  public interface ICurrentUser
{
    string? UserId { get; }
}

}