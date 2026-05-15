using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OrderService.Domain.Enums
{
public enum OrderStatus
{
    
    Pending = 1,
    InventoryReserved = 2,
    PaymentProcessing = 3,
    Confirmed = 4,
    Shipped = 5,
    Cancelled = 6,
    Failed = 7
}


}