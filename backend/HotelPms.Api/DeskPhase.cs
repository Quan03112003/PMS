using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace HotelPms.Api;

public record VirtualFolioInput(int PropertyId, string GuestName, string? Phone, string? Note);
public record VirtualLineInput(int? ServiceId, int Quantity, decimal? DiscountAmount, string? Description);
public record VirtualPayInput(decimal Amount, string Method, string? Reference, string? IdempotencyKey);
public record NightRateInput(RoomCardPricing.NightAmount[] Nights);

public static class DeskPhase
{
    public static void MapDeskPhase(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/reservations/{id:int}/nights", async (int id, ClaimsPrincipal user, PmsDbContext db) => {
            var row = await (from r in db.Reservations join room in db.Rooms on r.RoomId equals room.Id
                join type in db.RoomTypes on room.RoomTypeId equals type.Id
                where r.Id == id select new { r, type }).FirstOrDefaultAsync();
            if (row == null) return Results.NotFound();
            if (!Ops.HasScope(user, row.r.PropertyId)) return Results.Forbid();
            if (row.r.RateConfidential && !V5Desk.CanSeeRate(user)) return Ops.Fail("Booking bảo mật giá. Chỉ quản lý hoặc kế toán xem giá từng đêm.");
            var nights = row.r.CheckOutDate.DayNumber - row.r.CheckInDate.DayNumber;
            var editable = row.r.Status == "CONFIRMED" && row.r.BookingMode == "OVERNIGHT" && nights >= 2;
            var saved = RoomCardPricing.ReadNights(row.r.RatePlanSnapshot, row.r.CheckInDate, row.r.CheckOutDate);
            var cards = RoomCardPricing.Cards(row.r.CheckInDate, row.r.CheckOutDate, row.type.WeekdayRate ?? row.type.BaseRate, row.type.WeekendRate ?? row.type.WeekdayRate ?? row.type.BaseRate, row.type, 0);
            var rows = cards.Select(card => new { card.Date, card.Card, card.Floor, amount = saved.FirstOrDefault(x => x.Date == card.Date)?.Amount ?? card.Card });
            return Results.Ok(new { editable, reason = editable ? "Sửa từng đêm trước khi nhận phòng. Giá đã chốt không đổi khi bảng giá đổi." : "Chỉ sửa giá từng đêm trên phiếu qua đêm từ 2 đêm, khi chưa nhận phòng.", nights = rows });
        });
        api.MapPatch("/reservations/{id:int}/night-rates", async (int id, NightRateInput req, ClaimsPrincipal user, PmsDbContext db) => {
            var row = await (from r in db.Reservations join room in db.Rooms on r.RoomId equals room.Id
                join type in db.RoomTypes on room.RoomTypeId equals type.Id
                where r.Id == id select new { r, type }).FirstOrDefaultAsync();
            if (row == null) return Results.NotFound();
            if (!Ops.HasScope(user, row.r.PropertyId)) return Results.Forbid();
            if (row.r.RateConfidential && !V5Desk.CanSeeRate(user)) return Ops.Fail("Booking bảo mật giá. Chỉ quản lý hoặc kế toán sửa giá.");
            if (row.r.Status != "CONFIRMED" || row.r.BookingMode != "OVERNIGHT") return Ops.Fail("Chỉ sửa giá từng đêm khi phiếu qua đêm chưa nhận phòng.");
            var cards = RoomCardPricing.Cards(row.r.CheckInDate, row.r.CheckOutDate, row.type.WeekdayRate ?? row.type.BaseRate, row.type.WeekendRate ?? row.type.WeekdayRate ?? row.type.BaseRate, row.type, 0);
            if (cards.Count < 2) return Ops.Fail("Phiếu một đêm dùng giá chung, không tách từng đêm.");
            var reject = RoomCardPricing.RejectNights(cards, req.Nights ?? []);
            if (reject != null) return Ops.Fail(reject);
            var current = RoomCardPricing.Read(row.r.RatePlanSnapshot);
            var lines = RoomCardPricing.WithNights(current.Count > 0 ? current : RoomCardPricing.Build(row.type, null, "OVERNIGHT", row.r.CheckInDate, row.r.CheckOutDate, row.r.Adults, row.r.Children, null, null).Lines, req.Nights!);
            row.r.RatePlanSnapshot = RoomCardPricing.Serialize("NIGHT_RATES", "OVERNIGHT", lines, row.type, null, null, req.Nights);
            row.r.NightlyRate = decimal.Round(req.Nights!.Average(x => x.Amount), 0, MidpointRounding.AwayFromZero);
            await db.SaveChangesAsync();
            await Ops.Log(db, user, row.r.PropertyId, "NIGHT_RATES", "Reservation", row.r.Id);
            return Results.Ok(new { row.r.Id, row.r.NightlyRate });
        });

        api.MapGet("/virtual-folios", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
            var folios = await db.VirtualFolios.Where(x => x.PropertyId == propertyId).OrderByDescending(x => x.Id).Take(100).ToListAsync();
            var ids = folios.Select(x => x.Id).ToArray();
            var lines = await db.VirtualFolioLines.Where(x => ids.Contains(x.FolioId)).ToListAsync();
            var pays = await db.VirtualFolioPayments.Where(x => ids.Contains(x.FolioId)).ToListAsync();
            return Results.Ok(folios.Select(f => {
                var charged = lines.Where(x => x.FolioId == f.Id).Sum(x => x.Amount);
                var paid = pays.Where(x => x.FolioId == f.Id).Sum(x => x.Amount);
                return new { f.Id, f.Code, f.GuestName, f.Phone, f.Note, f.Status, f.CreatedAtUtc, charged, paid, balance = charged - paid };
            }));
        });
        api.MapGet("/virtual-folios/{id:int}", async (int id, ClaimsPrincipal user, PmsDbContext db) => {
            var folio = await db.VirtualFolios.FindAsync(id);
            if (folio == null) return Results.NotFound();
            if (!Ops.HasScope(user, folio.PropertyId)) return Results.Forbid();
            var lines = await db.VirtualFolioLines.Where(x => x.FolioId == id).OrderBy(x => x.Id).ToListAsync();
            var pays = await db.VirtualFolioPayments.Where(x => x.FolioId == id).OrderBy(x => x.Id).ToListAsync();
            return Results.Ok(new { folio, lines, payments = pays, charged = lines.Sum(x => x.Amount), paid = pays.Sum(x => x.Amount), balance = lines.Sum(x => x.Amount) - pays.Sum(x => x.Amount) });
        });
        api.MapPost("/virtual-folios", async (VirtualFolioInput req, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(req.GuestName)) return Ops.Fail("Nhập tên khách.");
            var folio = new VirtualFolio { PropertyId = req.PropertyId, Code = "VF-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), GuestName = req.GuestName.Trim(), Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(), Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim() };
            db.VirtualFolios.Add(folio);
            await db.SaveChangesAsync();
            await Ops.Log(db, user, req.PropertyId, "VIRTUAL_FOLIO", "VirtualFolio", folio.Id, folio.Code);
            return Results.Ok(folio);
        });
        api.MapPost("/virtual-folios/{id:int}/lines", async (int id, VirtualLineInput req, ClaimsPrincipal user, PmsDbContext db) => {
            await using var tx = await db.Database.BeginTransactionAsync();
            var folio = await db.VirtualFolios.FindAsync(id);
            if (folio == null) return Results.NotFound();
            if (!Ops.HasScope(user, folio.PropertyId)) return Results.Forbid();
            if (folio.Status != "OPEN") return Ops.Conflict("Folio đã tất toán.");
            VirtualFolioLine line;
            if (req.DiscountAmount is > 0)
            {
                var charged = await db.VirtualFolioLines.Where(x => x.FolioId == id).SumAsync(x => (decimal?)x.Amount) ?? 0;
                if (req.DiscountAmount > charged) return Ops.Fail("Giảm giá không được lớn hơn tiền dịch vụ.");
                line = new VirtualFolioLine { FolioId = id, Kind = "DISCOUNT", Description = string.IsNullOrWhiteSpace(req.Description) ? "Giảm giá" : req.Description.Trim(), Quantity = 1, UnitPrice = -req.DiscountAmount.Value, Amount = -req.DiscountAmount.Value };
            }
            else
            {
                if (req.Quantity < 1 || req.Quantity > 500 || req.ServiceId is not int serviceId) return Ops.Fail("Chọn dịch vụ và số lượng.");
                if (!await Ops.LockService(db, serviceId)) return Ops.Fail("Dịch vụ không tồn tại");
                var svc = await db.Services.FirstOrDefaultAsync(x => x.Id == serviceId && x.PropertyId == folio.PropertyId && x.IsActive);
                if (svc == null) return Ops.Fail("Dịch vụ không tồn tại");
                if (svc.TrackInventory && svc.StockQuantity < req.Quantity) return Ops.Conflict("Không đủ số lượng tồn kho");
                if (svc.TrackInventory) svc.StockQuantity -= req.Quantity;
                line = new VirtualFolioLine { FolioId = id, ServiceItemId = svc.Id, Kind = "SERVICE", Description = svc.Name, Quantity = req.Quantity, UnitPrice = svc.Price, Amount = svc.Price * req.Quantity };
                db.VirtualFolioLines.Add(line);
                await db.SaveChangesAsync();
                if (svc.TrackInventory) db.StockMovements.Add(new StockMovement { PropertyId = folio.PropertyId, ServiceItemId = svc.Id, Type = "SALE", QuantityChange = -req.Quantity, BalanceAfter = svc.StockQuantity, Reference = $"VF-{folio.Id}", ActorUserId = Ops.UserId(user) });
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(line);
            }
            db.VirtualFolioLines.Add(line);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(line);
        });
        api.MapPost("/virtual-folios/{id:int}/payments", async (int id, VirtualPayInput req, ClaimsPrincipal user, PmsDbContext db) => {
            if (req.Amount <= 0 || !Ops.PayMethods.Contains(req.Method) || !Ops.ValidIdempotency(req.IdempotencyKey)) return Ops.Fail("Cần số tiền, phương thức và mã giao dịch.");
            await using var tx = await db.Database.BeginTransactionAsync();
            var folio = await db.VirtualFolios.FindAsync(id);
            if (folio == null) return Results.NotFound();
            if (!Ops.HasScope(user, folio.PropertyId)) return Results.Forbid();
            var replay = await db.VirtualFolioPayments.FirstOrDefaultAsync(x => x.IdempotencyKey == req.IdempotencyKey);
            if (replay != null) return Results.Ok(replay);
            var charged = await db.VirtualFolioLines.Where(x => x.FolioId == id).SumAsync(x => (decimal?)x.Amount) ?? 0;
            var paid = await db.VirtualFolioPayments.Where(x => x.FolioId == id).SumAsync(x => (decimal?)x.Amount) ?? 0;
            if (paid + req.Amount > charged) return Ops.Conflict("Thu vượt số còn lại của folio ngoài phòng.");
            int? shiftId = null;
            if (req.Method == "CASH")
            {
                var shift = await db.Shifts.FirstOrDefaultAsync(x => x.PropertyId == folio.PropertyId && x.UserId == Ops.UserId(user) && x.Status == "OPEN");
                if (shift == null) return Ops.Conflict("Cần mở ca thu ngân trước khi thu tiền mặt");
                shiftId = shift.Id;
            }
            var payment = new VirtualFolioPayment { FolioId = id, Amount = req.Amount, Method = req.Method, ShiftId = shiftId, Reference = req.Reference, IdempotencyKey = req.IdempotencyKey };
            db.VirtualFolioPayments.Add(payment);
            if (req.Method == "CITY_LEDGER")
            {
                var debt = await db.Debts.FirstOrDefaultAsync(x => x.VirtualFolioId == id);
                if (debt == null) { debt = new DebtReceivable { PropertyId = folio.PropertyId, VirtualFolioId = id, Origin = "CITY_LEDGER", Note = folio.Code }; db.Debts.Add(debt); }
                debt.Status = "OPEN"; debt.SettledAtUtc = null; debt.Origin = "CITY_LEDGER";
                debt.OriginalAmount += req.Amount; debt.OutstandingAmount += req.Amount;
            }
            if (paid + req.Amount == charged) folio.Status = "SETTLED";
            await db.SaveChangesAsync();
            await Ops.Log(db, user, folio.PropertyId, "VIRTUAL_PAY", "VirtualFolio", folio.Id, req.Method);
            await tx.CommitAsync();
            return Results.Ok(payment);
        });
    }
}
