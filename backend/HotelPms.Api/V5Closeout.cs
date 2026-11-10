using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace HotelPms.Api;

public static class V5Closeout
{
    static readonly (string Code, string Name)[] NoteCatalog =
    [
        ("BED_PREFERENCE", "Yêu cầu loại giường"),
        ("ARRIVAL_TIME", "Giờ dự kiến đến"),
        ("SPECIAL_REQUEST", "Yêu cầu đặc biệt"),
        ("PAYMENT_GUARANTEE", "Bảo lãnh thanh toán"),
        ("OTHER", "Nội dung khác")
    ];
    static readonly (string Code, string Label)[] CleaningChecks =
    [
        ("BED", "Giường và ga"),
        ("BATH", "Nhà vệ sinh"),
        ("MINIBAR", "Đối chiếu minibar"),
        ("TRASH", "Rác và mùi phòng")
    ];

    public static void MapV5Closeout(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.AddEndpointFilter(Ops.DepartmentAccess);

        api.MapGet("/booking-note-types", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            await EnsureNoteTypes(db, propertyId);
            var rows = await db.BookingNoteTypes.Where(x => x.PropertyId == null || x.PropertyId == propertyId)
                .OrderBy(x => x.SortOrder).ToListAsync();
            return Results.Ok(rows);
        });
        api.MapPost("/booking-note-types", async (NoteTypeInput req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            var code = req.Code.Trim().ToUpperInvariant();
            if (code.Length is < 2 or > 40 || string.IsNullOrWhiteSpace(req.Name)) return Ops.Fail("Mã và tên loại ghi chú không hợp lệ");
            if (await db.BookingNoteTypes.AnyAsync(x => x.PropertyId == req.PropertyId && x.Code == code)) return Ops.Conflict("Mã loại ghi chú đã có");
            var row = new BookingNoteType { PropertyId = req.PropertyId, Code = code, Name = req.Name.Trim(), IsRequired = req.IsRequired, AllowFreeText = true, SortOrder = req.SortOrder };
            db.BookingNoteTypes.Add(row);
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, req.PropertyId, "NOTE_TYPE_CREATE", "BookingNoteType", row.Id, code);
            return Results.Ok(row);
        });
        api.MapPatch("/booking-note-types/{id:int}", async (int id, NoteTypeRequiredInput req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.CanManage(actor)) return Results.Forbid();
            var row = await db.BookingNoteTypes.FindAsync(id);
            if (row == null) return Results.NotFound();
            if (row.PropertyId is int propertyId && !Ops.HasScope(actor, propertyId)) return Results.Forbid();
            row.IsRequired = req.IsRequired;
            await db.SaveChangesAsync();
            if (row.PropertyId is int loggedPropertyId)
                await Ops.Log(db, actor, loggedPropertyId, "NOTE_TYPE_REQUIRED", "BookingNoteType", row.Id, req.IsRequired ? "REQUIRED" : "OPTIONAL");
            return Results.Ok(new { row.Id, row.IsRequired });
        });
        api.MapGet("/reservations/{id:int}/notes", async (int id, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            var reservation = await db.Reservations.FindAsync(id);
            if (reservation == null) return Results.NotFound();
            if (!Ops.HasScope(actor, reservation.PropertyId)) return Results.Forbid();
            var rows = await (from n in db.ReservationNotes
                              join t in db.BookingNoteTypes on n.NoteTypeId equals t.Id
                              join u in db.Users on n.CreatedByUserId equals u.Id
                              where n.ReservationId == id
                              orderby n.Id descending
                              select new { n.Id, n.NoteTypeId, t.Code, t.Name, n.NoteText, n.Version, n.IsCurrent, n.CreatedAtUtc, u.FullName }).Take(80).ToListAsync();
            return Results.Ok(rows);
        });
        api.MapPost("/reservations/{id:int}/notes", async (int id, ReservationNoteInput req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var reservation = await db.Reservations.FindAsync(id);
            if (reservation == null) return Results.NotFound();
            if (!Ops.HasScope(actor, reservation.PropertyId)) return Results.Forbid();
            var type = await db.BookingNoteTypes.FirstOrDefaultAsync(x => x.Id == req.NoteTypeId && x.IsActive && (x.PropertyId == null || x.PropertyId == reservation.PropertyId));
            if (type == null) return Ops.Fail("Loại ghi chú không còn hiệu lực");
            var text = (req.NoteText ?? "").Trim();
            if (type.AllowFreeText && text.Length is < 1 or > 500) return Ops.Fail("Nội dung ghi chú từ 1 đến 500 ký tự");
            var previous = await db.ReservationNotes.Where(x => x.ReservationId == id && x.NoteTypeId == type.Id && x.IsCurrent).ToListAsync();
            foreach (var old in previous) old.IsCurrent = false;
            var note = new ReservationNote
            {
                ReservationId = id, NoteTypeId = type.Id, NoteText = text,
                Version = previous.Count == 0 ? 1 : previous.Max(x => x.Version) + 1,
                CreatedByUserId = Ops.UserId(actor)
            };
            db.ReservationNotes.Add(note);
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, reservation.PropertyId, "RESERVATION_NOTE", "ReservationNote", note.Id, type.Code);
            await tx.CommitAsync();
            return Results.Ok(new { note.Id, note.Version, type.Code });
        });

        api.MapPatch("/reservations/{id:int}/deposit-terms", async (int id, DepositTermsInput req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            var reservation = await db.Reservations.FindAsync(id);
            if (reservation == null) return Results.NotFound();
            if (!Ops.HasScope(actor, reservation.PropertyId)) return Results.Forbid();
            if (reservation.Status is not ("CONFIRMED" or "CHECKED_IN")) return Ops.Conflict("Chỉ đặt hạn cọc cho phiếu chờ nhận hoặc đang ở");
            if (req.Amount < 0) return Ops.Fail("Số phải cọc không âm");
            reservation.DepositRequiredAmount = req.Amount;
            reservation.DepositDueAtUtc = req.DueAtUtc;
            reservation.DepositPolicyCode = Ops.TrimOrNull(req.PolicyCode);
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, reservation.PropertyId, "DEPOSIT_TERMS", "Reservation", id, req.Amount.ToString("0.##"));
            return Results.Ok(new { reservation.Id, reservation.DepositRequiredAmount, reservation.DepositDueAtUtc, reservation.Status, cancelled = false });
        });
        api.MapGet("/deposits/warnings", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            return Results.Ok(await DepositWarnings(db, propertyId));
        });
        api.MapPost("/deposits/reminders", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            var warnings = await DepositWarnings(db, propertyId);
            var logged = 0;
            foreach (var row in warnings)
            {
                var key = $"{row.ReservationId}:{row.Milestone}:IN_APP";
                if (await db.NotificationDispatches.AnyAsync(x => x.IdempotencyKey == key)) continue;
                db.NotificationDispatches.Add(new NotificationDispatch
                {
                    PropertyId = propertyId, ReservationId = row.ReservationId, NotificationType = "DEPOSIT_DUE",
                    Milestone = row.Milestone, Channel = "IN_APP", Status = "LOGGED", ScheduledAtUtc = DateTime.UtcNow,
                    SentAtUtc = DateTime.UtcNow, IdempotencyKey = key
                });
                logged++;
            }
            await db.SaveChangesAsync();
            if (logged > 0) await Ops.Log(db, actor, propertyId, "DEPOSIT_REMIND", "Property", propertyId, logged.ToString());
            return Results.Ok(new { logged, warnings, cancelledBookings = 0 });
        });

        api.MapGet("/reservation-holds", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            await ExpireHolds(db, propertyId);
            var now = DateTime.UtcNow;
            var rows = await (from h in db.ReservationHolds
                              join room in db.Rooms on h.RoomId equals room.Id
                              where h.PropertyId == propertyId
                              orderby h.Id descending
                              select new { h.Id, h.RoomId, room.Number, h.GuestName, h.GuestPhone, h.CheckInDate, h.CheckOutDate, h.Status, h.ExpiresAtUtc, h.ConvertedReservationId, secondsLeft = h.Status == "ACTIVE" ? (int)Math.Max(0, (h.ExpiresAtUtc - now).TotalSeconds) : 0 }).Take(50).ToListAsync();
            return Results.Ok(new { serverNowUtc = now, holds = rows });
        });
        api.MapPost("/reservation-holds", async (HoldInput req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            if (req.CheckInDate >= req.CheckOutDate || string.IsNullOrWhiteSpace(req.GuestName)) return Ops.Fail("Tên khách và khoảng ngày phải hợp lệ");
            if (req.HoldMinutes is < 15 or > 240) return Ops.Fail("Thời gian giữ từ 15 đến 240 phút");
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await ExpireHolds(db, req.PropertyId);
            if (!string.IsNullOrWhiteSpace(req.IdempotencyKey))
            {
                var replay = await db.ReservationHolds.FirstOrDefaultAsync(x => x.IdempotencyKey == req.IdempotencyKey);
                if (replay != null) { await tx.CommitAsync(); return Results.Ok(replay); }
            }
            if (!await Ops.LockRoom(db, req.RoomId)) return Ops.Conflict("Phòng không tồn tại");
            var room = await db.Rooms.FirstOrDefaultAsync(x => x.Id == req.RoomId && x.PropertyId == req.PropertyId);
            if (room == null || room.IsOutOfOrder) return Ops.Conflict("Phòng không giữ được");
            if (await Ops.IsBooked(db, room.Id, req.CheckInDate, req.CheckOutDate)) return Ops.Conflict("Phòng đã có phiếu trùng ngày");
            if (await ActiveHold(db, room.Id, req.CheckInDate, req.CheckOutDate)) return Ops.Conflict("Phòng đang có giữ chỗ khác");
            var hold = new ReservationHold
            {
                PropertyId = req.PropertyId, RoomId = room.Id, GuestName = req.GuestName.Trim(), GuestPhone = Ops.TrimOrNull(req.GuestPhone),
                CheckInDate = req.CheckInDate, CheckOutDate = req.CheckOutDate, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(req.HoldMinutes),
                CreatedByUserId = Ops.UserId(actor), IdempotencyKey = Ops.TrimOrNull(req.IdempotencyKey)
            };
            db.ReservationHolds.Add(hold);
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, req.PropertyId, "HOLD_CREATE", "ReservationHold", hold.Id, room.Number);
            await tx.CommitAsync();
            return Results.Ok(hold);
        });
        api.MapPost("/reservation-holds/{id:int}/release", async (int id, ReleaseInput req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            var hold = await db.ReservationHolds.FindAsync(id);
            if (hold == null) return Results.NotFound();
            if (!Ops.HasScope(actor, hold.PropertyId)) return Results.Forbid();
            if (hold.Status != "ACTIVE") return Ops.Conflict("Giữ chỗ không còn hiệu lực");
            hold.Status = "RELEASED"; hold.ReleasedAtUtc = DateTime.UtcNow; hold.ReleaseReason = Ops.TrimOrNull(req.Reason) ?? "Lễ tân nhả phòng";
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, hold.PropertyId, "HOLD_RELEASE", "ReservationHold", id);
            return Results.Ok(hold);
        });
        api.MapPost("/reservation-holds/{id:int}/convert", async (int id, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var hold = await db.ReservationHolds.FindAsync(id);
            if (hold == null) return Results.NotFound();
            if (!Ops.HasScope(actor, hold.PropertyId)) return Results.Forbid();
            if (hold.Status != "ACTIVE" || hold.ExpiresAtUtc <= DateTime.UtcNow) return Ops.Conflict("Giữ chỗ đã hết hạn");
            if (!await Ops.LockRoom(db, hold.RoomId)) return Ops.Conflict("Phòng không tồn tại");
            if (await Ops.IsBooked(db, hold.RoomId, hold.CheckInDate, hold.CheckOutDate)) return Ops.Conflict("Phòng vừa có phiếu khác");
            var room = await db.Rooms.FirstAsync(x => x.Id == hold.RoomId);
            var type = await db.RoomTypes.FirstAsync(x => x.Id == room.RoomTypeId);
            var (lines, roomUnit) = RoomCardPricing.Build(type, null, "OVERNIGHT", hold.CheckInDate, hold.CheckOutDate, 1, 0, null, null);
            var guest = new Guest { FullName = hold.GuestName, Phone = hold.GuestPhone ?? "" };
            db.Guests.Add(guest);
            await db.SaveChangesAsync();
            var reservation = new Reservation
            {
                Code = "BK-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), PropertyId = hold.PropertyId, RoomId = hold.RoomId,
                GuestId = guest.Id, CheckInDate = hold.CheckInDate, CheckOutDate = hold.CheckOutDate, Adults = 1, NightlyRate = roomUnit,
                Status = "CONFIRMED", BookingSource = "DIRECT", BookingMode = "OVERNIGHT",
                RatePlanSnapshot = RoomCardPricing.Serialize("ROOM_CARD", "OVERNIGHT", lines, type, null, null),
                DepositRequiredAmount = lines.Sum(x => x.Amount), DepositDueAtUtc = hold.ExpiresAtUtc, DepositPolicyCode = "HOLD_CONVERT"
            };
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync();
            hold.Status = "CONVERTED"; hold.ConvertedReservationId = reservation.Id; hold.ReleasedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, hold.PropertyId, "HOLD_CONVERT", "Reservation", reservation.Id, reservation.Code);
            await tx.CommitAsync();
            return Results.Ok(new { reservation.Id, reservation.Code, reservation.Status });
        });

        api.MapPost("/housekeeping-tasks/{id:int}/submit", async (int id, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            var task = await db.HousekeepingTasks.FindAsync(id);
            if (task == null) return Results.NotFound();
            if (!Ops.HasScope(actor, task.PropertyId)) return Results.Forbid();
            await EnsureChecklist(db, task);
            var open = await db.HousekeepingTaskItems.Where(x => x.TaskId == id && x.IsRequired && !x.IsDone).AnyAsync();
            if (open) return Ops.Fail("Còn hạng mục bắt buộc chưa xong");
            task.Status = "PENDING"; task.FinishedAtUtc = DateTime.UtcNow;
            var room = await db.Rooms.FindAsync(task.RoomId);
            if (room != null && room.HousekeepingStatus == "DIRTY") room.HousekeepingStatus = "CLEAN";
            await db.SaveChangesAsync();
            return Results.Ok(task);
        });
        api.MapPost("/housekeeping-tasks/{id:int}/approve", async (int id, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.CanManage(actor)) return Ops.Fail("Chỉ quản lý nghiệm thu phòng");
            var task = await db.HousekeepingTasks.FindAsync(id);
            if (task == null) return Results.NotFound();
            if (!Ops.HasScope(actor, task.PropertyId)) return Results.Forbid();
            if (await db.HousekeepingTaskItems.AnyAsync(x => x.TaskId == id && x.IsRequired && !x.IsDone)) return Ops.Fail("Còn hạng mục bắt buộc chưa xong");
            if (!await db.HousekeepingTaskMedia.AnyAsync(x => x.TaskId == id)) return Ops.Fail("Cần ảnh nghiệm thu trước khi trả phòng về sẵn sàng");
            task.Status = "FINISH"; task.InspectorName = actor.FindFirstValue(ClaimTypes.Name); task.FinishedAtUtc ??= DateTime.UtcNow;
            var room = await db.Rooms.FindAsync(task.RoomId);
            if (room != null) room.HousekeepingStatus = "INSPECTED";
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, task.PropertyId, "HK_APPROVE", "HousekeepingTask", id);
            return Results.Ok(new { task.Id, task.Status, room?.HousekeepingStatus });
        });
        api.MapPost("/housekeeping-tasks/{id:int}/reject", async (int id, ReleaseInput req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.CanManage(actor)) return Ops.Fail("Chỉ quản lý trả phiếu dọn");
            var task = await db.HousekeepingTasks.FindAsync(id);
            if (task == null) return Results.NotFound();
            if (!Ops.HasScope(actor, task.PropertyId)) return Results.Forbid();
            var reason = Ops.TrimOrNull(req.Reason);
            if (reason == null) return Ops.Fail("Cần lý do trả phiếu");
            task.Status = "REJECTED"; task.Note = reason;
            var room = await db.Rooms.FindAsync(task.RoomId);
            if (room != null) room.HousekeepingStatus = "DIRTY";
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, task.PropertyId, "HK_REJECT", "HousekeepingTask", id, reason);
            return Results.Ok(task);
        });
        api.MapPatch("/housekeeping-task-items/{id:int}", async (int id, ChecklistInput req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            var item = await db.HousekeepingTaskItems.FindAsync(id);
            if (item == null) return Results.NotFound();
            var task = await db.HousekeepingTasks.FindAsync(item.TaskId);
            if (task == null || !Ops.HasScope(actor, task.PropertyId)) return Results.Forbid();
            item.IsDone = req.IsDone; item.Note = Ops.TrimOrNull(req.Note);
            await db.SaveChangesAsync();
            return Results.Ok(item);
        });
        api.MapPost("/housekeeping-tasks/{id:int}/media", async (int id, IFormFile file, ClaimsPrincipal actor, PmsDbContext db, IWebHostEnvironment env) =>
        {
            var task = await db.HousekeepingTasks.FindAsync(id);
            if (task == null) return Results.NotFound();
            if (!Ops.HasScope(actor, task.PropertyId)) return Results.Forbid();
            if (file == null || file.Length is <= 0 or > 2_000_000) return Ops.Fail("Ảnh từ 1 byte đến 2 MB");
            if (file.ContentType is not ("image/jpeg" or "image/png" or "image/webp")) return Ops.Fail("Chỉ nhận ảnh JPEG, PNG hoặc WebP");
            var ext = file.ContentType == "image/png" ? ".png" : file.ContentType == "image/webp" ? ".webp" : ".jpg";
            var key = $"hk/{id}/{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(env.ContentRootPath, "hk-media", key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var stream = File.Create(path)) await file.CopyToAsync(stream);
            var media = new HousekeepingTaskMedia { TaskId = id, ObjectKey = key, ContentType = file.ContentType, FileSize = file.Length, UploadedByUserId = Ops.UserId(actor) };
            db.HousekeepingTaskMedia.Add(media);
            await db.SaveChangesAsync();
            return Results.Ok(new { media.Id, media.ObjectKey, media.FileSize });
        }).DisableAntiforgery();
        api.MapGet("/housekeeping-tasks/{taskId:int}/media/{mediaId:int}", async (int taskId, int mediaId, ClaimsPrincipal actor, PmsDbContext db, IWebHostEnvironment env) =>
        {
            var media = await db.HousekeepingTaskMedia.FirstOrDefaultAsync(x => x.Id == mediaId && x.TaskId == taskId);
            if (media == null) return Results.NotFound();
            var task = await db.HousekeepingTasks.FindAsync(taskId);
            if (task == null || !Ops.HasScope(actor, task.PropertyId)) return Results.Forbid();
            var path = Path.Combine(env.ContentRootPath, "hk-media", media.ObjectKey.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return Results.NotFound();
            return Results.File(path, media.ContentType);
        });

        api.MapGet("/night-audit", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            return Results.Ok(await db.NightAuditLogs.Where(x => x.PropertyId == propertyId).OrderByDescending(x => x.BusinessDate).Take(30).ToListAsync());
        });
        api.MapPost("/night-audit/run", async (NightRunInput req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.CanManageFinance(actor) || !Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var existing = await db.NightAuditLogs.FirstOrDefaultAsync(x => x.PropertyId == req.PropertyId && x.BusinessDate == req.BusinessDate);
            if (existing?.Status == "LOCKED") return Ops.Conflict("Ngày này đã khóa sổ");
            var log = existing ?? new NightAuditLog { PropertyId = req.PropertyId, BusinessDate = req.BusinessDate, Status = "RUNNING" };
            if (existing == null) db.NightAuditLogs.Add(log);
            else log.Status = "RUNNING";
            await db.SaveChangesAsync();
            var stays = await (from r in db.Reservations
                               where r.PropertyId == req.PropertyId && r.Status == "CHECKED_IN" && r.BookingMode == "OVERNIGHT"
                                  && r.CheckInDate <= req.BusinessDate && r.CheckOutDate > req.BusinessDate
                               select r).ToListAsync();
            var posted = 0; var skipped = 0; decimal postedAmount = 0;
            foreach (var stay in stays)
            {
                if (await db.NightAuditLines.AnyAsync(x => x.ReservationId == stay.Id && x.BusinessDate == req.BusinessDate)) { skipped++; continue; }
                var charge = new FolioCharge
                {
                    ReservationId = stay.Id, Category = "ROOM", Description = $"Tiền phòng đêm {req.BusinessDate:dd/MM/yyyy}",
                    Quantity = 1, UnitPrice = stay.NightlyRate, Amount = stay.NightlyRate, BusinessDate = req.BusinessDate, NightAuditLogId = log.Id
                };
                db.Charges.Add(charge);
                await db.SaveChangesAsync();
                db.NightAuditLines.Add(new NightAuditLine { NightAuditLogId = log.Id, ReservationId = stay.Id, BusinessDate = req.BusinessDate, Action = "POSTED", Amount = charge.Amount, FolioChargeId = charge.Id });
                posted++; postedAmount += charge.Amount;
            }
            var rooms = await db.Rooms.CountAsync(x => x.PropertyId == req.PropertyId && !x.IsOutOfOrder);
            log.Status = "COMPLETED"; log.RoomCharges = postedAmount; log.RoomsSold = stays.Count; log.RoomsAvailable = rooms;
            log.OccupancyPercent = rooms == 0 ? 0 : Math.Round(100m * stays.Count / rooms, 2);
            log.Adr = posted > 0 ? Math.Round(postedAmount / posted, 0) : 0;
            log.Revpar = rooms == 0 ? 0 : Math.Round(postedAmount / rooms, 0);
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, req.PropertyId, "NIGHT_AUDIT_RUN", "NightAuditLog", log.Id, req.BusinessDate.ToString("yyyy-MM-dd"));
            await tx.CommitAsync();
            return Results.Ok(new { log.Id, log.Status, posted, skipped, postedAmount, log.OccupancyPercent, log.Adr, log.Revpar });
        });
        api.MapPost("/night-audit/{id:int}/lock", async (int id, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.CanManageFinance(actor)) return Results.Forbid();
            var log = await db.NightAuditLogs.FindAsync(id);
            if (log == null) return Results.NotFound();
            if (!Ops.HasScope(actor, log.PropertyId)) return Results.Forbid();
            if (log.Status is not ("COMPLETED" or "REOPENED")) return Ops.Conflict("Chỉ khóa ngày đã chạy xong");
            log.Status = "LOCKED"; log.LockedAtUtc = DateTime.UtcNow; log.LockedByUserId = Ops.UserId(actor);
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, log.PropertyId, "NIGHT_AUDIT_LOCK", "NightAuditLog", id);
            return Results.Ok(log);
        });
        api.MapPost("/night-audit/{id:int}/reopen", async (int id, ReleaseInput req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!actor.IsInRole("CHAIN_ADMIN")) return Ops.Fail("Chỉ quản trị chuỗi được mở lại ngày đã khóa");
            var reason = Ops.TrimOrNull(req.Reason);
            if (reason == null) return Ops.Fail("Cần lý do mở lại ngày");
            var log = await db.NightAuditLogs.FindAsync(id);
            if (log == null) return Results.NotFound();
            if (log.Status != "LOCKED") return Ops.Conflict("Ngày chưa ở trạng thái khóa");
            log.Status = "REOPENED"; log.ReopenedAtUtc = DateTime.UtcNow; log.ReopenedByUserId = Ops.UserId(actor); log.ReopenReason = reason;
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, log.PropertyId, "NIGHT_AUDIT_REOPEN", "NightAuditLog", id, reason);
            return Results.Ok(log);
        });

        api.MapGet("/payment-refunds", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            return Results.Ok(await db.PaymentRefunds.Where(x => x.PropertyId == propertyId).OrderByDescending(x => x.Id).Take(50).ToListAsync());
        });
        api.MapPost("/payments/{id:int}/refund-requests", async (int id, RefundRequestInput req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.ValidIdempotency(req.IdempotencyKey)) return Ops.Fail("idempotencyKey bắt buộc là UUID");
            var payment = await db.Payments.FindAsync(id);
            if (payment == null) return Results.NotFound();
            var reservation = await db.Reservations.FindAsync(payment.ReservationId);
            if (reservation == null) return Results.NotFound();
            if (!Ops.HasScope(actor, reservation.PropertyId)) return Results.Forbid();
            if (payment.Amount <= 0 || payment.Kind is not ("PAYMENT" or "DEPOSIT" or "GROUP_PAYMENT")) return Ops.Fail("Chỉ lập hoàn cho khoản thu dương");
            if (req.Amount <= 0 || req.Amount > payment.Amount) return Ops.Fail("Số hoàn phải lớn hơn 0 và không vượt khoản gốc");
            var already = await db.PaymentRefunds.Where(x => x.OriginalPaymentId == id && x.Status == "POSTED").SumAsync(x => (decimal?)x.ApprovedAmount) ?? 0;
            if (already + req.Amount > payment.Amount) return Ops.Conflict("Số hoàn vượt phần còn được hoàn");
            var replay = await db.PaymentRefunds.FirstOrDefaultAsync(x => x.IdempotencyKey == req.IdempotencyKey);
            if (replay != null) return Results.Ok(replay);
            var reason = req.ReasonCode.Trim().ToUpperInvariant();
            if (reason is not ("GUEST" or "OVERPAY" or "CORRECTION" or "OTHER")) return Ops.Fail("Lý do hoàn không hợp lệ");
            var row = new PaymentRefund
            {
                PropertyId = reservation.PropertyId, ReservationId = reservation.Id, OriginalPaymentId = id,
                RequestedAmount = req.Amount, ReasonCode = reason, ReasonText = Ops.TrimOrNull(req.ReasonText),
                RequestedByUserId = Ops.UserId(actor), IdempotencyKey = req.IdempotencyKey
            };
            db.PaymentRefunds.Add(row);
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, reservation.PropertyId, "REFUND_REQUEST", "PaymentRefund", row.Id);
            return Results.Ok(row);
        });
        api.MapPost("/payment-refunds/{id:int}/approve", async (int id, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.CanManageFinance(actor)) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var row = await db.PaymentRefunds.FindAsync(id);
            if (row == null) return Results.NotFound();
            if (!Ops.HasScope(actor, row.PropertyId)) return Results.Forbid();
            if (row.Status != "PENDING") return Ops.Conflict("Phiếu hoàn không còn chờ duyệt");
            if (row.RequestedByUserId == Ops.UserId(actor)) return Ops.Fail("Người duyệt phải khác người lập phiếu");
            var locked = await Ops.RejectIfLocked(db, row.PropertyId);
            if (locked != null) return locked;
            await Ops.LockReservation(db, row.ReservationId);
            var payment = await db.Payments.FirstAsync(x => x.Id == row.OriginalPaymentId);
            var already = await db.PaymentRefunds.Where(x => x.OriginalPaymentId == payment.Id && x.Status == "POSTED").SumAsync(x => (decimal?)x.ApprovedAmount) ?? 0;
            if (already + row.RequestedAmount > payment.Amount) return Ops.Conflict("Số hoàn vượt phần còn được hoàn");
            var refund = new Payment
            {
                ReservationId = row.ReservationId, Amount = -row.RequestedAmount, Method = payment.Method, Kind = "REFUND",
                Reference = "REFUND-" + row.Id, IdempotencyKey = row.IdempotencyKey
            };
            db.Payments.Add(refund);
            await db.SaveChangesAsync();
            row.Status = "POSTED"; row.ApprovedAmount = row.RequestedAmount; row.ApprovedByUserId = Ops.UserId(actor);
            row.ApprovedAtUtc = DateTime.UtcNow; row.PostedPaymentId = refund.Id;
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, row.PropertyId, "REFUND_POST", "Payment", refund.Id);
            await tx.CommitAsync();
            return Results.Ok(row);
        });
        api.MapPost("/payment-refunds/{id:int}/reject", async (int id, ReleaseInput req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.CanManageFinance(actor)) return Results.Forbid();
            var row = await db.PaymentRefunds.FindAsync(id);
            if (row == null) return Results.NotFound();
            if (!Ops.HasScope(actor, row.PropertyId)) return Results.Forbid();
            if (row.Status != "PENDING") return Ops.Conflict("Phiếu hoàn không còn chờ duyệt");
            if (row.RequestedByUserId == Ops.UserId(actor)) return Ops.Fail("Người duyệt phải khác người lập phiếu");
            row.Status = "REJECTED"; row.ReasonText = Ops.TrimOrNull(req.Reason) ?? row.ReasonText; row.ApprovedByUserId = Ops.UserId(actor); row.ApprovedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(row);
        });

        api.MapGet("/operations/alerts", async (int propertyId, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();
            var property = await db.Properties.FindAsync(propertyId);
            if (property == null) return Results.NotFound();
            var today = PropertyToday(property.TimeZoneId);
            var rooms = await db.Rooms.CountAsync(x => x.PropertyId == propertyId && !x.IsOutOfOrder);
            var inHouse = await db.Reservations.CountAsync(x => x.PropertyId == propertyId && x.Status == "CHECKED_IN");
            var occupancy = rooms == 0 ? 0 : Math.Round(100m * inHouse / rooms, 1);
            var (start, end) = PropertyDayRangeUtc(property.TimeZoneId, today);
            var ids = db.Reservations.Where(r => r.PropertyId == propertyId).Select(r => r.Id);
            var revenue = await db.Charges.Where(x => ids.Contains(x.ReservationId) && x.PostedAtUtc >= start && x.PostedAtUtc < end && x.Category == "ROOM").SumAsync(x => (decimal?)x.Amount) ?? 0;
            var stock = await db.Services.Where(x => x.PropertyId == propertyId && x.TrackInventory && x.MinStock > 0 && x.StockQuantity <= x.MinStock)
                .Select(x => new { x.Id, x.Name, x.StockQuantity, x.MinStock }).ToListAsync();
            var alerts = new List<object>();
            if (property.LowOccupancyPercent > 0 && occupancy < property.LowOccupancyPercent)
                alerts.Add(new { kind = "OCCUPANCY", message = $"Công suất {occupancy}% thấp hơn ngưỡng {property.LowOccupancyPercent}%" });
            if (property.LowRevenueAmount > 0 && revenue < property.LowRevenueAmount)
                alerts.Add(new { kind = "REVENUE", message = $"Tiền phòng ghi hôm nay {revenue:N0} thấp hơn ngưỡng {property.LowRevenueAmount:N0}" });
            foreach (var item in stock) alerts.Add(new { kind = "STOCK", message = $"{item.Name} còn {item.StockQuantity}, định mức {item.MinStock}" });
            return Results.Ok(new { occupancy, revenue, rooms, inHouse, property.LowOccupancyPercent, property.LowRevenueAmount, alerts });
        });
        api.MapPatch("/properties/{id:int}/alert-thresholds", async (int id, AlertThresholdInput req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, id)) return Results.Forbid();
            if (req.LowOccupancyPercent is < 0 or > 100 || req.LowRevenueAmount < 0) return Ops.Fail("Ngưỡng không hợp lệ");
            var property = await db.Properties.FindAsync(id);
            if (property == null) return Results.NotFound();
            property.LowOccupancyPercent = req.LowOccupancyPercent; property.LowRevenueAmount = req.LowRevenueAmount;
            await db.SaveChangesAsync();
            return Results.Ok(new { property.Id, property.LowOccupancyPercent, property.LowRevenueAmount });
        });
        api.MapPatch("/services/{id:int}/min-stock", async (int id, MinStockInput req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.CanManage(actor)) return Results.Forbid();
            var service = await db.Services.FindAsync(id);
            if (service == null) return Results.NotFound();
            if (!Ops.HasScope(actor, service.PropertyId)) return Results.Forbid();
            if (req.MinStock < 0) return Ops.Fail("Định mức không âm");
            service.MinStock = req.MinStock;
            await db.SaveChangesAsync();
            return Results.Ok(new { service.Id, service.MinStock, service.StockQuantity });
        });
        api.MapGet("/integrations/readiness", (ClaimsPrincipal actor) =>
        {
            if (!Ops.CanManage(actor)) return Results.Forbid();
            return Results.Ok(new
            {
                ota = "Sandbox kênh bán đã có trong mã. Chưa nối Hotelink hay OTA thật.",
                eInvoice = "Hóa đơn điện tử đang là bản nháp. Chưa có nhà cung cấp theo hợp đồng.",
                restaurant = "Chưa có hợp đồng POS nhà hàng.",
                spa = "Chưa có hợp đồng spa.",
                pbx = "Chưa có tổng đài để ghi cước phòng."
            });
        });
    }

    public static DateOnly PropertyToday(string? timeZoneId) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PropertyZone(timeZoneId)));

    public static (DateTime StartUtc, DateTime EndUtc) PropertyDayRangeUtc(string? timeZoneId, DateOnly day)
    {
        var zone = PropertyZone(timeZoneId);
        var startLocal = DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var endLocal = DateTime.SpecifyKind(day.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(startLocal, zone), TimeZoneInfo.ConvertTimeToUtc(endLocal, zone));
    }

    static TimeZoneInfo PropertyZone(string? timeZoneId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Ho_Chi_Minh" : timeZoneId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "VN", "VN"); }
    }

    public static async Task EnsureCleaningTask(PmsDbContext db, Room room, string kind)
    {
        var task = await db.HousekeepingTasks.Where(t => t.RoomId == room.Id && t.Status != "FINISH").OrderByDescending(t => t.Id).FirstOrDefaultAsync();
        if (task == null)
        {
            task = new HousekeepingTask { PropertyId = room.PropertyId, RoomId = room.Id, Kind = kind, Status = "NEW" };
            db.HousekeepingTasks.Add(task);
            await db.SaveChangesAsync();
        }
        await EnsureChecklist(db, task);
    }

    static async Task EnsureChecklist(PmsDbContext db, HousekeepingTask task)
    {
        if (await db.HousekeepingTaskItems.AnyAsync(x => x.TaskId == task.Id)) return;
        foreach (var check in CleaningChecks)
            db.HousekeepingTaskItems.Add(new HousekeepingTaskItem { TaskId = task.Id, ChecklistCode = check.Code, Label = check.Label, IsRequired = true });
        await db.SaveChangesAsync();
    }

    static async Task EnsureNoteTypes(PmsDbContext db, int propertyId)
    {
        if (await db.BookingNoteTypes.AnyAsync(x => x.PropertyId == propertyId)) return;
        var order = 1;
        foreach (var item in NoteCatalog)
            db.BookingNoteTypes.Add(new BookingNoteType { PropertyId = propertyId, Code = item.Code, Name = item.Name, IsRequired = false, SortOrder = order++ });
        await db.SaveChangesAsync();
    }

    public static async Task<bool> ActiveHold(PmsDbContext db, int roomId, DateOnly start, DateOnly end, int? exceptId = null) =>
        await db.ReservationHolds.AnyAsync(h => h.RoomId == roomId && h.Id != exceptId && h.Status == "ACTIVE" && h.ExpiresAtUtc > DateTime.UtcNow && h.CheckInDate < end && h.CheckOutDate > start);

    static async Task ExpireHolds(PmsDbContext db, int propertyId)
    {
        var now = DateTime.UtcNow;
        await db.ReservationHolds.Where(h => h.PropertyId == propertyId && h.Status == "ACTIVE" && h.ExpiresAtUtc <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, "EXPIRED").SetProperty(h => h.ReleasedAtUtc, now).SetProperty(h => h.ReleaseReason, "Hết giờ giữ"));
    }

    static async Task<List<DepositWarning>> DepositWarnings(PmsDbContext db, int propertyId)
    {
        var rows = await db.Reservations.Where(r => r.PropertyId == propertyId && r.Status == "CONFIRMED" && r.DepositRequiredAmount > 0 && r.DepositDueAtUtc != null).ToListAsync();
        var ids = rows.Select(r => r.Id).ToArray();
        var received = await db.Payments.Where(p => ids.Contains(p.ReservationId) && p.Kind == "DEPOSIT")
            .GroupBy(p => p.ReservationId).Select(g => new { g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync();
        var now = DateTime.UtcNow;
        var list = new List<DepositWarning>();
        foreach (var row in rows)
        {
            var got = received.FirstOrDefault(x => x.Key == row.Id)?.Amount ?? 0;
            if (got >= row.DepositRequiredAmount || row.DepositDueAtUtc == null) continue;
            var due = row.DepositDueAtUtc.Value;
            var milestone = due <= now ? "OVERDUE" : due <= now.AddDays(1) ? "D1" : due <= now.AddDays(3) ? "D3" : null;
            if (milestone == null) continue;
            list.Add(new DepositWarning(row.Id, row.Code, row.DepositRequiredAmount, got, due, milestone, row.Status));
        }
        return list;
    }
}

public record NoteTypeInput(int PropertyId, string Code, string Name, bool IsRequired, int SortOrder);
public record NoteTypeRequiredInput(bool IsRequired);
public record ReservationNoteInput(int NoteTypeId, string? NoteText);
public record DepositTermsInput(decimal Amount, DateTime? DueAtUtc, string? PolicyCode);
public record HoldInput(int PropertyId, int RoomId, string GuestName, string? GuestPhone, DateOnly CheckInDate, DateOnly CheckOutDate, int HoldMinutes, string? IdempotencyKey);
public record ReleaseInput(string? Reason);
public record ChecklistInput(bool IsDone, string? Note);
public record NightRunInput(int PropertyId, DateOnly BusinessDate);
public record RefundRequestInput(decimal Amount, string ReasonCode, string? ReasonText, string IdempotencyKey);
public record AlertThresholdInput(decimal LowOccupancyPercent, decimal LowRevenueAmount);
public record MinStockInput(int MinStock);
public record DepositWarning(int ReservationId, string Code, decimal Required, decimal Received, DateTime DueAtUtc, string Milestone, string Status);
