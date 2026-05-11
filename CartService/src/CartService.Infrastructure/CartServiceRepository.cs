using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CartService.Domain;
using CartService.Domain.Aggregates;
using Newtonsoft.Json;

namespace CartService.Infrastructure
{
    public class CartServiceRepository(IRedisService dbService): IRepository
    {
        public async Task<CartAggregate?> GetCart(Guid cartId)
        {
             var cart =await dbService.GetValue(cartId.ToString());
             if (cart == null)
                 return null;
            return JsonConvert.DeserializeObject<CartAggregate>(cart,
    new JsonSerializerSettings
    {
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
    });

        }

        public async Task StoreCart(CartAggregate cart)
        {
            var cartJson = JsonConvert.SerializeObject(cart);
            await dbService.SetValue(cart.Id.ToString(), cartJson);
        }
    }
}