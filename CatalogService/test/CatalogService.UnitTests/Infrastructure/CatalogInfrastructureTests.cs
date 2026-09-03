using System.Security.Claims;
using Application.Abstractions;
using CatalogService.Infrastructure;
using Domain.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Moq;

namespace CatalogService.UnitTests;

public class CatalogInfrastructureTests
{
    [Fact]
    public void CurrentUser_ReturnsSubjectClaim()
    {
        var userId = Guid.NewGuid().ToString();
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim("sub", userId)
                ]))
            }
        };

        Assert.Equal(userId, new CurrentUser(accessor).UserId);
    }

    [Fact]
    public async Task MediatrDispatcher_WrapsConcreteDomainEvent()
    {
        var publisher = new Mock<IPublisher>();
        publisher.Setup(x => x.Publish(
                It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var domainEvent = new TestDomainEvent();

        await new MediatrDomainEventDispatcher(publisher.Object)
            .DispatchAsync(domainEvent);

        publisher.Verify(x => x.Publish(
            It.Is<INotification>(notification =>
                notification.GetType() == typeof(DomainEventNotification<TestDomainEvent>)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class TestDomainEvent : IDomainEvent
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }
}
