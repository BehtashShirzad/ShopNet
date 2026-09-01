using System;
using System.Collections.Generic;
using System.Text;

namespace InventoryService.Domain.Enums
{
    public enum ReservationStatus
    {
        Reserved = 1,
        Committed = 2,
        Released = 3,
        Expired = 4
    }
}
