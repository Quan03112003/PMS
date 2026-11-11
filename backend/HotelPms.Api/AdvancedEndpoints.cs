using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using HotelPms.Api;
using Microsoft.EntityFrameworkCore;

namespace HotelPms.Api;

public static class AdvancedEndpoints
{
    public static void MapPmsV2(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.AddEndpointFilter(Ops.DepartmentAccess);

        // MANAGEMENT: buildings / multi-building property and tariffs.
        api.MapGet("/buildings", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) =>
            Ops.HasScope(actor, propertyId) ? (IResult)Results.Ok(await db.Buildings.Where(x => x.PropertyId == propertyId).OrderBy(x => x.Name).ToListAsync()) : Results.Forbid());
        api.MapPost("/buildings", async (BuildingInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name)) return Ops.Fail("Thiếu mã hoặc tên tòa nhà");
            if (await db.Buildings.AnyAsync(x => x.PropertyId == req.PropertyId && x.Code == req.Code.Trim())) return Ops.Conflict("Trùng mã tòa nhà");
            var b = new HotelBuilding { PropertyId = req.PropertyId, Code = req.Code.Trim(), Name = req.Name.Trim() };
            db.Buildings.Add(b); await db.SaveChangesAsync(); await Ops.Log(db, actor, req.PropertyId, "BUILDING_CREATE", "HotelBuilding", b.Id); return Results.Ok(b);
        });
        api.MapPatch("/rooms/{id:int}/building", async (int id, RoomBuildingInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            var room = await db.Rooms.FindAsync(id); if (room == null) return Results.NotFound();
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, room.PropertyId)) return Results.Forbid();
            if (req.BuildingId.HasValue && !await db.Buildings.AnyAsync(b => b.Id == req.BuildingId && b.PropertyId == room.PropertyId)) return Ops.Fail("Tòa nhà không thuộc khách sạn");
            room.BuildingId = req.BuildingId; await db.SaveChangesAsync(); await Ops.Log(db, actor, room.PropertyId, "ROOM_BUILDING", "Room", id); return Results.Ok(room);
        });
        api.MapGet("/rate-plans", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) =>
            Ops.HasScope(actor, propertyId) ? (IResult)Results.Ok(await db.RatePlans.Where(x => x.PropertyId == propertyId).OrderBy(x => x.Mode).ToListAsync()) : Results.Forbid());
        api.MapPost("/rate-plans", async (RatePlanInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            if (!new[] { "OVERNIGHT", "DAY_USE", "HOURLY" }.Contains(req.Mode) || string.IsNullOrWhiteSpace(req.Code) || req.BasePrice < 0 || req.ExtraHourPrice < 0 || req.IncludedHours < 1) return Ops.Fail("Gói giá không hợp lệ");
            if (await db.RatePlans.AnyAsync(x => x.PropertyId == req.PropertyId && x.Code == req.Code.Trim())) return Ops.Conflict("Trùng mã gói giá");
            var plan = new RatePlan { PropertyId = req.PropertyId, Code = req.Code.Trim(), Name = req.Name.Trim(), Mode = req.Mode, BasePrice = req.BasePrice,
                IncludedHours = req.IncludedHours, ExtraHourPrice = req.ExtraHourPrice };
            db.RatePlans.Add(plan); await db.SaveChangesAsync(); await Ops.Log(db, actor, req.PropertyId, "RATE_PLAN_CREATE", "RatePlan", plan.Id); return Results.Ok(plan);
        });

        // FRONT DESK: waitlist and booking groups.
        api.MapGet("/waitlist", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) =>
            Ops.HasScope(actor, propertyId) ? (IResult)Results.Ok(await db.Waitlist.Where(x => x.PropertyId == propertyId).OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync()) : Results.Forbid());
        api.MapPost("/waitlist", async (WaitlistInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(req.GuestName) || req.CheckInDate >= req.CheckOutDate) return Ops.Fail("Tên khách hoặc khoảng ngày không hợp lệ");
            if (req.RoomTypeId.HasValue && !await db.RoomTypes.AnyAsync(x => x.Id == req.RoomTypeId && x.PropertyId == req.PropertyId)) return Ops.Fail("Hạng phòng không thuộc khách sạn");
            var w = new WaitlistEntry { PropertyId = req.PropertyId, GuestName = req.GuestName.Trim(), Phone = req.Phone?.Trim() ?? "", CheckInDate = req.CheckInDate,
                CheckOutDate = req.CheckOutDate, RoomTypeId = req.RoomTypeId, Note = req.Note }; db.Waitlist.Add(w);
            await db.SaveChangesAsync(); await Ops.Log(db, actor, req.PropertyId, "WAITLIST_CREATE", "WaitlistEntry", w.Id); return Results.Ok(w);
        });
        api.MapPost("/waitlist/{id:int}/cancel", async (int id, ClaimsPrincipal actor, PmsDbContext db) => {
            var w = await db.Waitlist.FindAsync(id); if (w == null) return Results.NotFound(); if (!Ops.HasScope(actor, w.PropertyId)) return Results.Forbid();
            if (w.Status != "WAITING") return Ops.Conflict("Không còn trong hàng đợi"); w.Status = "CANCELLED";
            await db.SaveChangesAsync(); await Ops.Log(db, actor, w.PropertyId, "WAITLIST_CANCEL", "WaitlistEntry", id); return Results.Ok(w);
        });
        api.MapPost("/waitlist/{id:int}/assign", async (int id, AssignWaitlistInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var w = await db.Waitlist.FindAsync(id); if (w == null) return Results.NotFound(); if (!Ops.HasScope(actor, w.PropertyId)) return Results.Forbid();
            if (w.Status != "WAITING") return Ops.Conflict("Hàng đợi đã được xử lý");
            if (!await Ops.LockRoom(db, req.RoomId)) return Ops.Conflict("Phòng không tồn tại");
            var room = await db.Rooms.FirstOrDefaultAsync(x => x.Id == req.RoomId && x.PropertyId == w.PropertyId);
            if (room == null || room.IsOutOfOrder || (w.CheckInDate <= DateOnly.FromDateTime(DateTime.Today) && room.HousekeepingStatus != "INSPECTED") || (w.RoomTypeId.HasValue && room.RoomTypeId != w.RoomTypeId)) return Ops.Conflict("Phòng không đáp ứng yêu cầu");
            if (await Ops.IsBooked(db, room.Id, w.CheckInDate, w.CheckOutDate)) return Ops.Conflict("Phòng đã có đặt phòng");
            var guest = new Guest { FullName = w.GuestName, Phone = w.Phone }; db.Guests.Add(guest); await db.SaveChangesAsync();
            var rate = await db.RoomTypes.Where(x => x.Id == room.RoomTypeId).Select(x => x.BaseRate).SingleAsync();
            var booking = new Reservation { Code = BookingCode(), PropertyId = w.PropertyId, RoomId = room.Id, GuestId = guest.Id,
                CheckInDate = w.CheckInDate, CheckOutDate = w.CheckOutDate, NightlyRate = rate, Note = w.Note };
            db.Reservations.Add(booking); await db.SaveChangesAsync(); w.Status = "ASSIGNED"; w.ReservationId = booking.Id;
            await db.SaveChangesAsync(); await Ops.Log(db, actor, w.PropertyId, "WAITLIST_ASSIGN", "Reservation", booking.Id);
            await tx.CommitAsync(); return Results.Ok(new { bookingId = booking.Id, booking.Code, waitlistId = w.Id });
        });
        api.MapPost("/group-bookings", async (GroupBookingInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            if (req.CheckInDate >= req.CheckOutDate || string.IsNullOrWhiteSpace(req.GuestName) || req.RoomIds == null || req.RoomIds.Length < 2 || req.RoomIds.Length > 30 || req.RoomIds.Distinct().Count() != req.RoomIds.Length)
                return Ops.Fail("Đoàn cần 2-30 phòng khác nhau, ngày trả sau ngày nhận");
            if (req.CutoffDate is DateOnly cutoff && cutoff >= req.CheckInDate) return Ops.Fail("Ngày khóa sổ phải trước ngày nhận phòng.");
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            foreach (var roomId in req.RoomIds.OrderBy(x => x))
                if (!await Ops.LockRoom(db, roomId)) return Ops.Conflict("Phòng không tồn tại");
            var rooms = await db.Rooms.Where(x => x.PropertyId == req.PropertyId && req.RoomIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync();
            if (rooms.Count != req.RoomIds.Length || rooms.Any(x => x.IsOutOfOrder || (req.CheckInDate <= DateOnly.FromDateTime(DateTime.Today) && x.HousekeepingStatus != "INSPECTED"))) return Ops.Conflict("Một hoặc nhiều phòng chưa sẵn sàng");
            foreach (var room in rooms) {
                if (await Ops.IsBooked(db, room.Id, req.CheckInDate, req.CheckOutDate)) return Ops.Conflict($"Phòng {room.Number} đã được đặt");
                if (await V5Closeout.ActiveHold(db, room.Id, req.CheckInDate, req.CheckOutDate)) return Ops.Conflict($"Phòng {room.Number} đang được giữ tạm");
            }
            var types = await db.RoomTypes.Where(x => x.PropertyId == req.PropertyId).ToDictionaryAsync(x => x.Id, x => x.BaseRate);
            var guest = new Guest { FullName = req.GuestName.Trim(), Phone = req.Phone?.Trim() ?? "" }; db.Guests.Add(guest); await db.SaveChangesAsync();
            var groupCode = "GR-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            foreach (var room in rooms) db.Reservations.Add(new Reservation { Code = BookingCode(), GroupCode = groupCode,
                PropertyId = req.PropertyId, GuestId = guest.Id, RoomId = room.Id, CheckInDate = req.CheckInDate, CheckOutDate = req.CheckOutDate,
                NightlyRate = types[room.RoomTypeId], Adults = 1, BookingSource = req.BookingSource ?? "DIRECT", Note = req.Note });
            await db.SaveChangesAsync();
            if (req.CutoffDate is DateOnly cut) db.GroupCutoffs.Add(new GroupCutoff { PropertyId = req.PropertyId, Code = groupCode, CutoffDate = cut });
            await db.SaveChangesAsync(); await Ops.Log(db, actor, req.PropertyId, "GROUP_BOOK", "Reservation", 0, groupCode);
            await tx.CommitAsync(); return Results.Ok(new { groupCode, roomCount = rooms.Count });
        });
        // Every physical room in a group has its own actual-occupant list, separated from the booking contact.
        api.MapGet("/groups/{code}/occupants", async (string code, int propertyId, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            var rooms = await (from r in db.Reservations join room in db.Rooms on r.RoomId equals room.Id
                where r.PropertyId == propertyId && r.GroupCode == code orderby room.Number
                select new { r.Id, r.Status, r.RoomId, RoomNumber = room.Number }).ToListAsync();
            if (rooms.Count == 0) return Results.NotFound();
            var ids = rooms.Select(x => x.Id).ToArray();
            var people = await db.Occupants.Where(x => ids.Contains(x.ReservationId)).OrderBy(x => x.Id).ToListAsync();
            return Results.Ok(rooms.Select(room => new { room.Id, room.RoomId, room.RoomNumber, room.Status,
                occupants = people.Where(x => x.ReservationId == room.Id).ToArray() }));
        });
        api.MapGet("/reservations/{id:int}/occupants", async (int id, ClaimsPrincipal actor, PmsDbContext db) => {
            var r = await db.Reservations.FindAsync(id); if (r == null) return Results.NotFound();
            if (!Ops.HasScope(actor, r.PropertyId)) return Results.Forbid();
            return Results.Ok(await db.Occupants.Where(x => x.ReservationId == id).OrderBy(x => x.Id).ToListAsync());
        });
        api.MapPost("/reservations/{id:int}/occupants", async (int id, OccupantInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (string.IsNullOrWhiteSpace(req.FullName) || req.FullName.Trim().Length > 150 ||
                (req.IdentityLast4 != null && req.IdentityLast4.Length > 4)) return Ops.Fail("Họ tên hoặc 4 ký tự cuối giấy tờ không hợp lệ");
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var r = await db.Reservations.FindAsync(id); if (r == null) return Results.NotFound();
            if (!Ops.HasScope(actor, r.PropertyId)) return Results.Forbid();
            if (r.Status != "CONFIRMED" && r.Status != "CHECKED_IN") return Ops.Conflict("Không thể sửa danh sách khách khi đã kết thúc đặt phòng");
            var room = await db.Rooms.FindAsync(r.RoomId); if (room == null) return Results.NotFound();
            var capacity = await db.RoomTypes.Where(x => x.Id == room.RoomTypeId).Select(x => x.Capacity).SingleAsync();
            if (await db.Occupants.CountAsync(x => x.ReservationId == id) >= capacity) return Ops.Conflict("Vượt sức chứa phòng");
            if (req.IsPrimary && await db.Occupants.AnyAsync(x => x.ReservationId == id && x.IsPrimary)) return Ops.Conflict("Phòng đã có khách chính");
            var person = new ReservationOccupant { ReservationId = id, FullName = req.FullName.Trim(), Phone = req.Phone?.Trim() ?? "",
                Nationality = req.Nationality?.Trim() ?? "", IdentityLast4 = req.IdentityLast4?.Trim() ?? "", IsPrimary = req.IsPrimary };
            db.Occupants.Add(person); await db.SaveChangesAsync(); await Ops.Log(db, actor, r.PropertyId, "OCCUPANT_ADD", "ReservationOccupant", person.Id);
            await tx.CommitAsync(); return Results.Ok(person);
        });
        api.MapDelete("/reservations/{id:int}/occupants/{personId:int}", async (int id, int personId, ClaimsPrincipal actor, PmsDbContext db) => {
            var r = await db.Reservations.FindAsync(id); if (r == null) return Results.NotFound();
            if (!Ops.HasScope(actor, r.PropertyId)) return Results.Forbid();
            if (r.Status != "CONFIRMED" && r.Status != "CHECKED_IN") return Ops.Conflict("Đã kết thúc hồ sơ lưu trú");
            var person = await db.Occupants.FirstOrDefaultAsync(x => x.Id == personId && x.ReservationId == id);
            if (person == null) return Results.NotFound();
            db.Occupants.Remove(person); await db.SaveChangesAsync(); await Ops.Log(db, actor, r.PropertyId, "OCCUPANT_DELETE", "ReservationOccupant", personId);
            return Results.Ok(new { personId, removed = true });
        });
        api.MapGet("/groups", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            var list = await db.Reservations.Where(x => x.PropertyId == propertyId && x.GroupCode != null).ToListAsync();
            var cuts = await db.GroupCutoffs.Where(x => x.PropertyId == propertyId).ToDictionaryAsync(x => x.Code, x => x.CutoffDate);
            return Results.Ok(list.GroupBy(x => x.GroupCode).Select(g => new { groupCode = g.Key, rooms = g.Count(),
                confirmed = g.Count(x => x.Status == "CONFIRMED"), inHouse = g.Count(x => x.Status == "CHECKED_IN"), checkedOut = g.Count(x => x.Status == "CHECKED_OUT"),
                cancelled = g.Count(x => x.Status == "CANCELLED"), noShow = g.Count(x => x.Status == "NO_SHOW"),
                cutoffDate = cuts.TryGetValue(g.Key!, out var day) ? day : (DateOnly?)null }).ToList());
        });
        api.MapGet("/groups/{code}/candidates", async (string code, int propertyId, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            var members = await db.Reservations.Where(x => x.PropertyId == propertyId && x.GroupCode == code && x.Status != "CANCELLED").ToListAsync();
            if (members.Count == 0) return Results.NotFound();
            var active = members.Where(x => x.Status is "CONFIRMED" or "CHECKED_IN").ToList();
            var rows = await (from r in db.Reservations join g in db.Guests on r.GuestId equals g.Id join room in db.Rooms on r.RoomId equals room.Id
                where r.PropertyId == propertyId && r.GroupCode == null && (r.Status == "CONFIRMED" || r.Status == "CHECKED_IN")
                orderby room.Number
                select new { r.Id, r.Code, r.Status, r.CheckInDate, r.CheckOutDate, room.Number, g.FullName }).ToListAsync();
            var open = rows.Where(r => active.Any(m => r.CheckInDate < m.CheckOutDate && r.CheckOutDate > m.CheckInDate)).ToList();
            return Results.Ok(open);
        });
        api.MapGet("/groups/{code}/membership", async (string code, int propertyId, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            var rows = await (from e in db.GroupMemberships join actorUser in db.Users on e.CreatedByUserId equals actorUser.Id
                join r in db.Reservations on e.ReservationId equals r.Id join room in db.Rooms on r.RoomId equals room.Id
                where e.PropertyId == propertyId && (e.OldGroupCode == code || e.NewGroupCode == code)
                orderby e.CreatedAtUtc descending
                select new { e.CreatedAtUtc, e.Action, e.OldGroupCode, e.NewGroupCode, r.Code, room.Number, actorUser.FullName }).Take(50).ToListAsync();
            return Results.Ok(rows);
        });
        api.MapPost("/groups/{code}/attach", async (string code, GroupMembershipInput req, ClaimsPrincipal actor, PmsDbContext db) =>
            await ChangeGroupMembership(code, req, actor, db, attach: true));
        api.MapPost("/groups/{code}/detach", async (string code, GroupMembershipInput req, ClaimsPrincipal actor, PmsDbContext db) =>
            await ChangeGroupMembership(code, req, actor, db, attach: false));
        api.MapPost("/groups/{code}/check-in", async (string code, int propertyId, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var rs = await db.Reservations.Where(x => x.PropertyId == propertyId && x.GroupCode == code && x.Status == "CONFIRMED").ToListAsync();
            if (rs.Count == 0) return Ops.Conflict("Không có phòng đoàn chờ nhận");
            var ids = rs.Select(x => x.RoomId).ToArray();
            foreach (var roomId in ids.OrderBy(x => x))
                if (!await Ops.LockRoom(db, roomId)) return Ops.Conflict("Có phòng không tồn tại");
            var rooms = await db.Rooms.Where(x => ids.Contains(x.Id)).ToListAsync();
            if (rooms.Count != rs.Count || rooms.Any(x => x.IsOutOfOrder || x.HousekeepingStatus != "INSPECTED") || await db.Stays.AnyAsync(x => ids.Contains(x.RoomId) && x.Status == "IN_HOUSE")) return Ops.Conflict("Có phòng đoàn chưa sẵn sàng");
            foreach (var r in rs) {
                r.Status = "CHECKED_IN"; db.Stays.Add(new Stay { ReservationId = r.Id, RoomId = r.RoomId });
                var nights = r.CheckOutDate.DayNumber - r.CheckInDate.DayNumber;
                db.Charges.Add(new FolioCharge { ReservationId = r.Id, Category = "ROOM", Description = $"Tiền phòng đoàn {nights} đêm", Quantity = nights, UnitPrice = r.NightlyRate, Amount = nights * r.NightlyRate });
            }
            await db.SaveChangesAsync(); await Ops.Log(db, actor, propertyId, "GROUP_CHECKIN", "Reservation", 0, code); await tx.CommitAsync();
            return Results.Ok(new { groupCode = code, checkedIn = rs.Count });
        });
        api.MapPost("/charges/{id:int}/transfer", async (int id, TransferChargeInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (req.Quantity <= 0) return Ops.Fail("Số lượng phải > 0");
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var source = await db.Charges.FindAsync(id); if (source == null) return Results.NotFound();
            if (source.Category != "SERVICE") return Ops.Conflict("Chỉ chuyển khoản dịch vụ");
            var srcRes = await db.Reservations.FindAsync(source.ReservationId);
            var destStay = await db.Stays.FindAsync(req.TargetStayId);
            if (srcRes == null || destStay == null) return Results.NotFound();
            var destRes = await db.Reservations.FindAsync(destStay.ReservationId);
            if (destRes == null || destRes.PropertyId != srcRes.PropertyId || !Ops.HasScope(actor, srcRes.PropertyId)) return Results.Forbid();
            if (destStay.Status != "IN_HOUSE" || !await db.Stays.AnyAsync(s => s.ReservationId == srcRes.Id && s.Status == "IN_HOUSE") || srcRes.Id == destRes.Id) return Ops.Conflict("Hai hồ sơ cần đang lưu trú và khác nhau");
            if (req.Quantity > source.Quantity) return Ops.Fail("Số lượng chuyển lớn hơn hiện có");
            var sourcePaid = await db.Payments.Where(x => x.ReservationId == srcRes.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
            var sourceTotal = await db.Charges.Where(x => x.ReservationId == srcRes.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
            var moved = req.Quantity * source.UnitPrice;
            if (sourceTotal - moved < sourcePaid) return Ops.Conflict("Không thể chuyển: folio nguồn sẽ âm sau khi đã thanh toán");
            source.Quantity -= req.Quantity; source.Amount = source.UnitPrice * source.Quantity;
            if (source.Quantity == 0) db.Charges.Remove(source);
            var newCharge = new FolioCharge { ReservationId = destRes.Id, ServiceItemId = source.ServiceItemId, Category = "SERVICE", Description = source.Description,
                Quantity = req.Quantity, UnitPrice = source.UnitPrice, Amount = moved }; db.Charges.Add(newCharge);
            await db.SaveChangesAsync(); await Ops.Log(db, actor, srcRes.PropertyId, "CHARGE_TRANSFER", "FolioCharge", newCharge.Id, $"{srcRes.Id} -> {destRes.Id}");
            await tx.CommitAsync(); return Results.Ok(new { movedAmount = moved, destinationChargeId = newCharge.Id });
        });
        api.MapPost("/groups/{code}/pay", async (string code, GroupPayInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            if (req.Amount <= 0 || !Methods.Contains(req.Method) || !Ops.ValidIdempotency(req.IdempotencyKey))
                return Ops.Fail("Cần số tiền, hình thức và idempotencyKey UUID");
            var bookingIds = await db.Reservations.Where(x => x.PropertyId == req.PropertyId && x.GroupCode == code && x.Status == "CHECKED_IN")
                .OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
            if (bookingIds.Count == 0) return Ops.Conflict("Đoàn chưa nhận phòng");
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            foreach (var bookingId in bookingIds) await Ops.LockReservation(db, bookingId);
            var rs = await db.Reservations.Where(x => bookingIds.Contains(x.Id) && x.Status == "CHECKED_IN").OrderBy(x => x.Id).ToListAsync();
            if (rs.Count != bookingIds.Count) return Ops.Conflict("Trạng thái đoàn đã thay đổi");
            var idemPrefix = req.IdempotencyKey + "|";
            var previous = await db.Payments.Where(x => x.IdempotencyKey != null && x.IdempotencyKey.StartsWith(idemPrefix)).ToListAsync();
            if (previous.Count > 0) {
                if (previous.Sum(x => x.Amount) != req.Amount || previous.Any(x => x.Method != req.Method || x.Kind != "GROUP_PAYMENT" || !rs.Any(r => r.Id == x.ReservationId)))
                    return Ops.Conflict("idempotencyKey đã dùng cho lệnh thanh toán khác");
                await tx.CommitAsync(); return Results.Ok(new { groupCode = code, amount = req.Amount, count = rs.Count, replay = true });
            }
            var shiftId = await GetShiftId(db, actor, req.PropertyId, req.Method);
            if (req.Method == "CASH" && shiftId == null) return Ops.Conflict("Cần mở ca thu ngân");
            var remain = req.Amount;
            foreach (var r in rs) {
                var total = await db.Charges.Where(x => x.ReservationId == r.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
                var paid = await db.Payments.Where(x => x.ReservationId == r.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
                var applied = Math.Min(remain, Math.Max(0, total - paid));
                if (applied > 0) { db.Payments.Add(new Payment { ReservationId = r.Id, ShiftId = shiftId, Method = req.Method, Amount = applied, Kind = "GROUP_PAYMENT", Reference = code, IdempotencyKey = $"{req.IdempotencyKey}|{r.Id}" }); remain -= applied; }
            }
            if (remain > 0) return Ops.Conflict("Số tiền vượt tổng dư nợ của các phòng đoàn");
            await db.SaveChangesAsync(); await Ops.Log(db, actor, req.PropertyId, "GROUP_PAYMENT", "Reservation", 0, code); await tx.CommitAsync();
            return Results.Ok(new { groupCode = code, amount = req.Amount, count = rs.Count });
        });
        api.MapPost("/groups/{code}/checkout", async (string code, int propertyId, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var rs = await db.Reservations.Where(x => x.GroupCode == code && x.PropertyId == propertyId && x.Status == "CHECKED_IN").ToListAsync();
            if (rs.Count == 0) return Ops.Conflict("Không có phòng đoàn đang ở");
            foreach (var r in rs) {
                var total = await db.Charges.Where(x => x.ReservationId == r.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
                var paid = await db.Payments.Where(x => x.ReservationId == r.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
                if (total != paid) return Ops.Conflict($"Phòng thuộc booking {r.Code} chưa thanh toán đủ");
            }
            var ids = rs.Select(r => r.Id).ToArray(); var stays = await db.Stays.Where(x => ids.Contains(x.ReservationId) && x.Status == "IN_HOUSE").ToListAsync();
            foreach (var r in rs) r.Status = "CHECKED_OUT";
            foreach (var s in stays) { s.Status = "CHECKED_OUT"; s.CheckedOutAtUtc = DateTime.UtcNow; }
            var stayRoomIds = stays.Select(s => s.RoomId).ToArray();
            var rooms = await db.Rooms.Where(x => stayRoomIds.Contains(x.Id)).ToListAsync(); foreach (var room in rooms) room.HousekeepingStatus = "DIRTY";
            await db.SaveChangesAsync(); await Ops.Log(db, actor, propertyId, "GROUP_CHECKOUT", "Reservation", 0, code); await tx.CommitAsync();
            return Results.Ok(new { groupCode = code, checkedOut = stays.Count });
        });

        api.MapGet("/checkouts", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            return Results.Ok(await (from stay in db.Stays join r in db.Reservations on stay.ReservationId equals r.Id
                join g in db.Guests on r.GuestId equals g.Id join room in db.Rooms on stay.RoomId equals room.Id
                where r.PropertyId == propertyId && stay.Status == "CHECKED_OUT"
                orderby stay.CheckedOutAtUtc descending
                select new { stay.Id, stay.CheckedOutAtUtc, roomNumber = room.Number, g.FullName, r.Code }).Take(50).ToListAsync());
        });

        // RECEIVABLES: debt checkout, debt collection, guarded same-day restore.
        api.MapGet("/debts", async (int propertyId, DateOnly? from, DateOnly? to, string? status, string? source, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            var rangeError = Ops.DateWindow(from, to, out var start, out var end);
            if (rangeError != null) return Ops.Fail(rangeError);
            var ranged = from.HasValue;
            var wantedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
            var wantedSource = string.IsNullOrWhiteSpace(source) ? null : source.Trim().ToUpperInvariant();
            var stayRows = await (from d in db.Debts
                where d.PropertyId == propertyId && d.ReservationId != null
                join r in db.Reservations on d.ReservationId equals r.Id
                join g in db.Guests on r.GuestId equals g.Id
                select new { d.Id, d.ReservationId, d.VirtualFolioId, Code = r.Code, g.FullName, g.Phone, d.OriginalAmount, d.OutstandingAmount, d.Status, d.Origin, d.Note, d.CreatedAtUtc, Source = r.BookingSource }).ToListAsync();
            var outside = await (from d in db.Debts
                where d.PropertyId == propertyId && d.VirtualFolioId != null
                join f in db.VirtualFolios on d.VirtualFolioId equals f.Id
                select new { d.Id, d.ReservationId, d.VirtualFolioId, Code = f.Code, FullName = f.GuestName, Phone = f.Phone, d.OriginalAmount, d.OutstandingAmount, d.Status, d.Origin, d.Note, d.CreatedAtUtc, Source = "NGOAI" }).ToListAsync();
            var rows = stayRows.Concat(outside)
                .Where(x => !ranged || x.Status == "OPEN" || (x.CreatedAtUtc >= start && x.CreatedAtUtc < end))
                .Where(x => wantedStatus == null || x.Status == wantedStatus)
                .Where(x => wantedSource == null || x.Source == wantedSource)
                .OrderByDescending(x => x.Id).Take(ranged ? 500 : 200);
            return Results.Ok(rows);
        });
        api.MapGet("/debts/export", async (int propertyId, DateOnly? from, DateOnly? to, string? status, string? source, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            var rangeError = Ops.DateWindow(from, to, out var start, out var end);
            if (rangeError != null) return Ops.Fail(rangeError);
            var ranged = from.HasValue;
            var wantedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
            var wantedSource = string.IsNullOrWhiteSpace(source) ? null : source.Trim().ToUpperInvariant();
            var stayRows = await (from d in db.Debts
                where d.PropertyId == propertyId && d.ReservationId != null
                join r in db.Reservations on d.ReservationId equals r.Id
                join g in db.Guests on r.GuestId equals g.Id
                select new { Code = r.Code, Name = g.FullName, Phone = g.Phone ?? "", d.OriginalAmount, d.OutstandingAmount, d.Status, d.Origin, Source = r.BookingSource, d.CreatedAtUtc }).ToListAsync();
            var outside = await (from d in db.Debts
                where d.PropertyId == propertyId && d.VirtualFolioId != null
                join f in db.VirtualFolios on d.VirtualFolioId equals f.Id
                select new { Code = f.Code, Name = f.GuestName, Phone = f.Phone ?? "", d.OriginalAmount, d.OutstandingAmount, d.Status, d.Origin, Source = "NGOAI", d.CreatedAtUtc }).ToListAsync();
            var rows = stayRows.Concat(outside).Where(x => !ranged || x.Status == "OPEN" || (x.CreatedAtUtc >= start && x.CreatedAtUtc < end))
                .Where(x => wantedStatus == null || x.Status == wantedStatus).Where(x => wantedSource == null || x.Source == wantedSource);
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Ma,Khach,Dien thoai,Nguon,Loai no,No goc,Con thieu,Trang thai,Ngay");
            foreach (var row in rows)
                csv.AppendLine($"{row.Code},{row.Name},{row.Phone},{row.Source},{row.Origin},{row.OriginalAmount},{row.OutstandingAmount},{row.Status},{row.CreatedAtUtc:yyyy-MM-dd}");
            return Results.Text("\uFEFF" + csv, "text/csv; charset=utf-8");
        });
        api.MapPost("/stays/{stayId:int}/checkout-debt", async (int stayId, CheckoutDebtInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var stay = await db.Stays.FindAsync(stayId); if (stay == null) return Results.NotFound();
            var r = await db.Reservations.FindAsync(stay.ReservationId); if (r == null) return Results.NotFound();
            if (!Ops.HasScope(actor, r.PropertyId)) return Results.Forbid();
            if (stay.Status != "IN_HOUSE") return Ops.Conflict("Phòng không còn khách");
            var charges = await db.Charges.Where(x => x.ReservationId == r.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
            var paid = await db.Payments.Where(x => x.ReservationId == r.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
            if (charges <= paid) return Ops.Conflict("Chỉ dùng chức năng này khi còn công nợ; trường hợp đủ tiền hãy checkout thường");
            var debt = await db.Debts.FirstOrDefaultAsync(x => x.ReservationId == r.Id);
            if (debt != null && debt.Status != "REVERSED") return Ops.Conflict("Đã có hồ sơ công nợ");
            if (debt == null) { debt = new DebtReceivable { PropertyId = r.PropertyId, ReservationId = r.Id }; db.Debts.Add(debt); }
            debt.OriginalAmount = charges - paid; debt.OutstandingAmount = charges - paid;
            debt.Status = "OPEN"; debt.Note = req.Note; debt.CreatedAtUtc = DateTime.UtcNow; debt.SettledAtUtc = null;
            stay.Status = "CHECKED_OUT"; stay.CheckedOutAtUtc = DateTime.UtcNow; r.Status = "CHECKED_OUT";
            var room = await db.Rooms.FindAsync(stay.RoomId); if (room != null) room.HousekeepingStatus = "DIRTY";
            await db.SaveChangesAsync(); await Ops.Log(db, actor, r.PropertyId, "CHECKOUT_DEBT", "DebtReceivable", debt.Id);
            await tx.CommitAsync(); return Results.Ok(debt);
        });
        api.MapPost("/debts/{id:int}/collect", async (int id, PaymentInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var d = await db.Debts.FindAsync(id); if (d == null) return Results.NotFound(); if (!Ops.HasScope(actor, d.PropertyId)) return Results.Forbid();
            if (!Ops.ValidIdempotency(req.IdempotencyKey)) return Ops.Fail("Cần idempotencyKey UUID khi thu công nợ");
            if (d.Origin == "CITY_LEDGER" || d.ReservationId == null)
            {
                if (await db.AuditEvents.AnyAsync(x => x.Action == "DEBT_COLLECT" && x.EntityId == d.Id && x.Details == req.IdempotencyKey))
                { await tx.CommitAsync(); return Results.Ok(d); }
                if (d.Status != "OPEN" || req.Amount <= 0 || req.Amount > d.OutstandingAmount || !Methods.Contains(req.Method) || req.Method == "CITY_LEDGER")
                    return Ops.Fail("Công nợ không mở hoặc số tiền/phương thức không hợp lệ");
                var cityShift = await GetShiftId(db, actor, d.PropertyId, req.Method);
                if (req.Method == "CASH" && cityShift == null) return Ops.Conflict("Cần mở ca thu ngân");
                db.Vouchers.Add(new CashVoucher { PropertyId = d.PropertyId, Code = "VC-" + Guid.NewGuid().ToString("N")[..9].ToUpperInvariant(), Type = "RECEIPT", Method = req.Method, Amount = req.Amount, Category = "CITY_LEDGER", Description = "Thu nợ City Ledger", ActorUserId = Ops.UserId(actor), ShiftId = cityShift });
                d.OutstandingAmount -= req.Amount; if (d.OutstandingAmount == 0) { d.Status = "SETTLED"; d.SettledAtUtc = DateTime.UtcNow; }
                await db.SaveChangesAsync(); await Ops.Log(db, actor, d.PropertyId, "DEBT_COLLECT", "DebtReceivable", d.Id, req.IdempotencyKey!);
                await tx.CommitAsync(); return Results.Ok(d);
            }
            await Ops.LockReservation(db, d.ReservationId.Value);
            var previous = await db.Payments.FirstOrDefaultAsync(x => x.IdempotencyKey == req.IdempotencyKey);
            if (previous != null) {
                if (previous.Kind != "DEBT" || previous.ReservationId != d.ReservationId || previous.Amount != req.Amount || previous.Method != req.Method)
                    return Ops.Conflict("idempotencyKey đã dùng cho giao dịch khác");
                await tx.CommitAsync(); return Results.Ok(d);
            }
            await db.Entry(d).ReloadAsync();
            if (d.Status != "OPEN" || req.Amount <= 0 || req.Amount > d.OutstandingAmount || !Methods.Contains(req.Method)) return Ops.Fail("Công nợ không mở hoặc số tiền/phương thức không hợp lệ");
            var shiftId = await GetShiftId(db, actor, d.PropertyId, req.Method);
            if (req.Method == "CASH" && shiftId == null) return Ops.Conflict("Cần mở ca thu ngân");
            db.Payments.Add(new Payment { ReservationId = d.ReservationId.Value, ShiftId = shiftId, Amount = req.Amount, Method = req.Method, Kind = "DEBT", Reference = req.Reference, IdempotencyKey = req.IdempotencyKey });
            d.OutstandingAmount -= req.Amount; if (d.OutstandingAmount == 0) { d.Status = "SETTLED"; d.SettledAtUtc = DateTime.UtcNow; }
            await db.SaveChangesAsync(); await Ops.Log(db, actor, d.PropertyId, "DEBT_COLLECT", "DebtReceivable", d.Id, $"{req.Amount:0}");
            await tx.CommitAsync(); return Results.Ok(d);
        });
        api.MapPost("/stays/{id:int}/restore", async (int id, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.CanManage(actor)) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var stay = await db.Stays.FindAsync(id); if (stay == null) return Results.NotFound();
            var r = await db.Reservations.FindAsync(stay.ReservationId); if (r == null) return Results.NotFound();
            if (!Ops.HasScope(actor, r.PropertyId)) return Results.Forbid();
            if (stay.Status != "CHECKED_OUT" || !stay.CheckedOutAtUtc.HasValue || stay.CheckedOutAtUtc.Value.Date != DateTime.UtcNow.Date) return Ops.Conflict("Chỉ khôi phục trong cùng ngày UTC của bản demo");
            var room = await db.Rooms.FindAsync(stay.RoomId);
            if (room == null || room.IsOutOfOrder) return Ops.Conflict("Phòng không tồn tại hoặc đang bảo trì");
            if (await db.Stays.AnyAsync(x => x.RoomId == stay.RoomId && x.Id != stay.Id && x.Status == "IN_HOUSE")) return Ops.Conflict("Phòng đã có khách khác");
            if (await db.Reservations.AnyAsync(x => x.Id != r.Id && x.RoomId == stay.RoomId &&
                    (x.Status == "CONFIRMED" || x.Status == "CHECKED_IN") &&
                    x.CheckInDate < r.CheckOutDate && x.CheckOutDate > r.CheckInDate))
                return Ops.Conflict("Đã có booking mới chồng lịch, không thể khôi phục checkout");
            var debt = await db.Debts.FirstOrDefaultAsync(x => x.ReservationId == r.Id);
            if (debt != null && debt.OutstandingAmount != debt.OriginalAmount) return Ops.Conflict("Công nợ đã được thu một phần hoặc toàn bộ, không thể khôi phục");
            if (debt != null) { debt.Status = "REVERSED"; debt.OutstandingAmount = 0; }
            stay.Status = "IN_HOUSE"; stay.CheckedOutAtUtc = null; r.Status = "CHECKED_IN";
            await db.SaveChangesAsync(); await Ops.Log(db, actor, r.PropertyId, "RESTORE_CHECKOUT", "Stay", id);
            await tx.CommitAsync(); return Results.Ok(new { stay.Id, stay.Status });
        });

        // INVENTORY: suppliers, signed stock ledger, out-of-room retail sales.
        api.MapGet("/suppliers", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) =>
            Ops.HasScope(actor, propertyId) ? (IResult)Results.Ok(await db.Suppliers.Where(x => x.PropertyId == propertyId).OrderBy(x => x.Name).ToListAsync()) : Results.Forbid());
        api.MapPost("/suppliers", async (SupplierInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name)) return Ops.Fail("Thiếu mã/tên nhà cung cấp");
            if (await db.Suppliers.AnyAsync(x => x.PropertyId == req.PropertyId && x.Code == req.Code.Trim())) return Ops.Conflict("Trùng mã nhà cung cấp");
            var supplier = new Supplier { PropertyId = req.PropertyId, Code = req.Code.Trim(), Name = req.Name.Trim(), Phone = req.Phone ?? "", Address = req.Address };
            db.Suppliers.Add(supplier); await db.SaveChangesAsync(); await Ops.Log(db, actor, req.PropertyId, "SUPPLIER_CREATE", "Supplier", supplier.Id); return Results.Ok(supplier);
        });
        api.MapGet("/stock/movements", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            return Results.Ok(await (from m in db.StockMovements join svc in db.Services on m.ServiceItemId equals svc.Id
                where m.PropertyId == propertyId orderby m.Id descending select new { m.Id, m.Type, m.QuantityChange, m.BalanceAfter, m.UnitCost,
                    m.Reference, m.SupplierId, m.CreatedAtUtc, serviceName = svc.Name, serviceCode = svc.Code }).Take(300).ToListAsync());
        });
        api.MapPost("/stock/movements", async (StockMoveInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            if (!new[] { "IN", "OUT", "RETURN", "ADJUST" }.Contains(req.Type) || req.QuantityChange == 0 || Math.Abs((long)req.QuantityChange) > 100000 || req.UnitCost < 0) return Ops.Fail("Loại hoặc số lượng nhập/xuất không hợp lệ");
            if ((req.Type == "IN" || req.Type == "RETURN") && req.QuantityChange < 0 || req.Type == "OUT" && req.QuantityChange > 0) return Ops.Fail("Chiều thay đổi tồn kho không đúng loại phiếu");
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (!await Ops.LockService(db, req.ServiceItemId)) return Ops.Fail("Hàng hóa không tồn tại");
            var svc = await db.Services.FirstOrDefaultAsync(x => x.Id == req.ServiceItemId && x.PropertyId == req.PropertyId && x.TrackInventory);
            if (svc == null) return Ops.Fail("Hàng hóa không theo dõi tồn kho");
            if (req.SupplierId.HasValue && !await db.Suppliers.AnyAsync(x => x.Id == req.SupplierId && x.PropertyId == req.PropertyId)) return Ops.Fail("Nhà cung cấp không thuộc cơ sở");
            if (svc.StockQuantity + req.QuantityChange < 0) return Ops.Conflict("Không đủ tồn kho");
            svc.StockQuantity += req.QuantityChange;
            var movement = new StockMovement { PropertyId = req.PropertyId, ServiceItemId = svc.Id, Type = req.Type, QuantityChange = req.QuantityChange,
                BalanceAfter = svc.StockQuantity, UnitCost = req.UnitCost, SupplierId = req.SupplierId, Reference = req.Reference, ActorUserId = Ops.UserId(actor) };
            db.StockMovements.Add(movement); await db.SaveChangesAsync(); await Ops.Log(db, actor, req.PropertyId, "STOCK_" + req.Type, "StockMovement", (int)movement.Id);
            await tx.CommitAsync(); return Results.Ok(movement);
        });
        api.MapGet("/retail-sales", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) =>
            Ops.HasScope(actor, propertyId) ? (IResult)Results.Ok(await db.RetailSales.Where(x => x.PropertyId == propertyId).OrderByDescending(x => x.Id).Take(100).ToListAsync()) : Results.Forbid());
        api.MapPost("/retail-sales", async (RetailSaleInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            if (req.Quantity < 1 || req.Quantity > 500 || !Methods.Contains(req.PaymentMethod)) return Ops.Fail("Số lượng hoặc phương thức không hợp lệ");
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (!await Ops.LockService(db, req.ServiceItemId)) return Ops.Fail("Hàng hóa không tồn tại");
            var svc = await db.Services.FirstOrDefaultAsync(x => x.Id == req.ServiceItemId && x.PropertyId == req.PropertyId && x.IsActive);
            if (svc == null) return Ops.Fail("Hàng hóa không tồn tại");
            if (svc.TrackInventory && svc.StockQuantity < req.Quantity) return Ops.Conflict("Thiếu hàng tồn kho");
            var shift = await GetShiftId(db, actor, req.PropertyId, req.PaymentMethod);
            if (req.PaymentMethod == "CASH" && shift == null) return Ops.Conflict("Cần mở ca thu ngân để bán thu tiền mặt");
            if (svc.TrackInventory) svc.StockQuantity -= req.Quantity;
            var sale = new RetailSale { PropertyId = req.PropertyId, ServiceItemId = svc.Id, Quantity = req.Quantity, UnitPrice = svc.Price,
                TotalAmount = svc.Price * req.Quantity, PaymentMethod = req.PaymentMethod, ShiftId = shift, ActorUserId = Ops.UserId(actor) };
            db.RetailSales.Add(sale); await db.SaveChangesAsync();
            if (svc.TrackInventory) db.StockMovements.Add(new StockMovement { PropertyId = req.PropertyId, ServiceItemId = svc.Id,
                Type = "SALE", QuantityChange = -req.Quantity, BalanceAfter = svc.StockQuantity, Reference = $"SALE-{sale.Id}", ActorUserId = Ops.UserId(actor) });
            await db.SaveChangesAsync(); await Ops.Log(db, actor, req.PropertyId, "RETAIL_SALE", "RetailSale", sale.Id);
            await tx.CommitAsync(); return Results.Ok(sale);
        });
        // BOOKKEEPING: generic external receipts and expense vouchers (not double-counted as folio payments).
        api.MapGet("/vouchers", async (int propertyId, DateOnly? from, DateOnly? to, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            var rangeError = Ops.DateWindow(from, to, out var start, out var end);
            if (rangeError != null) return Ops.Fail(rangeError);
            var vouchers = db.Vouchers.Where(x => x.PropertyId == propertyId);
            if (from.HasValue) vouchers = vouchers.Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end);
            return Results.Ok(await vouchers.OrderByDescending(x => x.Id).Take(from.HasValue ? 500 : 200).ToListAsync());
        });
        api.MapPost("/vouchers", async (VoucherInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.CanManageFinance(actor) || !Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            if (!new[] { "RECEIPT", "EXPENSE" }.Contains(req.Type) || !Methods.Contains(req.Method) || req.Amount <= 0 || req.Amount > 10000000000) return Ops.Fail("Phiếu thu/chi không hợp lệ");
            var shiftId = await GetShiftId(db, actor, req.PropertyId, req.Method);
            if (req.Method == "CASH" && shiftId == null) return Ops.Conflict("Cần mở ca để ghi phiếu tiền mặt");
            var voucher = new CashVoucher { PropertyId = req.PropertyId, Code = "VC-" + Guid.NewGuid().ToString("N")[..9].ToUpperInvariant(),
                Type = req.Type, Method = req.Method, Amount = req.Amount, Category = req.Category ?? "OTHER", Description = req.Description ?? "",
                ActorUserId = Ops.UserId(actor), ShiftId = shiftId };
            db.Vouchers.Add(voucher); await db.SaveChangesAsync(); await Ops.Log(db, actor, req.PropertyId, "VOUCHER_" + req.Type, "CashVoucher", voucher.Id); return Results.Ok(voucher);
        });
        api.MapGet("/guest-assets", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) =>
            Ops.HasScope(actor, propertyId) ? (IResult)Results.Ok(await db.GuestAssets.Where(x => x.PropertyId == propertyId).OrderByDescending(x => x.Id).Take(150).ToListAsync()) : Results.Forbid());
        api.MapPost("/guest-assets", async (GuestAssetInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(req.Description) || req.Quantity < 1) return Ops.Fail("Mô tả tài sản và số lượng phải hợp lệ");
            var stay = await db.Stays.FindAsync(req.StayId); if (stay == null) return Results.NotFound();
            if (stay.Status != "IN_HOUSE" || !await db.Reservations.AnyAsync(x => x.Id == stay.ReservationId && x.PropertyId == req.PropertyId)) return Ops.Conflict("Lưu trú không thuộc cơ sở hoặc đã checkout");
            var a = new GuestAsset { PropertyId = req.PropertyId, StayId = req.StayId, Description = req.Description.Trim(), Quantity = req.Quantity, Note = req.Note };
            db.GuestAssets.Add(a); await db.SaveChangesAsync(); await Ops.Log(db, actor, req.PropertyId, "ASSET_RECEIVED", "GuestAsset", a.Id); return Results.Ok(a);
        });
        api.MapPost("/guest-assets/{id:int}/return", async (int id, ClaimsPrincipal actor, PmsDbContext db) => {
            var a = await db.GuestAssets.FindAsync(id); if (a == null) return Results.NotFound(); if (!Ops.HasScope(actor, a.PropertyId)) return Results.Forbid();
            if (a.ReturnedAtUtc.HasValue) return Ops.Conflict("Tài sản đã trả"); a.ReturnedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(); await Ops.Log(db, actor, a.PropertyId, "ASSET_RETURNED", "GuestAsset", id); return Results.Ok(a);
        });

        // MANAGEMENT REPORTS: by property and chain; snapshot only, NOT audited night posting.
        api.MapGet("/reports/chain", async (DateOnly from, DateOnly to, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.CanManageFinance(actor)) return Results.Forbid(); if (from > to || to.DayNumber - from.DayNumber > 366) return Ops.Fail("Chỉ xem báo cáo tối đa 366 ngày");
            var pid = Ops.UserProperty(actor);
            var properties = await db.Properties.Where(x => x.IsActive && (!pid.HasValue || x.Id == pid)).ToListAsync();
            var ids = properties.Select(p => p.Id).ToArray(); var all = new List<object>();
            foreach (var prop in properties) {
                var span = to.DayNumber - from.DayNumber + 1;
                var today = V5Closeout.PropertyToday(prop.TimeZoneId);
                var resIds = db.Reservations.Where(r => r.PropertyId == prop.Id).Select(r => r.Id);
                var start = from.ToDateTime(TimeOnly.MinValue).ToUniversalTime(); var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
                decimal roomRevenue; decimal serviceRevenue; decimal folioPayments; decimal expense;
                if (span <= 31)
                {
                    await DeskScale.EnsureSnapshots(db, prop, from, to, today);
                    var snaps = await db.PropertyDaySnapshots.Where(x => x.PropertyId == prop.Id && x.BusinessDate >= from && x.BusinessDate <= to && x.BusinessDate < today).ToListAsync();
                    roomRevenue = snaps.Sum(x => x.RoomRevenue);
                    serviceRevenue = snaps.Sum(x => x.ServiceRevenue);
                    folioPayments = snaps.Sum(x => x.Payments);
                    expense = snaps.Sum(x => x.Expenses);
                    if (from <= today && today <= to)
                    {
                        var liveStart = today.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
                        var liveEnd = today.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
                        var live = await db.Charges.Where(x => resIds.Contains(x.ReservationId) && x.PostedAtUtc >= liveStart && x.PostedAtUtc < liveEnd)
                            .GroupBy(x => x.Category).Select(g => new { g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync();
                        roomRevenue += live.Where(x => x.Key == "ROOM").Sum(x => x.Amount);
                        serviceRevenue += live.Where(x => x.Key == "SERVICE").Sum(x => x.Amount);
                        folioPayments += await db.Payments.Where(x => resIds.Contains(x.ReservationId) && x.PaidAtUtc >= liveStart && x.PaidAtUtc < liveEnd).SumAsync(x => (decimal?)x.Amount) ?? 0;
                        expense += await db.Vouchers.Where(x => x.PropertyId == prop.Id && x.Type == "EXPENSE" && x.CreatedAtUtc >= liveStart && x.CreatedAtUtc < liveEnd).SumAsync(x => (decimal?)x.Amount) ?? 0;
                    }
                }
                else
                {
                    var c = await db.Charges.Where(x => resIds.Contains(x.ReservationId) && x.PostedAtUtc >= start && x.PostedAtUtc < end)
                        .GroupBy(x => x.Category).Select(g => new { g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync();
                    roomRevenue = c.Where(x => x.Key == "ROOM").Sum(x => x.Amount);
                    serviceRevenue = c.Where(x => x.Key == "SERVICE").Sum(x => x.Amount);
                    folioPayments = await db.Payments.Where(x => resIds.Contains(x.ReservationId) && x.PaidAtUtc >= start && x.PaidAtUtc < end).SumAsync(x => (decimal?)x.Amount) ?? 0;
                    expense = await db.Vouchers.Where(x => x.PropertyId == prop.Id && x.Type == "EXPENSE" && x.CreatedAtUtc >= start && x.CreatedAtUtc < end).SumAsync(x => (decimal?)x.Amount) ?? 0;
                }
                var retail = await db.RetailSales.Where(x => x.PropertyId == prop.Id && x.SoldAtUtc >= start && x.SoldAtUtc < end).SumAsync(x => (decimal?)x.TotalAmount) ?? 0;
                var otherReceipt = await db.Vouchers.Where(x => x.PropertyId == prop.Id && x.Type == "RECEIPT" && x.CreatedAtUtc >= start && x.CreatedAtUtc < end).SumAsync(x => (decimal?)x.Amount) ?? 0;
                var debt = await db.Debts.Where(x => x.PropertyId == prop.Id && x.Status == "OPEN").SumAsync(x => (decimal?)x.OutstandingAmount) ?? 0;
                var rooms = await db.Rooms.CountAsync(x => x.PropertyId == prop.Id && !x.IsOutOfOrder);
                var inHouse = await db.Reservations.CountAsync(x => x.PropertyId == prop.Id && x.Status == "CHECKED_IN");
                var reservedDates = await db.Reservations.Where(x => x.PropertyId == prop.Id && (x.Status == "CHECKED_IN" || x.Status == "CHECKED_OUT")
                    && x.CheckInDate <= to && x.CheckOutDate > from).Select(x => new { x.CheckInDate, x.CheckOutDate }).ToListAsync();
                var roomNights = reservedDates.Sum(x => Math.Max(0, Math.Min(x.CheckOutDate.DayNumber, to.DayNumber + 1) - Math.Max(x.CheckInDate.DayNumber, from.DayNumber)));
                var availableNights = rooms * (to.DayNumber - from.DayNumber + 1);
                var approxOccupancy = availableNights > 0 ? Math.Round(100m * roomNights / availableNights, 1) : 0m;
                var adr = roomNights > 0 ? Math.Round(roomRevenue / roomNights, 0) : 0m;
                var revpar = availableNights > 0 ? Math.Round(roomRevenue / availableNights, 0) : 0m;
                all.Add(new { propertyId = prop.Id, propertyCode = prop.Code, propertyName = prop.Name, rooms, inHouse, soldRoomNights = roomNights, approxOccupancyPercent = approxOccupancy, adr, revpar,
                    roomRevenue, serviceRevenue, retailRevenue = retail, folioPayments, expense, otherReceipt, outstandingDebt = debt });
            }
            return Results.Ok(new { from, to, properties = all,
                note = to.DayNumber - from.DayNumber + 1 <= 31
                    ? "Ngày đã qua trong khoảng 31 ngày đọc một dòng mỗi cơ sở mỗi ngày. Hôm nay vẫn tính trực tiếp. Doanh thu theo ngày ghi charge, không phân bổ đêm."
                    : "Khoảng dài hơn 31 ngày tính trực tiếp. Doanh thu theo ngày ghi charge, không phân bổ đêm." });
        });
        api.MapGet("/reports/agencies", async (int propertyId, DateOnly from, DateOnly to, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, propertyId)) return Results.Forbid();
            if (from > to || to.DayNumber - from.DayNumber > 366) return Ops.Fail("Khoảng ngày không hợp lệ");
            var agents = await db.Agencies.Where(x => x.PropertyId == propertyId).ToListAsync();
            var start = from.ToDateTime(TimeOnly.MinValue).ToUniversalTime(); var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            var rs = await db.Reservations.Where(x => x.PropertyId == propertyId && x.AgencyId != null).ToListAsync();
            var ids = rs.Select(x => x.Id).ToArray();
            var charges = await db.Charges.Where(x => ids.Contains(x.ReservationId) && x.Category == "ROOM" && x.PostedAtUtc >= start && x.PostedAtUtc < end).ToListAsync();
            var output = agents.Select(a => {
                var agIds = rs.Where(r => r.AgencyId == a.Id).Select(r => r.Id).ToHashSet();
                var revenue = charges.Where(c => agIds.Contains(c.ReservationId)).Sum(c => c.Amount);
                return new { a.Id, a.Code, a.Name, a.CommissionPercent, bookingCount = agIds.Count, roomChargeRevenue = revenue,
                    estimatedCommission = Math.Round(revenue * a.CommissionPercent / 100m, 0) };
            }).ToList();
            return Results.Ok(new { from, to, agencies = output, note = "Hoa hồng ước tính từ tiền phòng ghi sổ trong kỳ, chưa đối soát hợp đồng/đại lý." });
        });
        api.MapGet("/shifts/{id:int}/reconcile", async (int id, ClaimsPrincipal actor, PmsDbContext db) => {
            var shift = await db.Shifts.FindAsync(id); if (shift == null) return Results.NotFound();
            if (!Ops.CanManageFinance(actor) || !Ops.HasScope(actor, shift.PropertyId)) return Results.Forbid();
            var received = await db.Payments.Where(x => x.ShiftId == id && x.Method == "CASH").SumAsync(x => (decimal?)x.Amount) ?? 0;
            var retail = await db.RetailSales.Where(x => x.ShiftId == id && x.PaymentMethod == "CASH").SumAsync(x => (decimal?)x.TotalAmount) ?? 0;
            var outside = await db.VirtualFolioPayments.Where(x => x.ShiftId == id && x.Method == "CASH").SumAsync(x => (decimal?)x.Amount) ?? 0;
            var vouchers = await db.Vouchers.Where(x => x.ShiftId == id && x.Method == "CASH").ToListAsync();
            var receipt = vouchers.Where(x => x.Type == "RECEIPT").Sum(x => x.Amount);
            var expense = vouchers.Where(x => x.Type == "EXPENSE").Sum(x => x.Amount);
            var expected = shift.OpeningAmount + received + retail + outside + receipt - expense;
            return Results.Ok(new { shift.Id, shift.PropertyId, shift.UserId, shift.Status, shift.OpeningAmount, roomCash = received,
                retailCash = retail + outside, otherCashReceipt = receipt, cashExpense = expense, expectedClosing = expected,
                actualClosing = shift.ClosingAmount, variance = shift.ClosingAmount.HasValue ? shift.ClosingAmount - expected : (decimal?)null });
        });
        api.MapGet("/reports/stock", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            return Results.Ok(await db.Services.Where(x => x.PropertyId == propertyId && x.TrackInventory).Select(x => new { x.Code, x.Name, x.StockQuantity, x.Unit, x.Price }).OrderBy(x => x.Code).ToListAsync());
        });
        api.MapGet("/reports/stock-card", async (int propertyId, DateOnly from, DateOnly to, string? name, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            if (from > to || to.DayNumber - from.DayNumber > 366) return Ops.Fail("Khoảng ngày không hợp lệ");
            var property = await db.Properties.Where(x => x.Id == propertyId)
                .Select(x => new { x.Code, x.Name, x.Address, x.City, x.Phone, x.Fax, x.Email, x.Website, x.TimeZoneId }).FirstOrDefaultAsync();
            if (property == null) return Results.NotFound();
            TimeZoneInfo zone;
            try { zone = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(property.TimeZoneId) ? "Asia/Ho_Chi_Minh" : property.TimeZoneId); }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { zone = TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "VN", "VN"); }
            var start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone);
            var end = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone);
            var services = await db.Services.Where(x => x.PropertyId == propertyId && x.TrackInventory).OrderBy(x => x.Name).ToListAsync();
            if (!string.IsNullOrWhiteSpace(name))
                services = services.Where(x => x.Name.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            var ids = services.Select(x => x.Id).ToArray();
            var movements = await db.StockMovements.Where(x => x.PropertyId == propertyId && ids.Contains(x.ServiceItemId))
                .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).ToListAsync();
            var lines = services.Select(svc => {
                var mine = movements.Where(x => x.ServiceItemId == svc.Id).ToList();
                var before = mine.LastOrDefault(x => x.CreatedAtUtc < start);
                var during = mine.Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end).ToList();
                var opening = before != null ? before.BalanceAfter
                    : during.Count > 0 ? during[0].BalanceAfter - during[0].QuantityChange
                    : mine.Count > 0 ? mine[0].BalanceAfter - mine[0].QuantityChange
                    : svc.StockQuantity;
                var inbound = during.Where(x => x.QuantityChange > 0).Sum(x => x.QuantityChange);
                var outbound = during.Where(x => x.QuantityChange < 0).Sum(x => -x.QuantityChange);
                var note = string.Join("; ", during.Select(x => x.Reference).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(3));
                return new { code = svc.Code, name = svc.Name, unit = svc.Unit, opening, inbound, outbound, closing = opening + inbound - outbound, note };
            }).ToList();
            return Results.Ok(new { property, from, to, serviceName = string.IsNullOrWhiteSpace(name) ? "<<Tất cả>>" : name.Trim(), groupName = "<<Tất cả>>", lines });
        });
        api.MapGet("/reports/cash-ledger", async (int propertyId, DateOnly from, DateOnly to, string? kind, string? method, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            if (from > to || to.DayNumber - from.DayNumber > 366) return Ops.Fail("Khoảng ngày không hợp lệ");
            var mode = string.IsNullOrWhiteSpace(kind) ? "COLLECT" : kind.Trim().ToUpperInvariant();
            if (mode is not ("COLLECT" or "DEPOSIT" or "REFUND" or "SERVICE" or "DEBT" or "HISTORY")) return Ops.Fail("Loại báo cáo không hợp lệ");
            var payMethod = string.IsNullOrWhiteSpace(method) ? null : method.Trim().ToUpperInvariant();
            if (payMethod != null && !Ops.PayMethods.Contains(payMethod)) return Ops.Fail("Phương thức thanh toán không hợp lệ");
            var property = await db.Properties.Where(x => x.Id == propertyId)
                .Select(x => new { x.Code, x.Name, x.Address, x.City, x.Phone, x.Fax, x.Email, x.Website, x.TimeZoneId }).FirstOrDefaultAsync();
            if (property == null) return Results.NotFound();
            TimeZoneInfo zone;
            try { zone = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(property.TimeZoneId) ? "Asia/Ho_Chi_Minh" : property.TimeZoneId); }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { zone = TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "VN", "VN"); }
            var start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone);
            var end = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone);
            string Stamp(DateTime utc) {
                var value = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
                return TimeZoneInfo.ConvertTimeFromUtc(value, zone).ToString("dd/MM/yyyy HH:mm");
            }
            var title = mode switch { "DEPOSIT" => "BÁO CÁO TẠM ỨNG", "REFUND" => "BÁO CÁO TRẢ LẠI", "SERVICE" => "BÁO CÁO DỊCH VỤ ĐÃ CHECKOUT", "DEBT" => "CHI TIẾT CÔNG NỢ", "HISTORY" => "LỊCH SỬ THANH TOÁN CÔNG NỢ", _ => "BÁO CÁO THU TIỀN" };
            var methodLabel = payMethod switch { "CASH" => "Tiền mặt", "TRANSFER" => "Chuyển khoản", "CARD" => "Thẻ", "MOMO" => "MoMo", "VNPAY" => "VNPay", _ => "<<Tất cả>>" };
            if (mode == "SERVICE") {
                var charges = await (from c in db.Charges
                    join r in db.Reservations on c.ReservationId equals r.Id
                    join g in db.Guests on r.GuestId equals g.Id
                    join room in db.Rooms on r.RoomId equals room.Id
                    where r.PropertyId == propertyId && r.Status == "CHECKED_OUT" && c.Category == "SERVICE" && c.PostedAtUtc >= start && c.PostedAtUtc < end
                    orderby c.PostedAtUtc
                    select new { c.PostedAtUtc, r.Code, Room = room.Number, g.FullName, c.Description, c.Quantity, c.Amount }).ToListAsync();
                var lines = charges.Select(x => new { at = Stamp(x.PostedAtUtc), booking = x.Code, room = x.Room, guest = x.FullName, detail = x.Description, quantity = x.Quantity, amount = x.Amount }).ToList();
                return Results.Ok(new { property, from, to, mode, title, methodLabel = "<<Tất cả>>", total = lines.Sum(x => x.amount), lines });
            }
            if (mode == "DEBT") {
                var debts = await (from d in db.Debts
                    join r in db.Reservations on d.ReservationId equals r.Id
                    join g in db.Guests on r.GuestId equals g.Id
                    join room in db.Rooms on r.RoomId equals room.Id
                    where d.PropertyId == propertyId && d.CreatedAtUtc >= start && d.CreatedAtUtc < end
                    orderby d.CreatedAtUtc
                    select new { d.CreatedAtUtc, r.Code, Room = room.Number, g.FullName, g.Phone, d.OriginalAmount, d.OutstandingAmount, d.Status, d.Note, r.GroupCode }).ToListAsync();
                var debtLines = debts.Select(x => new {
                    at = Stamp(x.CreatedAtUtc), booking = x.Code, room = x.Room, guest = x.FullName, phone = x.Phone,
                    original = x.OriginalAmount, outstanding = x.OutstandingAmount,
                    status = x.Status switch { "OPEN" => "Còn nợ", "SETTLED" => "Đã thu", "REVERSED" => "Đã hủy", _ => x.Status },
                    note = x.Note ?? "", party = string.IsNullOrWhiteSpace(x.GroupCode) ? "Khách lẻ" : "Khách đoàn"
                }).ToList();
                return Results.Ok(new { property, from, to, mode, title, methodLabel = "<<Tất cả>>", total = debtLines.Sum(x => x.outstanding), lines = debtLines });
            }
            var wanted = mode switch { "DEPOSIT" => new[] { "DEPOSIT" }, "REFUND" => new[] { "REFUND" }, "HISTORY" => new[] { "DEBT" }, _ => new[] { "PAYMENT", "GROUP_PAYMENT", "DEBT" } };
            var payments = await (from p in db.Payments
                join r in db.Reservations on p.ReservationId equals r.Id
                join g in db.Guests on r.GuestId equals g.Id
                join room in db.Rooms on r.RoomId equals room.Id
                where r.PropertyId == propertyId && wanted.Contains(p.Kind) && p.PaidAtUtc >= start && p.PaidAtUtc < end && (payMethod == null || p.Method == payMethod)
                orderby p.PaidAtUtc
                select new { p.PaidAtUtc, r.Code, Room = room.Number, g.FullName, p.Method, p.Kind, p.Amount, p.Reference }).ToListAsync();
            var money = payments.Select(x => new {
                at = Stamp(x.PaidAtUtc), booking = x.Code, room = x.Room, guest = x.FullName,
                method = x.Method switch { "CASH" => "Tiền mặt", "TRANSFER" => "Chuyển khoản", "CARD" => "Thẻ", "MOMO" => "MoMo", "VNPAY" => "VNPay", _ => x.Method },
                amount = x.Amount, note = x.Reference ?? ""
            }).ToList();
            return Results.Ok(new { property, from, to, mode, title, methodLabel, total = money.Sum(x => x.amount), lines = money });
        });
        api.MapGet("/reports/receipts", async (int propertyId, DateOnly from, DateOnly to, string? name, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            if (from > to || to.DayNumber - from.DayNumber > 366) return Ops.Fail("Khoảng ngày không hợp lệ");
            var property = await db.Properties.Where(x => x.Id == propertyId)
                .Select(x => new { x.Code, x.Name, x.Address, x.City, x.Phone, x.Fax, x.Email, x.Website }).FirstOrDefaultAsync();
            if (property == null) return Results.NotFound();
            var needle = name?.Trim();
            var stays = await (from r in db.Reservations
                join g in db.Guests on r.GuestId equals g.Id
                join room in db.Rooms on r.RoomId equals room.Id
                where r.PropertyId == propertyId && r.Status != "CANCELLED" && r.CheckOutDate >= @from && r.CheckOutDate <= to
                orderby r.CheckOutDate, room.Number
                select new { r.Id, r.Code, r.CheckInDate, r.CheckOutDate, r.Adults, r.Children, r.BookingMode, r.BookingSource, r.Note, r.AgencyId, Room = room.Number, g.FullName }).ToListAsync();
            if (!string.IsNullOrWhiteSpace(needle))
                stays = stays.Where(x => x.FullName.Contains(needle, StringComparison.OrdinalIgnoreCase) || x.Code.Contains(needle, StringComparison.OrdinalIgnoreCase) || x.Room.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();
            var ids = stays.Select(x => x.Id).ToArray();
            var charges = await db.Charges.Where(x => ids.Contains(x.ReservationId)).Select(x => new { x.ReservationId, x.Amount }).ToListAsync();
            var payments = await db.Payments.Where(x => ids.Contains(x.ReservationId)).Select(x => new { x.ReservationId, x.Kind, x.Method, x.Amount }).ToListAsync();
            var agencyIds = stays.Where(x => x.AgencyId != null).Select(x => x.AgencyId!.Value).Distinct().ToArray();
            var agencies = await db.Agencies.Where(x => agencyIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync();
            string PayName(string method) => method switch { "CASH" => "Tiền mặt", "TRANSFER" => "Chuyển khoản", "CARD" => "Thẻ", "MOMO" => "MoMo", "VNPAY" => "VNPay", _ => method };
            string StayName(string mode) => mode switch { "HOURLY" => "Nghỉ giờ", "DAY_USE" => "Trong ngày", _ => "Ngày đêm" };
            var lines = stays.Select(x => {
                var mineCharges = charges.Where(c => c.ReservationId == x.Id).ToList();
                var minePays = payments.Where(p => p.ReservationId == x.Id).ToList();
                var gross = mineCharges.Where(c => c.Amount > 0).Sum(c => c.Amount);
                var discount = mineCharges.Where(c => c.Amount < 0).Sum(c => -c.Amount);
                var deposit = minePays.Where(p => p.Kind == "DEPOSIT").Sum(p => p.Amount);
                var paid = minePays.Where(p => p.Kind != "DEPOSIT" && p.Amount > 0).Sum(p => p.Amount);
                var due = gross - discount - minePays.Sum(p => p.Amount);
                var methods = string.Join(", ", minePays.Select(p => PayName(p.Method)).Distinct());
                return new {
                    checkIn = x.CheckInDate.ToString("dd/MM/yyyy"), checkOut = x.CheckOutDate.ToString("dd/MM/yyyy"),
                    booking = x.Code, room = x.Room, guests = x.Adults + x.Children, guest = x.FullName,
                    total = gross, deposit, discount, paid, due, method = methods, stay = StayName(x.BookingMode),
                    note = x.Note ?? "", channel = agencies.FirstOrDefault(a => a.Id == x.AgencyId)?.Name ?? x.BookingSource
                };
            }).ToList();
            return Results.Ok(new { property, from, to, guestName = string.IsNullOrWhiteSpace(needle) ? "<<Tất cả>>" : needle, title = "DANH SÁCH PHIẾU THANH TOÁN", totalDue = lines.Sum(x => x.due), lines });
        });
        api.MapGet("/reports/registration-csv", async (int propertyId, DateOnly from, DateOnly to, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            if (from > to || to.DayNumber - from.DayNumber > 366) return Ops.Fail("Khoảng ngày không hợp lệ");
            var data = await (from r in db.Reservations join g in db.Guests on r.GuestId equals g.Id
                join room in db.Rooms on r.RoomId equals room.Id where r.PropertyId == propertyId && r.CheckInDate >= @from && r.CheckInDate <= to && r.Status != "CANCELLED"
                orderby r.CheckInDate select new { r.Code, room.Number, g.FullName, g.Phone, g.Nationality, g.IdentityLast4, r.CheckInDate, r.CheckOutDate, r.Status }).ToListAsync();
            var csv = new StringBuilder("Booking,Room,Guest,Phone,Nationality,IdentityLast4,CheckIn,CheckOut,Status\r\n");
            foreach (var r in data) csv.AppendLine(string.Join(",", new[] { r.Code, r.Number, r.FullName, r.Phone, r.Nationality, r.IdentityLast4, r.CheckInDate.ToString("yyyy-MM-dd"), r.CheckOutDate.ToString("yyyy-MM-dd"), r.Status }.Select(Csv)));
            return Results.Text("\uFEFF" + csv.ToString(), "text/csv; charset=utf-8");
        });
        api.MapGet("/daily-closes", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) =>
            Ops.HasScope(actor, propertyId) ? (IResult)Results.Ok(await db.DailyCloses.Where(x => x.PropertyId == propertyId).OrderByDescending(x => x.BusinessDate).Take(90).ToListAsync()) : Results.Forbid());
        api.MapPost("/daily-closes", async (DailyCloseInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.CanManageFinance(actor) || !Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            if (req.BusinessDate > DateOnly.FromDateTime(DateTime.UtcNow)) return Ops.Fail("Không được chốt ngày tương lai");
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (await db.DailyCloses.AnyAsync(x => x.PropertyId == req.PropertyId && x.BusinessDate == req.BusinessDate)) return Ops.Conflict("Ngày này đã có snapshot");
            var resIds = db.Reservations.Where(x => x.PropertyId == req.PropertyId).Select(x => x.Id);
            var start = req.BusinessDate.ToDateTime(TimeOnly.MinValue).ToUniversalTime(); var end = start.AddDays(1);
            var charges = await db.Charges.Where(x => resIds.Contains(x.ReservationId) && x.PostedAtUtc >= start && x.PostedAtUtc < end).ToListAsync();
            var payments = await db.Payments.Where(x => resIds.Contains(x.ReservationId) && x.PaidAtUtc >= start && x.PaidAtUtc < end).ToListAsync();
            var close = new DailyClose { PropertyId = req.PropertyId, BusinessDate = req.BusinessDate, RoomCharges = charges.Where(x => x.Category == "ROOM").Sum(x => x.Amount),
                ServiceCharges = charges.Where(x => x.Category == "SERVICE").Sum(x => x.Amount), ReceivedAmount = payments.Sum(x => x.Amount), ActorUserId = Ops.UserId(actor) };
            db.DailyCloses.Add(close); await db.SaveChangesAsync(); await Ops.Log(db, actor, req.PropertyId, "DAILY_SNAPSHOT", "DailyClose", close.Id);
            await tx.CommitAsync(); return Results.Ok(close);
        });
    }

    private static async Task<IResult> ChangeGroupMembership(string code, GroupMembershipInput req, ClaimsPrincipal actor, PmsDbContext db, bool attach) {
        if (!Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
        code = code.Trim();
        if (code.Length is 0 or > 30) return Ops.Fail("Mã đoàn không hợp lệ");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var memberIds = await db.Reservations.Where(x => x.PropertyId == req.PropertyId && x.GroupCode == code && x.Status != "CANCELLED")
            .OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        if (memberIds.Count == 0) return Results.NotFound();
        if (!await db.Reservations.AnyAsync(x => x.Id == req.ReservationId && x.PropertyId == req.PropertyId)) return Results.NotFound();
        foreach (var id in memberIds.Append(req.ReservationId).Distinct().OrderBy(x => x))
            await Ops.LockReservation(db, id);
        var booking = await db.Reservations.FirstAsync(x => x.Id == req.ReservationId && x.PropertyId == req.PropertyId);
        if (booking.Status is not ("CONFIRMED" or "CHECKED_IN")) return Ops.Conflict("Chỉ nhập hoặc tách phòng đang chờ nhận hoặc đang ở");
        var members = await db.Reservations.Where(x => x.PropertyId == req.PropertyId && x.GroupCode == code && x.Status != "CANCELLED").ToListAsync();
        if (attach) {
            if (booking.GroupCode == code) return Ops.Conflict("Phòng đã thuộc đoàn này");
            if (!string.IsNullOrWhiteSpace(booking.GroupCode)) return Ops.Conflict("Phòng đang thuộc đoàn khác, hãy tách trước");
            if (members.Count >= 30) return Ops.Conflict("Đoàn đã đủ 30 phòng");
            var active = members.Where(x => x.Status is "CONFIRMED" or "CHECKED_IN").ToList();
            if (active.Count == 0) return Ops.Conflict("Đoàn không còn phòng đang hiệu lực");
            if (!active.Any(m => booking.CheckInDate < m.CheckOutDate && booking.CheckOutDate > m.CheckInDate))
                return Ops.Fail("Ngày ở không trùng khoảng ngày của đoàn");
            booking.GroupCode = code;
        } else {
            if (booking.GroupCode != code) return Ops.Conflict("Phòng không thuộc đoàn này");
            booking.GroupCode = null;
        }
        db.GroupMemberships.Add(new GroupMembershipEvent {
            PropertyId = req.PropertyId, ReservationId = booking.Id, Action = attach ? "ATTACH" : "DETACH",
            OldGroupCode = attach ? null : code, NewGroupCode = attach ? code : null, CreatedByUserId = Ops.UserId(actor)
        });
        await db.SaveChangesAsync();
        await Ops.Log(db, actor, req.PropertyId, attach ? "GROUP_ATTACH" : "GROUP_DETACH", "Reservation", booking.Id, code);
        await tx.CommitAsync();
        return Results.Ok(new { groupCode = code, reservationId = booking.Id, booking.Code, attached = attach });
    }
    private static string BookingCode() => "BK-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    private static readonly string[] Methods = Ops.PayMethods;
    private static async Task<int?> GetShiftId(PmsDbContext db, ClaimsPrincipal actor, int propertyId, string method)
        => method != "CASH" ? null : await db.Shifts.Where(x => x.PropertyId == propertyId && x.UserId == Ops.UserId(actor) && x.Status == "OPEN").Select(x => (int?)x.Id).FirstOrDefaultAsync();
    private static string Csv(string? s) {
        var value = (s ?? "").Replace("\r", " ").Replace("\n", " ");
        if (value.Length > 0 && "=+@-".Contains(value[0])) value = "'" + value; // avoid spreadsheet formula injection
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}

public record BuildingInput(int PropertyId, string Code, string Name);
public record RoomBuildingInput(int? BuildingId);
public record RatePlanInput(int PropertyId, string Code, string Name, string Mode, decimal BasePrice, int IncludedHours, decimal ExtraHourPrice);
public record WaitlistInput(int PropertyId, string GuestName, string? Phone, DateOnly CheckInDate, DateOnly CheckOutDate, int? RoomTypeId, string? Note);
public record AssignWaitlistInput(int RoomId);
public record GroupBookingInput(int PropertyId, string GuestName, string? Phone, DateOnly CheckInDate, DateOnly CheckOutDate, int[] RoomIds, string? BookingSource, string? Note, DateOnly? CutoffDate = null);
public record GroupMembershipInput(int PropertyId, int ReservationId);
public record TransferChargeInput(int TargetStayId, int Quantity);
public record GroupPayInput(int PropertyId, decimal Amount, string Method, string? IdempotencyKey = null);
public record OccupantInput(string FullName, string? Phone, string? Nationality, string? IdentityLast4, bool IsPrimary);
public record CheckoutDebtInput(string? Note);
public record SupplierInput(int PropertyId, string Code, string Name, string? Phone, string? Address);
public record StockMoveInput(int PropertyId, int ServiceItemId, string Type, int QuantityChange, decimal UnitCost, int? SupplierId, string? Reference);
public record RetailSaleInput(int PropertyId, int ServiceItemId, int Quantity, string PaymentMethod);
public record VoucherInput(int PropertyId, string Type, string Method, string? Category, string? Description, decimal Amount);
public record GuestAssetInput(int PropertyId, int StayId, string Description, int Quantity, string? Note);
public record DailyCloseInput(int PropertyId, DateOnly BusinessDate);
