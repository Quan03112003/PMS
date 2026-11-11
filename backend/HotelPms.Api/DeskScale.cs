using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HotelPms.Api;

public record GroupMasterOpenInput(int PropertyId, string Code, string GuestName);
public record GroupMasterLineInput(int PropertyId, string Code, int? ServiceItemId, string? Description, decimal? Amount, decimal? Discount);
public record GroupMasterPayInput(int PropertyId, string Code, string Method, decimal Amount, string? Reference, string? IdempotencyKey);
public record GroupReassignInput(int PropertyId, string Code, int ReservationId, int RoomId);

public static class DeskScale
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/housekeeping/productivity", async (int propertyId, DateOnly? from, DateOnly? to, ClaimsPrincipal user, PmsDbContext db) =>
        {
            if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
            var window = Ops.DateWindow(from, to, out var start, out var end);
            if (window != null) return Ops.Fail(window);
            var rows = db.HousekeepingTasks.Where(t => t.PropertyId == propertyId && t.Status == "FINISH" && t.FinishedAtUtc != null);
            if (from.HasValue) rows = rows.Where(t => t.FinishedAtUtc >= start && t.FinishedAtUtc < end);
            var done = await rows.Select(t => new { t.Cleaner1, t.Cleaner2, t.Cleaner3 }).Take(500).ToListAsync();
            var count = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in done)
                foreach (var name in new[] { row.Cleaner1, row.Cleaner2, row.Cleaner3 })
                    if (!string.IsNullOrWhiteSpace(name)) count[name.Trim()] = count.GetValueOrDefault(name.Trim()) + 1;
            return Results.Ok(count.OrderByDescending(x => x.Value).Select(x => new { cleaner = x.Key, rooms = x.Value }));
        });

        api.MapGet("/housekeeping/stock-alerts", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) =>
        {
            if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
            var items = await db.Services.Where(s => s.PropertyId == propertyId && s.IsActive && s.TrackInventory)
                .OrderBy(s => s.Name).Select(s => new { s.Id, s.Name, s.StockQuantity, s.MinStock, low = s.StockQuantity == 0 || (s.MinStock > 0 && s.StockQuantity <= s.MinStock) }).ToListAsync();
            return Results.Ok(items);
        });

        api.MapPost("/groups/master", async (GroupMasterOpenInput req, ClaimsPrincipal user, PmsDbContext db) =>
        {
            if (!Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
            var code = (req.Code ?? "").Trim().ToUpperInvariant();
            if (code.Length is < 2 or > 30 || string.IsNullOrWhiteSpace(req.GuestName)) return Ops.Fail("Cần mã đoàn và tên đoàn.");
            var folio = await db.GroupMasterFolios.FirstOrDefaultAsync(x => x.PropertyId == req.PropertyId && x.Code == code);
            if (folio == null)
            {
                folio = new GroupMasterFolio { PropertyId = req.PropertyId, Code = code, GuestName = req.GuestName.Trim() };
                db.GroupMasterFolios.Add(folio);
                await db.SaveChangesAsync();
            }
            return await MasterView(db, folio);
        });

        api.MapGet("/groups/master", async (int propertyId, string code, ClaimsPrincipal user, PmsDbContext db) =>
        {
            if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
            var folio = await FindMaster(db, propertyId, code);
            return folio == null ? Results.NotFound() : await MasterView(db, folio);
        });

        api.MapPost("/groups/master/lines", async (GroupMasterLineInput req, ClaimsPrincipal user, PmsDbContext db) =>
        {
            if (!Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
            var folio = await FindMaster(db, req.PropertyId, req.Code);
            if (folio == null || folio.Status != "OPEN") return Ops.Fail("Sổ đoàn chưa mở.");
            if (req.Discount is > 0)
            {
                var services = await db.GroupMasterLines.Where(x => x.FolioId == folio.Id && x.Kind == "SERVICE").SumAsync(x => x.Amount);
                if (req.Discount > services) return Ops.Fail("Giảm giá không được lớn hơn tiền dịch vụ trên sổ đoàn.");
                db.GroupMasterLines.Add(new GroupMasterLine { FolioId = folio.Id, Kind = "DISCOUNT", Description = "Giảm giá sổ đoàn", Amount = -req.Discount.Value });
            }
            else if (req.ServiceItemId is int serviceId)
            {
                var item = await db.Services.FirstOrDefaultAsync(s => s.Id == serviceId && s.PropertyId == req.PropertyId && s.IsActive);
                if (item == null) return Ops.Fail("Dịch vụ không thuộc cơ sở.");
                db.GroupMasterLines.Add(new GroupMasterLine { FolioId = folio.Id, Kind = "SERVICE", Description = item.Name, Amount = item.Price });
            }
            else return Ops.Fail("Chọn dịch vụ hoặc số giảm giá.");
            await db.SaveChangesAsync();
            return await MasterView(db, folio);
        });

        api.MapPost("/groups/master/payments", async (GroupMasterPayInput req, ClaimsPrincipal user, PmsDbContext db) =>
        {
            if (!Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
            var method = (req.Method ?? "").Trim().ToUpperInvariant();
            if (req.Amount <= 0 || method is not ("CASH" or "TRANSFER" or "CARD" or "MOMO" or "VNPAY" or "CITY_LEDGER")) return Ops.Fail("Phương thức hoặc số tiền không hợp lệ");
            var folio = await FindMaster(db, req.PropertyId, req.Code);
            if (folio == null || folio.Status != "OPEN") return Ops.Fail("Sổ đoàn chưa mở.");
            if (!string.IsNullOrWhiteSpace(req.IdempotencyKey) && await db.GroupMasterPayments.AnyAsync(p => p.IdempotencyKey == req.IdempotencyKey))
                return await MasterView(db, folio);
            if (method == "CASH")
            {
                var shift = await db.Shifts.FirstOrDefaultAsync(s => s.UserId == Ops.UserId(user) && s.PropertyId == req.PropertyId && s.Status == "OPEN");
                if (shift == null) return Ops.Fail("Mở ca thu ngân trước khi thu tiền mặt vào sổ đoàn.");
            }
            var lines = await db.GroupMasterLines.Where(x => x.FolioId == folio.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
            var paid = await db.GroupMasterPayments.Where(x => x.FolioId == folio.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
            if (paid + req.Amount > lines) return Ops.Fail("Thu vượt số còn lại trên sổ đoàn.");
            db.GroupMasterPayments.Add(new GroupMasterPayment { FolioId = folio.Id, Method = method, Amount = req.Amount, Reference = Ops.TrimOrNull(req.Reference), IdempotencyKey = Ops.TrimOrNull(req.IdempotencyKey) });
            if (method == "CITY_LEDGER")
                db.Debts.Add(new DebtReceivable { PropertyId = req.PropertyId, Origin = "GROUP_MASTER", OriginalAmount = req.Amount, OutstandingAmount = req.Amount, Status = "OPEN", Note = folio.Code });
            if (paid + req.Amount == lines) { folio.Status = "SETTLED"; folio.ClosedAtUtc = DateTime.UtcNow; }
            await db.SaveChangesAsync();
            return await MasterView(db, folio);
        });

        api.MapPost("/groups/reassign", async (GroupReassignInput req, ClaimsPrincipal user, PmsDbContext db) =>
        {
            if (!Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
            var code = (req.Code ?? "").Trim().ToUpperInvariant();
            var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.Id == req.ReservationId && r.PropertyId == req.PropertyId && r.GroupCode == code);
            if (reservation == null || reservation.Status != "CONFIRMED") return Ops.Fail("Chỉ đổi phòng của đoàn khi phòng còn đang xác nhận.");
            var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == req.RoomId && r.PropertyId == req.PropertyId && !r.IsOutOfOrder);
            if (room == null) return Ops.Fail("Phòng mới không thuộc cơ sở.");
            if (await Ops.IsBooked(db, room.Id, reservation.CheckInDate, reservation.CheckOutDate, reservation.Id))
                return Ops.Conflict("Phòng mới đã có khách trong khoảng ngày của đoàn.");
            reservation.RoomId = room.Id;
            await db.SaveChangesAsync();
            await Ops.Log(db, user, req.PropertyId, "GROUP_REASSIGN", "Reservation", reservation.Id, $"{code} -> {room.Number}");
            return Results.Ok(new { reservation.Id, room.Number });
        });

        api.MapGet("/groups/invoices", async (int propertyId, string code, ClaimsPrincipal user, PmsDbContext db) =>
        {
            if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
            var key = (code ?? "").Trim().ToUpperInvariant();
            var rooms = await (from r in db.Reservations
                               join room in db.Rooms on r.RoomId equals room.Id
                               join guest in db.Guests on r.GuestId equals guest.Id
                               where r.PropertyId == propertyId && r.GroupCode == key
                               orderby room.Number
                               select new { r.Id, r.Code, room.Number, guest.FullName, r.Status, r.CheckInDate, r.CheckOutDate }).ToListAsync();
            var ids = rooms.Select(x => x.Id).ToArray();
            var charges = await db.Charges.Where(c => ids.Contains(c.ReservationId)).GroupBy(c => c.ReservationId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync();
            var payments = await db.Payments.Where(p => ids.Contains(p.ReservationId)).GroupBy(p => p.ReservationId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync();
            return Results.Ok(rooms.Select(r => new { r.Code, r.Number, r.FullName, r.Status, r.CheckInDate, r.CheckOutDate, charges = charges.FirstOrDefault(c => c.Id == r.Id)?.Amount ?? 0, payments = payments.FirstOrDefault(p => p.Id == r.Id)?.Amount ?? 0 }));
        });

        api.MapGet("/reports/tax", async (int propertyId, DateOnly? from, DateOnly? to, ClaimsPrincipal user, PmsDbContext db, IMediator mediator) =>
        {
            if (!Ops.HasScope(user, propertyId) || !V5Desk.CanSeeRate(user)) return Results.Forbid();
            var window = Ops.DateWindow(from, to, out var start, out var end);
            if (window != null) return Ops.Fail(window);
            if (!from.HasValue) return Ops.Fail("Chọn đủ từ ngày và đến ngày.");
            var rules = await db.TaxRules.Where(r => r.PropertyId == propertyId && r.IsActive && r.EffectiveFrom <= to && (r.EffectiveTo == null || r.EffectiveTo >= from)).ToListAsync();
            var resIds = db.Reservations.Where(r => r.PropertyId == propertyId).Select(r => r.Id);
            var charges = await db.Charges.Where(c => resIds.Contains(c.ReservationId) && c.PostedAtUtc >= start && c.PostedAtUtc < end)
                .GroupBy(c => c.Category).Select(g => new { Category = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync();
            var rows = new List<object>();
            foreach (var charge in charges)
            {
                foreach (var rule in rules.Where(r => TaxMath.Matches(r.AppliesTo, charge.Category)))
                {
                    var tax = await mediator.Send(new ComputeLineTax(charge.Amount, rule.RatePercent, rule.IsInclusive));
                    rows.Add(new { rule.Code, rule.Name, rule.TaxType, baseAmount = charge.Amount, tax = decimal.Round(tax, 0, MidpointRounding.AwayFromZero), charge.Category });
                }
            }
            return Results.Ok(new { from, to, rows, note = "Bảng kê thuế để đối chiếu, chưa phải tờ khai gửi cơ quan thuế." });
        });
    }

    public static async Task EnsureSnapshots(PmsDbContext db, Property property, DateOnly from, DateOnly to, DateOnly today)
    {
        var last = to < today ? to : today.AddDays(-1);
        if (last < from) return;
        var have = await db.PropertyDaySnapshots.Where(x => x.PropertyId == property.Id && x.BusinessDate >= from && x.BusinessDate <= last).Select(x => x.BusinessDate).ToListAsync();
        for (var day = from; day <= last; day = day.AddDays(1))
        {
            if (have.Contains(day)) continue;
            var start = day.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            var end = day.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            var resIds = db.Reservations.Where(r => r.PropertyId == property.Id).Select(r => r.Id);
            var sums = await db.Charges.Where(c => resIds.Contains(c.ReservationId) && c.PostedAtUtc >= start && c.PostedAtUtc < end)
                .GroupBy(c => c.Category).Select(g => new { g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync();
            decimal Cat(string name) => sums.Where(x => x.Key == name).Sum(x => x.Amount);
            var payments = await db.Payments.Where(p => resIds.Contains(p.ReservationId) && p.PaidAtUtc >= start && p.PaidAtUtc < end).SumAsync(p => (decimal?)p.Amount) ?? 0;
            var expenses = await db.Vouchers.Where(v => v.PropertyId == property.Id && v.Type == "EXPENSE" && v.CreatedAtUtc >= start && v.CreatedAtUtc < end).SumAsync(v => (decimal?)v.Amount) ?? 0;
            db.PropertyDaySnapshots.Add(new PropertyDaySnapshot
            {
                PropertyId = property.Id, BusinessDate = day,
                RoomRevenue = Cat("ROOM"), ServiceRevenue = Cat("SERVICE"), NoShowRevenue = Cat("NO_SHOW"),
                Payments = payments, Expenses = expenses
            });
        }
        await db.SaveChangesAsync();
    }

    static async Task<GroupMasterFolio?> FindMaster(PmsDbContext db, int propertyId, string? code)
    {
        var key = (code ?? "").Trim().ToUpperInvariant();
        return await db.GroupMasterFolios.FirstOrDefaultAsync(x => x.PropertyId == propertyId && x.Code == key);
    }

    static async Task<IResult> MasterView(PmsDbContext db, GroupMasterFolio folio)
    {
        var lines = await db.GroupMasterLines.Where(x => x.FolioId == folio.Id).OrderBy(x => x.Id).ToListAsync();
        var payments = await db.GroupMasterPayments.Where(x => x.FolioId == folio.Id).OrderBy(x => x.Id).ToListAsync();
        var due = lines.Sum(x => x.Amount);
        var paid = payments.Sum(x => x.Amount);
        return Results.Ok(new { folio.Id, folio.Code, folio.GuestName, folio.Status, due, paid, balance = due - paid, lines, payments, note = "Sổ đoàn tách khỏi doanh thu phòng." });
    }
}
