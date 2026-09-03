using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderService.Application.Inventory;

namespace OrderService.Infrastructure;

public sealed class SqlOrderTransactionLock(WriteDbContext context) : IOrderTransactionLock
{
    public Task AcquireAsync(string resource, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Order locks require the command pipeline transaction.");
        var parameter = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = "order-service:" + resource };
        return context.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = @resource,
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @result < 0 THROW 51000, 'Could not acquire order transaction lock.', 1;
            """, [parameter], cancellationToken);
    }
}
