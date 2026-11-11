using Microsoft.EntityFrameworkCore;

namespace HotelPms.Api;

public static class CatalogCache
{
    static readonly TimeSpan Life = TimeSpan.FromSeconds(45);
    static readonly System.Collections.Concurrent.ConcurrentDictionary<int, (DateTime At, List<RoomType> Rows)> RoomTypes = new();
    static readonly System.Collections.Concurrent.ConcurrentDictionary<int, (DateTime At, List<TaxRule> Rows)> TaxRules = new();

    public static void ForgetRoomTypes(int propertyId) => RoomTypes.TryRemove(propertyId, out _);
    public static void ForgetTaxRules(int propertyId) => TaxRules.TryRemove(propertyId, out _);

    public static async Task<List<RoomType>> RoomTypesFor(PmsDbContext db, int propertyId)
    {
        if (RoomTypes.TryGetValue(propertyId, out var hit) && DateTime.UtcNow - hit.At < Life) return hit.Rows;
        var rows = await db.RoomTypes.AsNoTracking().Where(x => x.PropertyId == propertyId).OrderBy(x => x.Name).ToListAsync();
        RoomTypes[propertyId] = (DateTime.UtcNow, rows);
        return rows;
    }

    public static async Task<List<TaxRule>> TaxRulesFor(PmsDbContext db, int propertyId)
    {
        if (TaxRules.TryGetValue(propertyId, out var hit) && DateTime.UtcNow - hit.At < Life) return hit.Rows;
        var rows = await db.TaxRules.AsNoTracking().Where(x => x.PropertyId == propertyId).OrderBy(x => x.Code).ThenByDescending(x => x.EffectiveFrom).ToListAsync();
        TaxRules[propertyId] = (DateTime.UtcNow, rows);
        return rows;
    }
}
