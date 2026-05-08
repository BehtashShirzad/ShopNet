using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Application.Abstractions.Contracts;

namespace CatalogService.Infrastructure
{
  public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _httpContextAccessor = accessor;
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
}

}