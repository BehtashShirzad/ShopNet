using Application.Abstractions;
using Domain.Abstractions;

namespace BuildingBlocks.UnitTests;

public class DomainPrimitivesTests
{
    [Fact]
    public void Entities_WithSameId_AreEqual()
    {
        var id = Guid.NewGuid();
        var first = new TestEntity(id);
        var second = new TestEntity(id);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Entities_WithDifferentIds_AreNotEqual()
    {
        var first = new TestEntity(Guid.NewGuid());
        var second = new TestEntity(Guid.NewGuid());

        Assert.NotEqual(first, second);
        Assert.True(first != second);
        Assert.False(first == second);
    }

    [Fact]
    public void AggregateRoot_RaisesAndClearsEvents()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        var domainEvent = new TestDomainEvent();

        aggregate.RaiseEvent(domainEvent);

        Assert.Same(domainEvent, Assert.Single(aggregate.DomainEvents));
        aggregate.ClearEvents();
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void ValueObjects_UseStructuralEquality()
    {
        var first = new TestValueObject("code", 4);
        var same = new TestValueObject("code", 4);
        var different = new TestValueObject("code", 5);

        Assert.Equal(first, same);
        Assert.True(first == same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, different);
        Assert.True(first != different);
        Assert.True((TestValueObject?)null == null);
    }

    [Fact]
    public void IdGenerator_ProducesUniqueVersionSevenIds()
    {
        var first = IdGenerator.New();
        var second = IdGenerator.New();

        Assert.NotEqual(Guid.Empty, first);
        Assert.NotEqual(first, second);
        Assert.Equal(7, first.Version);
    }

    [Fact]
    public void DomainException_PreservesMessage()
    {
        var exception = new DomainException("domain failure");

        Assert.Equal("domain failure", exception.Message);
    }

    [Fact]
    public void DomainEventNotification_PreservesEventInstance()
    {
        var domainEvent = new TestDomainEvent();

        var notification = new DomainEventNotification<TestDomainEvent>(domainEvent);

        Assert.Same(domainEvent, notification.DomainEvent);
    }

    private sealed class TestEntity(Guid id) : Entity<Guid>(id);

    private sealed class TestAggregate(Guid id) : AggregateRoot<Guid>(id);

    private sealed class TestValueObject(string code, int number) : ValueObject
    {
        protected override IEnumerable<object> GetMemberValues()
        {
            yield return code;
            yield return number;
        }
    }

    private sealed record TestDomainEvent : IDomainEvent
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }
}
