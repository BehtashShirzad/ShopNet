using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InventoryService.Infrastructure;

public sealed class InventoryDesignTimeFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        // Tooling can generate migrations without starting RabbitMQ or connecting to the application DB.
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__InventoryServiceConnection")
            ?? "Server=localhost;Database=InventoryService;Integrated Security=true;TrustServerCertificate=true";
        return new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(connection).Options);
    }
}
