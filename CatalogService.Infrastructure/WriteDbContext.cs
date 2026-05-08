using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Infrastructure
{
    public class WriteDbContext:DbContext
    {
        public WriteDbContext(DbContextOptions<WriteDbContext> options)
            : base(options)
        {
              ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        }
    }
}