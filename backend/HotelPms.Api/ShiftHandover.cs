using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace HotelPms.Api;

public sealed class ShiftHandover
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int CashShiftId { get; set; }
    public int FromUserId { get; set; }
    public int ToUserId { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "SUBMITTED";
    [Precision(18, 2)] public decimal OpeningAmount { get; set; }
    [Precision(18, 2)] public decimal ExpectedCash { get; set; }
    [Precision(18, 2)] public decimal DeclaredCash { get; set; }
    [Precision(18, 2)] public decimal Variance { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAtUtc { get; set; }
    public int? AcknowledgedByUserId { get; set; }
}
public sealed class ShiftHandoverItem
{
    public int Id { get; set; }
    public int HandoverId { get; set; }
    [MaxLength(24)] public string Kind { get; set; } = "";
    [MaxLength(40)] public string Reference { get; set; } = "";
    [MaxLength(240)] public string Summary { get; set; } = "";
}
public sealed class ShiftHandoverNote
{
    public int Id { get; set; }
    public int HandoverId { get; set; }
    public int AuthorUserId { get; set; }
    [MaxLength(500)] public string Text { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public record HandoverInput(int ToUserId, decimal DeclaredCash, string? Note);
public record HandoverNoteInput(string? Text);

public static class ShiftHandoverEndpoints
{
    public static void MapShiftHandover(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/shift-handover-receivers", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
            var me = Ops.UserId(user);
            var rows = await db.Users.Where(u => u.IsActive && u.Id != me && (u.PropertyId == null || u.PropertyId == propertyId))
                .OrderBy(u => u.FullName).Select(u => new { u.Id, u.FullName, u.Role }).Take(40).ToListAsync();
            return Results.Ok(rows);
        });
        api.MapGet("/shift-handovers", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
            var books = await db.ShiftHandovers.Where(x => x.PropertyId == propertyId).OrderByDescending(x => x.Id).Take(30).ToListAsync();
            return Results.Ok(await Present(db, books));
        });
        api.MapGet("/shifts/{id:int}/handover-preview", async (int id, ClaimsPrincipal user, PmsDbContext db) => {
            var shift = await db.Shifts.FindAsync(id);
            if (shift == null) return Results.NotFound();
            if (!Ops.HasScope(user, shift.PropertyId)) return Results.Forbid();
            var expected = await ExpectedCash(db, shift);
            var items = await Snapshot(db, shift.PropertyId);
            return Results.Ok(new { shiftId = shift.Id, openingAmount = shift.OpeningAmount, expectedCash = expected, items });
        });
        api.MapPost("/shifts/{id:int}/handover", async (int id, HandoverInput req, ClaimsPrincipal user, PmsDbContext db) => {
            var shift = await db.Shifts.FindAsync(id);
            if (shift == null) return Results.NotFound();
            if (!Ops.HasScope(user, shift.PropertyId) || (shift.UserId != Ops.UserId(user) && !Ops.CanManageFinance(user))) return Results.Forbid();
            if (shift.Status != "OPEN") return Ops.Conflict("Chỉ giao ca đang mở");
            if (req.DeclaredCash < 0) return Ops.Fail("Tiền mặt đếm được không âm");
            var note = Ops.TrimOrNull(req.Note);
            if (note?.Length > 500) return Ops.Fail("Ghi chú tối đa 500 ký tự");
            var me = Ops.UserId(user);
            if (req.ToUserId == me) return Ops.Fail("Người nhận phải khác người giao");
            var receiver = await db.Users.FirstOrDefaultAsync(u => u.Id == req.ToUserId && u.IsActive);
            if (receiver == null || (receiver.PropertyId is int pid && pid != shift.PropertyId)) return Ops.Fail("Người nhận không thuộc cơ sở hoặc đã ngừng dùng");
            var expected = await ExpectedCash(db, shift);
            var book = await db.ShiftHandovers.FirstOrDefaultAsync(x => x.CashShiftId == shift.Id);
            if (book?.Status == "ACKNOWLEDGED") return Ops.Conflict("Sổ giao ca đã khóa, chỉ được bổ sung ghi chú");
            if (book == null) {
                book = new ShiftHandover { PropertyId = shift.PropertyId, CashShiftId = shift.Id };
                db.ShiftHandovers.Add(book);
            } else {
                db.ShiftHandoverItems.RemoveRange(await db.ShiftHandoverItems.Where(x => x.HandoverId == book.Id).ToListAsync());
            }
            book.FromUserId = me; book.ToUserId = receiver.Id; book.Status = "SUBMITTED";
            book.OpeningAmount = shift.OpeningAmount; book.ExpectedCash = expected; book.DeclaredCash = req.DeclaredCash;
            book.Variance = req.DeclaredCash - expected; book.Note = note;
            book.SubmittedAtUtc = DateTime.UtcNow; book.AcknowledgedAtUtc = null; book.AcknowledgedByUserId = null;
            await db.SaveChangesAsync();
            db.ShiftHandoverItems.AddRange((await Snapshot(db, shift.PropertyId)).Select(x => new ShiftHandoverItem {
                HandoverId = book.Id, Kind = x.Kind, Reference = x.Reference, Summary = x.Summary }));
            await db.SaveChangesAsync();
            await Ops.Log(db, user, shift.PropertyId, "HANDOVER_SUBMIT", "ShiftHandover", book.Id);
            return Results.Ok((await Present(db, [book])).Single());
        });
        api.MapPost("/shift-handovers/{id:int}/acknowledge", async (int id, ClaimsPrincipal user, PmsDbContext db) => {
            var book = await db.ShiftHandovers.FindAsync(id);
            if (book == null) return Results.NotFound();
            if (!Ops.HasScope(user, book.PropertyId)) return Results.Forbid();
            var me = Ops.UserId(user);
            if (book.Status != "SUBMITTED") return Ops.Conflict("Sổ giao ca không còn chờ nhận");
            if (me == book.FromUserId) return Ops.Fail("Người nhận phải khác người giao");
            if (me != book.ToUserId) return Ops.Fail("Chỉ người được chỉ định mới xác nhận nhận ca");
            book.Status = "ACKNOWLEDGED"; book.AcknowledgedAtUtc = DateTime.UtcNow; book.AcknowledgedByUserId = me;
            await db.SaveChangesAsync();
            await Ops.Log(db, user, book.PropertyId, "HANDOVER_ACK", "ShiftHandover", book.Id);
            return Results.Ok((await Present(db, [book])).Single());
        });
        api.MapPost("/shift-handovers/{id:int}/notes", async (int id, HandoverNoteInput req, ClaimsPrincipal user, PmsDbContext db) => {
            var book = await db.ShiftHandovers.FindAsync(id);
            if (book == null) return Results.NotFound();
            if (!Ops.HasScope(user, book.PropertyId)) return Results.Forbid();
            if (book.Status != "ACKNOWLEDGED") return Ops.Conflict("Chỉ bổ sung sau khi người nhận đã xác nhận");
            var text = Ops.TrimOrNull(req.Text);
            if (text == null || text.Length > 500) return Ops.Fail("Nội dung bổ sung từ 1 đến 500 ký tự");
            var note = new ShiftHandoverNote { HandoverId = book.Id, AuthorUserId = Ops.UserId(user), Text = text };
            db.ShiftHandoverNotes.Add(note);
            await db.SaveChangesAsync();
            await Ops.Log(db, user, book.PropertyId, "HANDOVER_NOTE", "ShiftHandover", book.Id);
            return Results.Ok(new { note.Id, note.Text, note.CreatedAtUtc });
        });
    }

    public static async Task<bool> IsAcknowledged(PmsDbContext db, int shiftId) =>
        await db.ShiftHandovers.AnyAsync(x => x.CashShiftId == shiftId && x.Status == "ACKNOWLEDGED");

    private static async Task<decimal> ExpectedCash(PmsDbContext db, CashShift shift)
    {
        var cashIn = await db.Payments.Where(x => x.ShiftId == shift.Id && x.Method == "CASH").SumAsync(x => (decimal?)x.Amount) ?? 0;
        var retail = await db.RetailSales.Where(x => x.ShiftId == shift.Id && x.PaymentMethod == "CASH").SumAsync(x => (decimal?)x.TotalAmount) ?? 0;
        var receipts = await db.Vouchers.Where(x => x.ShiftId == shift.Id && x.Method == "CASH" && x.Type == "RECEIPT").SumAsync(x => (decimal?)x.Amount) ?? 0;
        var expenses = await db.Vouchers.Where(x => x.ShiftId == shift.Id && x.Method == "CASH" && x.Type == "EXPENSE").SumAsync(x => (decimal?)x.Amount) ?? 0;
        return shift.OpeningAmount + cashIn + retail + receipts - expenses;
    }

    private static async Task<List<ItemDraft>> Snapshot(PmsDbContext db, int propertyId)
    {
        var zone = await db.Properties.Where(x => x.Id == propertyId).Select(x => x.TimeZoneId).FirstAsync();
        var today = V5Closeout.PropertyToday(zone);
        var rows = new List<ItemDraft>();
        rows.AddRange(await (
            from r in db.Reservations
            join g in db.Guests on r.GuestId equals g.Id
            join room in db.Rooms on r.RoomId equals room.Id
            where r.PropertyId == propertyId && r.Status == "CONFIRMED" && r.CheckInDate <= today
            orderby r.CheckInDate
            select new ItemDraft("ARRIVAL", r.Code, room.Number + " · " + g.FullName)).Take(12).ToListAsync());
        rows.AddRange(await (
            from r in db.Reservations
            join g in db.Guests on r.GuestId equals g.Id
            join room in db.Rooms on r.RoomId equals room.Id
            where r.PropertyId == propertyId && r.Status == "CHECKED_IN" && r.CheckOutDate <= today
            orderby r.CheckOutDate
            select new ItemDraft("DEPARTURE", r.Code, room.Number + " · " + g.FullName)).Take(12).ToListAsync());
        rows.AddRange(await (
            from n in db.ReservationNotes
            join r in db.Reservations on n.ReservationId equals r.Id
            join t in db.BookingNoteTypes on n.NoteTypeId equals t.Id
            where n.IsCurrent && r.PropertyId == propertyId && (r.Status == "CONFIRMED" || r.Status == "CHECKED_IN")
            orderby n.Id descending
            select new ItemDraft("NOTE", r.Code, t.Name + ": " + n.NoteText)).Take(12).ToListAsync());
        rows.AddRange(await db.Reservations.Where(r => r.PropertyId == propertyId && r.Note != null && r.Note != "" && (r.Status == "CONFIRMED" || r.Status == "CHECKED_IN"))
            .OrderByDescending(r => r.Id).Select(r => new ItemDraft("NOTE", r.Code, r.Note!)).Take(12).ToListAsync());
        rows.AddRange(await db.Rooms.Where(r => r.PropertyId == propertyId && r.IsOutOfOrder)
            .OrderBy(r => r.Number).Select(r => new ItemDraft("MAINTENANCE", r.Number, "Phòng " + r.Number + " đang bảo trì")).Take(12).ToListAsync());
        rows.AddRange(await (
            from task in db.HousekeepingTasks
            join room in db.Rooms on task.RoomId equals room.Id
            where task.PropertyId == propertyId && task.Status != "FINISH"
            orderby task.Id
            select new ItemDraft("HOUSEKEEPING", room.Number, room.Number + " · " + task.Kind + " · " + task.Status)).Take(12).ToListAsync());
        var debts = await (
            from debt in db.Debts
            join r in db.Reservations on debt.ReservationId equals r.Id
            where debt.PropertyId == propertyId && debt.Status == "OPEN"
            orderby debt.Id
            select new { r.Code, debt.OutstandingAmount }).Take(12).ToListAsync();
        rows.AddRange(debts.Select(x => new ItemDraft("DEBT", x.Code, "Còn " + x.OutstandingAmount.ToString("0"))));
        var desk = await db.DeskNotes.Where(n => n.PropertyId == propertyId && n.Status == "OPEN")
            .OrderByDescending(n => n.Id).Select(n => new { n.Id, n.Text }).Take(12).ToListAsync();
        rows.AddRange(desk.Select(n => new ItemDraft("DESK", "GC-" + n.Id, n.Text)));
        rows.AddRange(await (
            from r in db.Reservations
            join g in db.Guests on r.GuestId equals g.Id
            where r.PropertyId == propertyId && g.LoyaltyCard != null && g.LoyaltyCard != "" && (r.Status == "CONFIRMED" || r.Status == "CHECKED_IN")
            orderby r.Id
            select new ItemDraft("MEMBER", r.Code, g.FullName + " · thẻ " + g.LoyaltyCard)).Take(12).ToListAsync());
        return rows.Select(x => x with { Reference = Clip(x.Reference, 40), Summary = Clip(x.Summary, 240) }).ToList();
    }

    private static async Task<List<object>> Present(PmsDbContext db, List<ShiftHandover> books)
    {
        var ids = books.Select(x => x.Id).ToList();
        var userIds = books.SelectMany(x => new int?[] { x.FromUserId, x.ToUserId, x.AcknowledgedByUserId }).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        var items = await db.ShiftHandoverItems.Where(x => ids.Contains(x.HandoverId)).OrderBy(x => x.Id).ToListAsync();
        var notes = await db.ShiftHandoverNotes.Where(x => ids.Contains(x.HandoverId)).OrderBy(x => x.Id).ToListAsync();
        var noteAuthors = notes.Select(x => x.AuthorUserId).Distinct().Where(x => !names.ContainsKey(x)).ToList();
        if (noteAuthors.Count > 0)
            foreach (var row in await db.Users.Where(u => noteAuthors.Contains(u.Id)).Select(u => new { u.Id, u.FullName }).ToListAsync())
                names[row.Id] = row.FullName;
        return books.Select(book => (object)new {
            book.Id, book.PropertyId, book.CashShiftId, book.FromUserId, book.ToUserId, book.Status,
            fromName = names.GetValueOrDefault(book.FromUserId), toName = names.GetValueOrDefault(book.ToUserId),
            book.OpeningAmount, book.ExpectedCash, book.DeclaredCash, book.Variance, book.Note,
            book.SubmittedAtUtc, book.AcknowledgedAtUtc,
            items = items.Where(x => x.HandoverId == book.Id).Select(x => new { x.Kind, x.Reference, x.Summary }),
            notes = notes.Where(x => x.HandoverId == book.Id).Select(x => new { x.Id, x.Text, x.CreatedAtUtc, authorName = names.GetValueOrDefault(x.AuthorUserId) })
        }).ToList();
    }

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..max];
    private sealed record ItemDraft(string Kind, string Reference, string Summary);
}
