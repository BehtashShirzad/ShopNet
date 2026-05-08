using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Abstractions
{
    public abstract class Entity
    {
          public Guid ModifierId { get ;set; }
        public DateTime ModifiedAt { get ;set; }
        public Guid CreatorId { get ;set; }
        public DateTime CreatedAt { get ;set; }
    }
 

public abstract class Entity<TID> : Entity, IEntity<TID>, IEquatable<Entity<TID>>
    where TID : IEquatable<TID>
{
    public TID Id { get; init; } = default!;
       

        protected Entity() { }

    protected Entity(TID id)
    {
        Id = id;
    }

    public bool Equals(Entity<TID>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj)
        => Equals(obj as Entity<TID>);

    public override int GetHashCode()
        => HashCode.Combine(Id);

    public static bool operator ==(Entity<TID>? left, Entity<TID>? right)
        => Equals(left, right);

    public static bool operator !=(Entity<TID>? left, Entity<TID>? right)
        => !(left == right);
}

}