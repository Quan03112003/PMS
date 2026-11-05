using Microsoft.EntityFrameworkCore;
namespace HotelPms.Api;

public static class AdvancedSeeder
{
    public static async Task Seed(PmsDbContext db)
    {
        foreach (var prop in await db.Properties.ToListAsync())
        {
            if (!await db.Buildings.AnyAsync(x => x.PropertyId == prop.Id))
            {
                var b = new HotelBuilding { PropertyId = prop.Id, Code = "MAIN", Name = "Tòa nhà chính" };
                db.Buildings.Add(b); await db.SaveChangesAsync();
                foreach (var room in await db.Rooms.Where(x => x.PropertyId == prop.Id && x.BuildingId == null).ToListAsync()) room.BuildingId = b.Id;
                await db.SaveChangesAsync();
            }
            if (!await db.RatePlans.AnyAsync(x => x.PropertyId == prop.Id))
            {
                var std = await db.RoomTypes.Where(x => x.PropertyId == prop.Id).OrderBy(x => x.BaseRate).Select(x => x.BaseRate).FirstOrDefaultAsync();
                db.RatePlans.AddRange(
                    new RatePlan { PropertyId = prop.Id, Code = "NIGHT", Name = "Qua đêm tiêu chuẩn", Mode = "OVERNIGHT", BasePrice = std, IncludedHours = 24 },
                    new RatePlan { PropertyId = prop.Id, Code = "HOUR2", Name = "Nghỉ giờ 2 tiếng", Mode = "HOURLY", BasePrice = 250000, IncludedHours = 2, ExtraHourPrice = 75000 },
                    new RatePlan { PropertyId = prop.Id, Code = "DAY", Name = "Nghỉ ngày", Mode = "DAY_USE", BasePrice = 450000, IncludedHours = 8, ExtraHourPrice = 80000 }
                );
                await db.SaveChangesAsync();
            }
            if (!await db.Suppliers.AnyAsync(x => x.PropertyId == prop.Id))
            {
                db.Suppliers.Add(new Supplier { PropertyId = prop.Id, Code = "SUP-DEMO", Name = "Nhà cung cấp mẫu", Phone = "" });
                await db.SaveChangesAsync();
            }
        }
        // Opening inventory movement for demo stock already present when upgrading from V1.
        foreach (var svc in await db.Services.Where(x => x.TrackInventory).ToListAsync())
        {
            if (!await db.StockMovements.AnyAsync(x => x.ServiceItemId == svc.Id))
                db.StockMovements.Add(new StockMovement { PropertyId = svc.PropertyId, ServiceItemId = svc.Id,
                    Type = "OPENING", QuantityChange = svc.StockQuantity, BalanceAfter = svc.StockQuantity,
                    Reference = "INITIAL_V1_OR_V2_STOCK", ActorUserId = 0 });
        }
        await db.SaveChangesAsync();
    }
}
