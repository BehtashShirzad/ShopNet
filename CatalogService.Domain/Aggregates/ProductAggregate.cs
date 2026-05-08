
using Ardalis.GuardClauses;
using CatalogService.Domain.DomainEvents;
using CatalogService.Domain.ExceptionMessages;
using SharedKernel.Domain;

namespace CatalogService.Domain.Aggregates
{
    public class ProductAggregate : AggregateRoot<System.Guid>
    {
        private ProductAggregate()
        {

        }

        public Guid CategoryId { get; private set; }
        public string Name { get; private set; } = null!;
        public string? Description { get; private set; }
        public decimal Price { get; private set; }

        public static ProductAggregate Create(Guid categoryId, string name, string description, decimal price)
        {
            Guard.Against.NullOrEmpty(
            categoryId, nameof(categoryId),
            ProductExceptionMessages.InvalidCategoryId);


            Guard.Against.NullOrEmpty(
            name, nameof(name),
            ProductExceptionMessages.NameCannotBeNullOrEmpty);

            Guard.Against.NegativeOrZero(
            price,
            nameof(price),
            ProductExceptionMessages.PriceMustBeGreaterThanZero);

            var product = new ProductAggregate
            {
                Id = IdGenerator.New(),
                Name = name,
                Description = description,
                Price = price,
                CategoryId = categoryId
            };

            product.RaiseEvent(new ProductCreatedDomainEvent(product.Id));
            return product;
        }

        public void Update(Guid? categoryId, string? newName, decimal? newPrice, string? newDescription)
        {
            var changed = false;

            if (newName != null && newName != Name)
            {
                Rename(newName);
                changed = true;
            }

            if (newDescription != null && newDescription != Description)
            {
                ReviseDescription(newDescription);
                changed = true;
            }

            if (newPrice.HasValue && newPrice.Value != Price)
            {
                ChangePrice(newPrice.Value);
                changed = true;
            }

            if (categoryId.HasValue && categoryId.Value != CategoryId)
            {
                ChangeCategory(categoryId.Value);
                changed = true;
            }

            if (changed)
                RaiseEvent(new ProductUpdatedDomainEvent(Id));

        }




        #region  Helper Methods
        private void ChangePrice(decimal newPrice)
        {
            Guard.Against.NegativeOrZero(
            newPrice,
            nameof(newPrice),
            ProductExceptionMessages.PriceMustBeGreaterThanZero);

            Price = newPrice;
        }

        private void ChangeCategory(Guid categoryId)
        {
            Guard.Against.NullOrEmpty(
           categoryId, nameof(categoryId),
           ProductExceptionMessages.InvalidCategoryId);

            CategoryId = categoryId;
        }

        private void ReviseDescription(string newDescription)
        {
            Description = newDescription;
        }

        private void Rename(string newName)
        {
            Guard.Against.NullOrEmpty(
            newName, nameof(newName),
            ProductExceptionMessages.NameCannotBeNullOrEmpty);

            Name = newName;
        }
        #endregion




    }
}