using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace HotelPms.Api;

public record CashAccountInput(int PropertyId, string Name);
public record CashMoveInput(decimal Amount, string? Method, string? Note);
public record CashTransferInput(int PropertyId, int FromAccountId, int ToAccountId, decimal Amount, string? Note);
public record LateCheckoutInput(decimal Amount);
public record GroupCutoffInput(int PropertyId, DateOnly CutoffDate);

public static class DeskNext
{
    static readonly string[] Methods = ["CASH", "TRANSFER", "CARD"];

    public static void MapDeskNext(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/cash-accounts", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.CanManageFinance(user) || !Ops.HasScope(user, propertyId)) return Results.Forbid();
            var accounts = await db.CashAccounts.Where(x => x.PropertyId == propertyId && x.IsActive).OrderBy(x => x.Name).ToListAsync();
            var moves = await db.CashMovements.Where(x => x.PropertyId == propertyId).ToListAsync();
            return Results.Ok(accounts.Select(a => new { a.Id, a.Name, balance = Balance(moves, a.Id) }));
        });
        api.MapPost("/cash-accounts", async (CashAccountInput req, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.CanManageFinance(user) || !Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
            var name = req.Name?.Trim() ?? "";
            if (name.Length < 2 || name.Length > 80) return Ops.Fail("Tên quỹ từ 2 đến 80 ký tự.");
            if (await db.CashAccounts.AnyAsync(x => x.PropertyId == req.PropertyId && x.Name == name)) return Ops.Conflict("Quỹ này đã có.");
            var account = new CashAccount { PropertyId = req.PropertyId, Name = name };
            db.CashAccounts.Add(account);
            await db.SaveChangesAsync();
            await Ops.Log(db, user, req.PropertyId, "CASH_ACCOUNT", "CashAccount", account.Id, name);
            return Results.Ok(account);
        });
        api.MapPost("/cash-accounts/{id:int}/deposit", (int id, CashMoveInput req, ClaimsPrincipal user, PmsDbContext db) => Move(id, req, user, db, "DEPOSIT"));
        api.MapPost("/cash-accounts/{id:int}/withdraw", (int id, CashMoveInput req, ClaimsPrincipal user, PmsDbContext db) => Move(id, req, user, db, "WITHDRAW"));
        api.MapPost("/cash-accounts/transfer", async (CashTransferInput req, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.CanManageFinance(user) || !Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
            if (req.Amount <= 0 || req.Amount > 10000000000m || req.FromAccountId == req.ToAccountId) return Ops.Fail("Chọn hai quỹ khác nhau và số tiền hợp lệ.");
            await using var tx = await db.Database.BeginTransactionAsync();
            var accounts = await db.CashAccounts.Where(x => x.PropertyId == req.PropertyId && x.IsActive && (x.Id == req.FromAccountId || x.Id == req.ToAccountId)).ToListAsync();
            if (accounts.Count != 2) return Ops.Fail("Quỹ không thuộc cơ sở.");
            var moves = await db.CashMovements.Where(x => x.PropertyId == req.PropertyId).ToListAsync();
            if (Balance(moves, req.FromAccountId) < req.Amount) return Ops.Conflict("Quỹ nguồn không đủ số dư.");
            db.CashMovements.Add(new CashMovement { PropertyId = req.PropertyId, AccountId = req.FromAccountId, CounterAccountId = req.ToAccountId, MovementType = "TRANSFER", Method = "INTERNAL", Amount = req.Amount, Note = Clean(req.Note), ActorUserId = Ops.UserId(user) });
            await db.SaveChangesAsync();
            await Ops.Log(db, user, req.PropertyId, "CASH_TRANSFER", "CashAccount", req.FromAccountId, req.Amount.ToString("0"));
            await tx.CommitAsync();
            return Results.Ok(new { req.FromAccountId, req.ToAccountId, req.Amount });
        });
        api.MapGet("/reports/cash-funds", async (int propertyId, DateOnly from, DateOnly to, string? type, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.CanManageFinance(user) || !Ops.HasScope(user, propertyId)) return Results.Forbid();
            if (from > to) return Ops.Fail("Từ ngày không được sau đến ngày");
            var zone = await db.Properties.Where(x => x.Id == propertyId).Select(x => x.TimeZoneId).FirstAsync();
            var start = V5Closeout.PropertyDayRangeUtc(zone, from).StartUtc;
            var end = V5Closeout.PropertyDayRangeUtc(zone, to).EndUtc;
            var wanted = string.IsNullOrWhiteSpace(type) ? null : type.Trim().ToUpperInvariant();
            var accounts = await db.CashAccounts.Where(x => x.PropertyId == propertyId).ToDictionaryAsync(x => x.Id, x => x.Name);
            var rows = await db.CashMovements.Where(x => x.PropertyId == propertyId && x.CreatedAtUtc >= start && x.CreatedAtUtc < end
                && (wanted == null || x.MovementType == wanted)).OrderByDescending(x => x.Id).Take(500).ToListAsync();
            return Results.Ok(rows.Select(x => new { x.Id, x.MovementType, x.Method, x.Amount, x.Note, x.CreatedAtUtc, account = accounts.GetValueOrDefault(x.AccountId), counterAccount = x.CounterAccountId is int other ? accounts.GetValueOrDefault(other) : null }));
        });
        api.MapGet("/reports/cash-funds/export", async (int propertyId, DateOnly from, DateOnly to, string? type, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.CanManageFinance(user) || !Ops.HasScope(user, propertyId)) return Results.Forbid();
            if (from > to) return Ops.Fail("Từ ngày không được sau đến ngày");
            var zone = await db.Properties.Where(x => x.Id == propertyId).Select(x => x.TimeZoneId).FirstAsync();
            var start = V5Closeout.PropertyDayRangeUtc(zone, from).StartUtc;
            var end = V5Closeout.PropertyDayRangeUtc(zone, to).EndUtc;
            var wanted = string.IsNullOrWhiteSpace(type) ? null : type.Trim().ToUpperInvariant();
            var accounts = await db.CashAccounts.Where(x => x.PropertyId == propertyId).ToDictionaryAsync(x => x.Id, x => x.Name);
            var rows = await db.CashMovements.Where(x => x.PropertyId == propertyId && x.CreatedAtUtc >= start && x.CreatedAtUtc < end && (wanted == null || x.MovementType == wanted)).OrderBy(x => x.Id).ToListAsync();
            var csv = new StringBuilder();
            csv.AppendLine("Ngay,Loai,Quy,Quy doi ung,Hinh thuc,So tien,Ghi chu");
            foreach (var row in rows)
                csv.AppendLine($"{row.CreatedAtUtc:yyyy-MM-dd},{row.MovementType},{accounts.GetValueOrDefault(row.AccountId)},{(row.CounterAccountId is int other ? accounts.GetValueOrDefault(other) : "")},{row.Method},{row.Amount},{row.Note}");
            return Results.Text("\uFEFF" + csv, "text/csv; charset=utf-8");
        });

        api.MapGet("/housekeeping/folio-board", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
            var stays = await (from stay in db.Stays
                join r in db.Reservations on stay.ReservationId equals r.Id
                join room in db.Rooms on stay.RoomId equals room.Id
                join g in db.Guests on r.GuestId equals g.Id
                where r.PropertyId == propertyId && stay.Status == "IN_HOUSE"
                orderby room.Number
                select new { stay.Id, room.Number, g.FullName, r.Code }).ToListAsync();
            var services = await db.Services.Where(x => x.PropertyId == propertyId && x.IsActive).OrderBy(x => x.Name).ToListAsync();
            return Results.Ok(new { stays, services = services.Where(x => StaffAccess.IsMinibarOrLaundry(x.Code)).Select(x => new { x.Id, x.Code, x.Name, x.Price, x.StockQuantity, x.TrackInventory }) });
        });

        api.MapPost("/reservations/{id:int}/no-show", async (int id, ClaimsPrincipal user, PmsDbContext db) => {
            var r = await db.Reservations.FindAsync(id);
            if (r == null) return Results.NotFound();
            if (!Ops.HasScope(user, r.PropertyId)) return Results.Forbid();
            if (r.RateConfidential && !V5Desk.CanSeeRate(user)) return Ops.Fail("Booking bảo mật giá. Chỉ quản lý hoặc kế toán ghi no-show.");
            if (r.Status != "CONFIRMED") return Ops.Fail("Chỉ phiếu đã xác nhận, chưa nhận phòng, mới ghi no-show.");
            var zone = await db.Properties.Where(x => x.Id == r.PropertyId).Select(x => x.TimeZoneId).FirstAsync();
            if (r.CheckInDate >= V5Closeout.PropertyToday(zone)) return Ops.Fail("Khách vẫn có thể đến. Chỉ ghi no-show sau ngày nhận phòng.");
            if (await db.Charges.AnyAsync(x => x.ReservationId == r.Id && x.Category == "NO_SHOW")) return Ops.Conflict("Phiếu đã có dòng doanh thu no-show.");
            var nights = RoomCardPricing.ReadNights(r.RatePlanSnapshot, r.CheckInDate, r.CheckOutDate);
            var amount = nights.Count > 0 ? nights[0].Amount : r.NightlyRate;
            if (amount <= 0) return Ops.Fail("Phiếu không có giá đêm để ghi no-show.");
            r.Status = "NO_SHOW";
            db.Charges.Add(new FolioCharge { ReservationId = r.Id, Category = "NO_SHOW", Description = "Doanh thu no-show", Quantity = 1, UnitPrice = amount, Amount = amount });
            await db.SaveChangesAsync();
            await Ops.Log(db, user, r.PropertyId, "NO_SHOW", "Reservation", r.Id, amount.ToString("0"));
            return Results.Ok(new { r.Id, r.Status, amount });
        });

        api.MapPost("/stays/{id:int}/late-checkout", async (int id, LateCheckoutInput req, ClaimsPrincipal user, PmsDbContext db) => {
            var stay = await db.Stays.FindAsync(id);
            if (stay == null) return Results.NotFound();
            var r = await db.Reservations.FindAsync(stay.ReservationId);
            if (r == null) return Results.NotFound();
            if (!Ops.HasScope(user, r.PropertyId)) return Results.Forbid();
            if (stay.Status != "IN_HOUSE") return Ops.Fail("Chỉ ghi phụ thu trả muộn khi khách đang ở.");
            if (req.Amount <= 0 || req.Amount > 10000000000m) return Ops.Fail("Số tiền phụ thu không hợp lệ.");
            if (await db.Charges.AnyAsync(x => x.ReservationId == r.Id && x.Category == "LATE_CHECKOUT")) return Ops.Conflict("Phiếu đã có một dòng phụ thu trả muộn.");
            db.Charges.Add(new FolioCharge { ReservationId = r.Id, Category = "LATE_CHECKOUT", Description = "Phụ thu trả muộn", Quantity = 1, UnitPrice = req.Amount, Amount = req.Amount });
            await db.SaveChangesAsync();
            await Ops.Log(db, user, r.PropertyId, "LATE_CHECKOUT", "Stay", stay.Id, req.Amount.ToString("0"));
            return Results.Ok(new { stay.Id, amount = req.Amount });
        });

        api.MapPut("/groups/{code}/cutoff", async (string code, GroupCutoffInput req, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
            var members = await db.Reservations.Where(x => x.PropertyId == req.PropertyId && x.GroupCode == code && x.Status != "CANCELLED").ToListAsync();
            if (members.Count == 0) return Results.NotFound();
            if (members.Any(x => req.CutoffDate >= x.CheckInDate)) return Ops.Fail("Ngày khóa sổ phải trước ngày nhận phòng.");
            var row = await db.GroupCutoffs.FirstOrDefaultAsync(x => x.PropertyId == req.PropertyId && x.Code == code);
            if (row == null) { row = new GroupCutoff { PropertyId = req.PropertyId, Code = code, CutoffDate = req.CutoffDate }; db.GroupCutoffs.Add(row); }
            else row.CutoffDate = req.CutoffDate;
            await db.SaveChangesAsync();
            await Ops.Log(db, user, req.PropertyId, "GROUP_CUTOFF", "GroupCutoff", row.Id, code);
            return Results.Ok(new { code, row.CutoffDate });
        });
        api.MapPost("/groups/{code}/release", async (string code, int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
            var cutoff = await db.GroupCutoffs.FirstOrDefaultAsync(x => x.PropertyId == propertyId && x.Code == code);
            if (cutoff == null) return Ops.Fail("Đoàn chưa có ngày khóa sổ.");
            var zone = await db.Properties.Where(x => x.Id == propertyId).Select(x => x.TimeZoneId).FirstAsync();
            if (V5Closeout.PropertyToday(zone) <= cutoff.CutoffDate) return Ops.Fail("Chưa đến ngày khóa sổ. Phòng chưa nhận vẫn giữ.");
            var waiting = await db.Reservations.Where(x => x.PropertyId == propertyId && x.GroupCode == code && x.Status == "CONFIRMED").ToListAsync();
            foreach (var booking in waiting) booking.Status = "CANCELLED";
            await db.SaveChangesAsync();
            await Ops.Log(db, user, propertyId, "GROUP_RELEASE", "GroupCutoff", cutoff.Id, $"{code}:{waiting.Count}");
            return Results.Ok(new { code, released = waiting.Count, kept = await db.Reservations.CountAsync(x => x.PropertyId == propertyId && x.GroupCode == code && x.Status != "CANCELLED") });
        });
    }

    static async Task<IResult> Move(int id, CashMoveInput req, ClaimsPrincipal user, PmsDbContext db, string type)
    {
        var method = (req.Method ?? "CASH").Trim().ToUpperInvariant();
        if (req.Amount <= 0 || req.Amount > 10000000000m || !Methods.Contains(method)) return Ops.Fail("Số tiền hoặc hình thức không hợp lệ.");
        var account = await db.CashAccounts.FindAsync(id);
        if (account == null || !account.IsActive) return Results.NotFound();
        if (!Ops.CanManageFinance(user) || !Ops.HasScope(user, account.PropertyId)) return Results.Forbid();
        var moves = await db.CashMovements.Where(x => x.PropertyId == account.PropertyId).ToListAsync();
        if (type == "WITHDRAW" && Balance(moves, account.Id) < req.Amount) return Ops.Conflict("Quỹ không đủ số dư.");
        db.CashMovements.Add(new CashMovement { PropertyId = account.PropertyId, AccountId = account.Id, MovementType = type, Method = method, Amount = req.Amount, Note = Clean(req.Note), ActorUserId = Ops.UserId(user) });
        await db.SaveChangesAsync();
        await Ops.Log(db, user, account.PropertyId, "CASH_" + type, "CashAccount", account.Id, req.Amount.ToString("0"));
        return Results.Ok(new { account.Id, type, req.Amount, balance = Balance(moves, account.Id) + (type == "DEPOSIT" ? req.Amount : -req.Amount) });
    }

    static decimal Balance(IEnumerable<CashMovement> moves, int accountId) =>
        moves.Sum(x => x.AccountId == accountId
            ? x.MovementType == "DEPOSIT" ? x.Amount : -x.Amount
            : x.CounterAccountId == accountId && x.MovementType == "TRANSFER" ? x.Amount : 0);

    static string? Clean(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}
