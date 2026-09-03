using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Abstractions
{
   public abstract class ValueObject : IEquatable<ValueObject>
    {
        public bool Equals(ValueObject? other) => other is not null && ValuesAreEqual(other);

        protected abstract IEnumerable<object> GetMemberValues();

        private bool ValuesAreEqual(ValueObject valueObject) =>
            GetMemberValues().SequenceEqual(valueObject.GetMemberValues());

        public override bool Equals(object? obj) =>
            obj is ValueObject valueObject && ValuesAreEqual(valueObject);

        public override int GetHashCode() =>
            GetMemberValues()
                .Aggregate(
                    default(int),
                    (hashCode, value) => HashCode.Combine(hashCode, value.GetHashCode())
                );

        public static bool operator ==(ValueObject? a, ValueObject? b)
        {
            if (a is null && b is null)
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            return a.Equals(b);
        }

        public static bool operator !=(ValueObject? a, ValueObject? b) => !(a == b);
    }
}
