using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using CatalogService.Domain.ExceptionMessages;
using Domain.Abstractions;

namespace CatalogService.Domain.Entities
{
    public class CategoryEntity : Entity<Guid>
    {
        public string Name { get; private set; } = null!;
        public static CategoryEntity Create(string name)
        {
            Guard.Against.NullOrEmpty(name,
            nameof(name),
            CategoryExceptionMessages.CategoryNameCannotBeNullOrEmpty);

            return new CategoryEntity()
            {
                Id = IdGenerator.New(),
                Name = name
            };



        }

        public void Update(string? name)
        {
            if(name != null &&  name != Name)
                Rename(name);
        }
        private void Rename(string newName)
        {
             Guard.Against.NullOrEmpty(newName,
            nameof(newName),
            CategoryExceptionMessages.CategoryNameCannotBeNullOrEmpty);
            Name=newName;
        }
    }
}