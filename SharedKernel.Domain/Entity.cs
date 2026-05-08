using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SharedKernel.Domain
{
    public class Entity<TID> : IEntity<TID>, IEquatable<Entity<TID>>
      where TID : IEquatable<TID>
    {

        public Entity(TID id)
        {
            Id = id;
        }

        public TID Id{get;init;}
         protected Entity()
        {
            Id = default!;
        }

       

        public bool Equals(Entity<TID>? other)
        {
            return Equals(other as object);
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (GetType() != obj.GetType())
            {
                return false;
            }

            var otherObject = (Entity<TID>)obj;

            return Id.Equals(otherObject.Id);
        }

        public static bool operator ==(Entity<TID> left, Entity<TID> right)
        {
            if (Equals(left, null))
            {
                return Equals(right, null);
            }
            else
            {
                return left.Equals(right);
            }
        }

        public static bool operator !=(Entity<TID> left, Entity<TID> right)
        {
            return !(left == right);
        }

        public override int GetHashCode() => Id.GetHashCode() ^ 31;
    }
}