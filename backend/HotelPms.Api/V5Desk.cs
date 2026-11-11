using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HotelPms.Api;

public record PriceGuardInput(decimal MinNightlyRate, decimal MaxDiscountPercent);
public record DebtApprovalInput(decimal Amount, string? Reason);
public record ConfidentialInput(bool RateConfidential);

public static class V5Desk
{
    public static void MapV5Desk(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/reports/nationality", async (int propertyId, DateOnly from, DateOnly to, ClaimsPrincipal user, PmsDbContext db) => {
            if (from > to) return Ops.Fail("Khoảng ngày không hợp lệ");
            var query = db.Reservations.Where(r => r.Status != "CANCELLED" && r.CheckInDate <= to && r.CheckOutDate > from);
            if (propertyId == 0) {
                if (!user.IsInRole("CHAIN_ADMIN")) return Results.Forbid();
            } else {
                if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
                query = query.Where(r => r.PropertyId == propertyId);
            }
            var rows = await (from r in query
                join g in db.Guests on r.GuestId equals g.Id
                join p in db.Properties on r.PropertyId equals p.Id
                select new { Property = p.Name, g.Nationality, GuestId = g.Id, r.BookingMode, r.CheckInDate, r.CheckOutDate }).ToListAsync();
            var report = rows.GroupBy(x => new { x.Property, Nation = string.IsNullOrWhiteSpace(x.Nationality) ? "Chưa khai" : x.Nationality.Trim() })
                .Select(g => new {
                    property = g.Key.Property, nationality = g.Key.Nation,
                    guests = g.Select(x => x.GuestId).Distinct().Count(),
                    roomNights = g.Where(x => x.BookingMode == "OVERNIGHT").Sum(x => OverlapNights(x.CheckInDate, x.CheckOutDate, from, to))
                }).OrderBy(x => x.property).ThenByDescending(x => x.roomNights).ToList();
            return Results.Ok(report);
        });
        api.MapGet("/guests/{id:int}/debts", async (int id, int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
            var lines = await (from d in db.Debts
                join r in db.Reservations on d.ReservationId equals r.Id
                where d.Status == "OPEN" && r.GuestId == id && r.PropertyId == propertyId
                orderby d.Id
                select new { d.Id, r.Code, d.OutstandingAmount, d.Note, d.CreatedAtUtc }).ToListAsync();
            return Results.Ok(new { outstanding = lines.Sum(x => x.OutstandingAmount), lines });
        });
        api.MapPost("/reservations/{id:int}/debt-approval", async (int id, DebtApprovalInput req, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.CanManage(user)) return Results.Forbid();
            if (req.Amount < 0 || req.Amount > 10000000000m) return Ops.Fail("Hạn mức nợ không hợp lệ");
            var reason = Ops.TrimOrNull(req.Reason);
            if (reason == null || reason.Length > 300) return Ops.Fail("Cần lý do duyệt nợ, tối đa 300 ký tự");
            var reservation = await db.Reservations.FindAsync(id);
            if (reservation == null) return Results.NotFound();
            if (!Ops.HasScope(user, reservation.PropertyId)) return Results.Forbid();
            reservation.ApprovedExposure = req.Amount;
            await db.SaveChangesAsync();
            await Ops.Log(db, user, reservation.PropertyId, "DEBT_APPROVAL", "Reservation", reservation.Id, reason);
            return Results.Ok(new { reservation.Id, reservation.ApprovedExposure });
        });
        api.MapPatch("/reservations/{id:int}/confidential", async (int id, ConfidentialInput req, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.CanManage(user)) return Ops.Fail("Chỉ quản lý được gắn hoặc gỡ bảo mật giá");
            var reservation = await db.Reservations.FindAsync(id);
            if (reservation == null) return Results.NotFound();
            if (!Ops.HasScope(user, reservation.PropertyId)) return Results.Forbid();
            reservation.RateConfidential = req.RateConfidential;
            await db.SaveChangesAsync();
            await Ops.Log(db, user, reservation.PropertyId, "RATE_CONFIDENTIAL", "Reservation", reservation.Id, req.RateConfidential ? "ON" : "OFF");
            return Results.Ok(new { reservation.Id, reservation.RateConfidential });
        });
        api.MapPatch("/room-types/{id:int}/price-guard", async (int id, PriceGuardInput req, ClaimsPrincipal user, PmsDbContext db) => {
            if (!user.IsInRole("CHAIN_ADMIN")) return Ops.Fail("Chỉ quản trị chuỗi được đặt giá sàn và trần giảm");
            if (req.MinNightlyRate < 0 || req.MaxDiscountPercent < 0 || req.MaxDiscountPercent > 100) return Ops.Fail("Giá sàn hoặc trần giảm không hợp lệ");
            var type = await db.RoomTypes.FindAsync(id);
            if (type == null) return Results.NotFound();
            type.MinNightlyRate = req.MinNightlyRate;
            type.MaxDiscountPercent = req.MaxDiscountPercent;
            await db.SaveChangesAsync();
            CatalogCache.ForgetRoomTypes(type.PropertyId);
            await Ops.Log(db, user, type.PropertyId, "PRICE_GUARD", "RoomType", type.Id);
            return Results.Ok(new { type.Id, type.MinNightlyRate, type.MaxDiscountPercent });
        });
        api.MapGet("/shifts/exposure", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
            var rows = await (from r in db.Reservations
                join g in db.Guests on r.GuestId equals g.Id
                join room in db.Rooms on r.RoomId equals room.Id
                where r.PropertyId == propertyId && r.Status == "CHECKED_IN"
                select new { r.Id, r.Code, g.FullName, RoomNumber = room.Number, r.ApprovedExposure,
                    Charges = db.Charges.Where(c => c.ReservationId == r.Id).Sum(c => (decimal?)c.Amount) ?? 0,
                    Payments = db.Payments.Where(p => p.ReservationId == r.Id).Sum(p => (decimal?)p.Amount) ?? 0 }).ToListAsync();
            var over = rows.Where(x => x.Charges > x.Payments + x.ApprovedExposure)
                .Select(x => new { x.Id, x.Code, x.FullName, x.RoomNumber, x.Charges, x.Payments, x.ApprovedExposure, shortfall = x.Charges - x.Payments - x.ApprovedExposure });
            return Results.Ok(over);
        });
    }

    public static bool CanSeeRate(ClaimsPrincipal user) =>
        user.IsInRole("CHAIN_ADMIN") || user.IsInRole("PROPERTY_MANAGER") || user.IsInRole("ACCOUNTING_AGENT");

    public static bool HideRate(ClaimsPrincipal user, bool confidential) => confidential && !CanSeeRate(user);

    public static string? Guard(RoomType type, string mode, decimal cardUnit, decimal? asked)
    {
        return StayPrice.Guard(type, mode, cardUnit, asked);
    }

    public static decimal Floor(RoomType type, string mode, decimal cardUnit) => StayPrice.Floor(type, mode, cardUnit);

    public static async Task<Dictionary<int, decimal>> OpenDebtByGuest(PmsDbContext db, IEnumerable<int> guestIds)
    {
        var ids = guestIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        var rows = await (from d in db.Debts
            join r in db.Reservations on d.ReservationId equals r.Id
            where d.Status == "OPEN" && ids.Contains(r.GuestId)
            group d by r.GuestId into g
            select new { GuestId = g.Key, Amount = g.Sum(x => x.OutstandingAmount) }).ToListAsync();
        return rows.ToDictionary(x => x.GuestId, x => x.Amount);
    }

    private static int OverlapNights(DateOnly checkIn, DateOnly checkOut, DateOnly from, DateOnly to)
    {
        var start = checkIn > from ? checkIn : from;
        var endExclusive = to.AddDays(1);
        var end = checkOut < endExclusive ? checkOut : endExclusive;
        var nights = end.DayNumber - start.DayNumber;
        return nights > 0 ? nights : 0;
    }
}
