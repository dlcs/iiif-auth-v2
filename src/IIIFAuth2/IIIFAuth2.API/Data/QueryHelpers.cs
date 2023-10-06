using IIIFAuth2.API.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Z.EntityFramework.Plus;

namespace IIIFAuth2.API.Data;

public static class QueryHelpers
{
    /// <summary>
    /// Get cached, untracked entities for specified customer. Entities are cached by Customer id and any additional
    /// cache keys
    /// </summary>
    public static Task<IEnumerable<T>> GetCachedCustomerRecords<T>(this DbSet<T> entity, int customerId,
        params string[] additionalCacheKeys)
        where T : class, IHaveCustomer
        => entity
            .AsNoTracking()
            .Where(r => r.Customer == customerId)
            .FromCacheAsync(additionalCacheKeys.Concat(new[] { CacheKeys.Customer(customerId) }).ToArray());
}