using Application.Abstractions.Contracts;
using Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderService.Infrastructure;

public sealed class OrderDesignTimeFactory : IDesignTimeDbContextFactory<WriteDbContext>
{
    public WriteDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<WriteDbContext>().UseSqlServer(
            Environment.GetEnvironmentVariable("ConnectionStrings__OrderServiceConnection") ??
            "Server=localhost;Database=OrderService;Integrated Security=true;TrustServerCertificate=true").Options,
            new ToolingDomainEventBus());

    private sealed class ToolingDomainEventBus : IDomainEventBus
    {
        public Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent
            => throw new NotSupportedException("The tooling context must not execute application domain events.");
    }
}
