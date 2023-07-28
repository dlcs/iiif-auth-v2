using IIIFAuth2.API.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Z.EntityFramework.Plus;

namespace IIIFAuth2.API.Data;

public static class QueryHelpers
{
    public static Task<IEnumerable<T>> GetCachedCustomerRecords<T>(this DbSet<T> entity, int customerId,
        params string[] cacheKeys)
        where T : class, IHaveCustomer
        => entity
            .AsNoTracking()
            .Where(r => r.Customer == customerId)
            .FromCacheAsync(cacheKeys.Concat(new[] { CacheKeys.Customer(customerId) }).ToArray());
}