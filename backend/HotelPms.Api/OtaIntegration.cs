using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HotelPms.Api;

public sealed class ChannelConnection
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [MaxLength(20)] public string Provider { get; set; } = "SANDBOX";
    [MaxLength(120)] public string DisplayName { get; set; } = "";
    [MaxLength(60)] public string HotelCode { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "DRAFT"; // DRAFT | ACTIVE | DISABLED
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastInboundAtUtc { get; set; }
}

public sealed class ChannelRoomMapping
{
    public int Id { get; set; }
    public int ConnectionId { get; set; }
    public int PropertyId { get; set; }
    public int RoomTypeId { get; set; }
    [MaxLength(60)] public string ChannelRoomCode { get; set; } = "";
    [MaxLength(60)] public string ChannelRateCode { get; set; } = "";
    public int? RatePlanId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

// One row per distinct inbound message. Raw payloads are not stored; only a hash, to limit retained guest data.
public sealed class ChannelBooking
{
    public int Id { get; set; }
    public int ConnectionId { get; set; }
    public int PropertyId { get; set; }
    [MaxLength(20)] public string Provider { get; set; } = "";
    [MaxLength(80)] public string ChannelBookingId { get; set; } = "";
    [MaxLength(10)] public string EventType { get; set; } = "NEW"; // NEW | MODIFY | CANCEL
    [MaxLength(64)] public string PayloadHash { get; set; } = "";
    [MaxLength(64)] public string Fingerprint { get; set; } = "";
    // PENDING_CONFIRMATION | CONFIRMED | NEEDS_REVIEW | FAILED | CANCELLED | REJECTED | RESOLVED
    [MaxLength(30)] public string Status { get; set; } = "RECEIVED";
    public int? ReservationId { get; set; }
    public int? RelatedBookingId { get; set; }
    [MaxLength(200)] public string GuestName { get; set; } = "";
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(160)] public string? Email { get; set; }
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    [MaxLength(60)] public string ChannelRoomCode { get; set; } = "";
    [MaxLength(60)] public string ChannelRateCode { get; set; } = "";
    public int Adults { get; set; }
    public int Children { get; set; }
    [Precision(18, 2)] public decimal TotalAmount { get; set; }
    [MaxLength(3)] public string Currency { get; set; } = "VND";
    public bool IsSandbox { get; set; }
    [MaxLength(500)] public string? Message { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FirstConfirmedAtUtc { get; set; }
    public DateTime? SecondConfirmedAtUtc { get; set; }
    public int? SecondConfirmedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public int? ReviewedByUserId { get; set; }
    [MaxLength(500)] public string? ReviewNote { get; set; }
}

public sealed class ChannelSyncLog
{
    public int Id { get; set; }
    public int? ConnectionId { get; set; }
    public int? PropertyId { get; set; }
    [MaxLength(20)] public string Provider { get; set; } = "";
    [MaxLength(10)] public string Direction { get; set; } = "INBOUND";
    [MaxLength(30)] public string EventType { get; set; } = "";
    [MaxLength(20)] public string Outcome { get; set; } = "";
    [MaxLength(500)] public string Message { get; set; } = "";
    public int? ChannelBookingRecordId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record OtaBookingPayload(string HotelCode, string ChannelBookingId, string EventType, string GuestName, string? Phone, string? Email,
    DateOnly CheckInDate, DateOnly CheckOutDate, string RoomCode, string? RateCode, int Adults, int Children, decimal TotalAmount, string? Currency);
public record ChannelConnectionInput(int PropertyId, string Provider, string DisplayName, string HotelCode);
public record ChannelStatusInput(string Status);
public record ChannelMappingInput(int ConnectionId, int RoomTypeId, string ChannelRoomCode, string? ChannelRateCode, int? RatePlanId);
public record ChannelReviewInput(string Decision, string Note);
public record SandboxBookingInput(int ConnectionId, string? ChannelBookingId, string EventType, string GuestName, string? Phone, string? Email,
    DateOnly CheckInDate, DateOnly CheckOutDate, string RoomCode, string? RateCode, int Adults, int Children, decimal TotalAmount);

public static class OtaIntegration
{
    public static readonly string[] Providers = ["SANDBOX", "HOTELINK"];
    private static readonly string[] EventTypes = ["NEW", "MODIFY", "CANCEL"];
    private static readonly string[] LiveStatuses = ["PENDING_CONFIRMATION", "CONFIRMED"];
    private static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    public static string? WebhookSecret(IConfiguration conf, string provider) {
        var secret = conf[$"Ota:Providers:{provider}:WebhookSecret"];
        return string.IsNullOrWhiteSpace(secret) || secret.Length < 32 ? null : secret;
    }

    public static bool VerifySignature(string secret, string timestamp, string body, string? signatureHeader) {
        if (string.IsNullOrWhiteSpace(signatureHeader) || !signatureHeader.StartsWith("sha256=", StringComparison.Ordinal)) return false;
        byte[] provided;
        try { provided = Convert.FromHexString(signatureHeader["sha256=".Length..]); } catch (FormatException) { return false; }
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    private static string Sha256Hex(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string? Validate(OtaBookingPayload p) {
        if (string.IsNullOrWhiteSpace(p.ChannelBookingId) || p.ChannelBookingId.Length > 80) return "Thiếu mã booking kênh hoặc dài quá 80 ký tự";
        if (!EventTypes.Contains(p.EventType?.ToUpperInvariant())) return "Loại sự kiện phải là NEW, MODIFY hoặc CANCEL";
        if (string.IsNullOrWhiteSpace(p.GuestName) || p.GuestName.Length > 200) return "Thiếu tên khách";
        if (string.IsNullOrWhiteSpace(p.RoomCode) || p.RoomCode.Length > 60) return "Thiếu mã phòng kênh";
        if (p.CheckInDate >= p.CheckOutDate || p.CheckOutDate.DayNumber - p.CheckInDate.DayNumber > 60) return "Khoảng ngày lưu trú không hợp lệ (tối đa 60 đêm)";
        if (p.Adults < 1 || p.Adults > 20 || p.Children < 0 || p.Children > 20) return "Số khách không hợp lệ";
        if (p.TotalAmount < 0 || p.TotalAmount > 10000000000m) return "Tổng tiền không hợp lệ";
        return null;
    }

    private static async Task WriteLog(PmsDbContext db, ChannelConnection? c, string provider, string eventType, string outcome, string message, int? recordId = null) {
        db.ChannelSyncLogs.Add(new ChannelSyncLog { ConnectionId = c?.Id, PropertyId = c?.PropertyId, Provider = provider, EventType = eventType,
            Outcome = outcome, Message = message.Length > 500 ? message[..500] : message, ChannelBookingRecordId = recordId });
        await db.SaveChangesAsync();
    }

    public static async Task<(ChannelBooking? Booking, string? Error, bool Replay)> Ingest(PmsDbContext db, ChannelConnection connection, OtaBookingPayload payload, bool isSandbox) {
        var error = Validate(payload);
        if (error != null) { await WriteLog(db, connection, connection.Provider, payload.EventType ?? "", "REJECTED", error); return (null, error, false); }
        var eventType = payload.EventType.ToUpperInvariant();
        var hash = Sha256Hex(JsonSerializer.Serialize(payload));
        var existing = await db.ChannelBookings.FirstOrDefaultAsync(x => x.Provider == connection.Provider && x.ChannelBookingId == payload.ChannelBookingId && x.PayloadHash == hash);
        if (existing != null) {
            await WriteLog(db, connection, connection.Provider, eventType, "REPLAY", $"Bản tin lặp lại booking {payload.ChannelBookingId}", existing.Id);
            return (existing, null, true);
        }
        var phoneDigits = new string((payload.Phone ?? "").Where(char.IsDigit).ToArray());
        var booking = new ChannelBooking {
            ConnectionId = connection.Id, PropertyId = connection.PropertyId, Provider = connection.Provider, ChannelBookingId = payload.ChannelBookingId.Trim(),
            EventType = eventType, PayloadHash = hash, IsSandbox = isSandbox,
            Fingerprint = Sha256Hex($"{connection.PropertyId}|{payload.GuestName.Trim().ToLowerInvariant()}|{phoneDigits}|{payload.CheckInDate:yyyy-MM-dd}|{payload.CheckOutDate:yyyy-MM-dd}"),
            GuestName = payload.GuestName.Trim(), Phone = payload.Phone?.Trim(), Email = payload.Email?.Trim(),
            CheckInDate = payload.CheckInDate, CheckOutDate = payload.CheckOutDate, ChannelRoomCode = payload.RoomCode.Trim(),
            ChannelRateCode = payload.RateCode?.Trim() ?? "", Adults = payload.Adults, Children = payload.Children, TotalAmount = payload.TotalAmount,
            Currency = string.IsNullOrWhiteSpace(payload.Currency) ? "VND" : payload.Currency.Trim().ToUpperInvariant()[..Math.Min(3, payload.Currency.Trim().Length)]
        };
        var original = await db.ChannelBookings.Where(x => x.Provider == connection.Provider && x.ChannelBookingId == booking.ChannelBookingId && x.EventType == "NEW")
            .OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (eventType == "NEW") {
            var lookalike = original ?? await db.ChannelBookings.Where(x => x.PropertyId == connection.PropertyId && x.Fingerprint == booking.Fingerprint &&
                LiveStatuses.Contains(x.Status)).OrderBy(x => x.Id).FirstOrDefaultAsync();
            if (lookalike != null) {
                booking.Status = "NEEDS_REVIEW"; booking.RelatedBookingId = lookalike.Id;
                booking.Message = original != null ? "Trùng mã booking kênh nhưng nội dung khác bản đã nhận" : "Nghi trùng: cùng khách, cùng ngày lưu trú với booking kênh khác";
            }
        } else if (eventType == "MODIFY") {
            booking.Status = "NEEDS_REVIEW"; booking.RelatedBookingId = original?.Id;
            booking.Message = "Kênh gửi thay đổi booking; lễ tân đối chiếu và cập nhật thủ công";
        } else {
            booking.RelatedBookingId = original?.Id;
            var reservation = original?.ReservationId is int rid ? await db.Reservations.FirstOrDefaultAsync(x => x.Id == rid) : null;
            var paid = reservation == null ? 0 : await db.Payments.Where(x => x.ReservationId == reservation.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
            if (reservation is { Status: "CONFIRMED" } && paid == 0) {
                reservation.Status = "CANCELLED"; booking.Status = "CANCELLED"; booking.ReservationId = reservation.Id;
                booking.Message = $"Đã hủy booking {reservation.Code} theo yêu cầu kênh";
            } else {
                booking.Status = "NEEDS_REVIEW";
                booking.Message = reservation == null ? "Không tìm thấy booking gốc để hủy" : "Booking đã nhận phòng hoặc có tiền cọc; cần xử lý hủy thủ công";
            }
        }
        if (booking.Status == "RECEIVED") return await Allocate(db, connection, booking);
        db.ChannelBookings.Add(booking);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) {
            db.ChangeTracker.Clear();
            var raced = await db.ChannelBookings.FirstAsync(x => x.Provider == booking.Provider && x.ChannelBookingId == booking.ChannelBookingId && x.PayloadHash == hash);
            return (raced, null, true);
        }
        connection.LastInboundAtUtc = DateTime.UtcNow;
        await WriteLog(db, connection, connection.Provider, eventType, booking.Status, booking.Message ?? "", booking.Id);
        return (booking, null, false);
    }

    // Layer 1 of the two-layer confirmation: the PMS itself accepts the booking by allocating a room and creating the reservation.
    public static async Task<(ChannelBooking? Booking, string? Error, bool Replay)> Allocate(PmsDbContext db, ChannelConnection connection, ChannelBooking booking) {
        var mapping = await db.ChannelRoomMappings.AsNoTracking().Where(x => x.ConnectionId == connection.Id && x.IsActive && x.ChannelRoomCode == booking.ChannelRoomCode &&
            (x.ChannelRateCode == booking.ChannelRateCode || x.ChannelRateCode == "")).OrderByDescending(x => x.ChannelRateCode).FirstOrDefaultAsync();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (mapping == null) {
            booking.Status = "FAILED"; booking.Message = $"Chưa mapping mã phòng kênh '{booking.ChannelRoomCode}' với hạng phòng PMS";
        } else {
            var candidates = await db.Rooms.Where(r => r.PropertyId == connection.PropertyId && r.RoomTypeId == mapping.RoomTypeId && !r.IsOutOfOrder)
                .OrderBy(r => r.Number).Select(r => r.Id).ToListAsync();
            int? roomId = null;
            foreach (var id in candidates) {
                if (!await Ops.LockRoom(db, id)) continue;
                if (!await Ops.IsBooked(db, id, booking.CheckInDate, booking.CheckOutDate)) { roomId = id; break; }
            }
            if (roomId == null) {
                booking.Status = "FAILED"; booking.Message = "Hết phòng trống thuộc hạng đã mapping cho khoảng ngày này; cần xử lý overbooking";
            } else {
                var nights = booking.CheckOutDate.DayNumber - booking.CheckInDate.DayNumber;
                var planPrice = mapping.RatePlanId is int planId ? await db.RatePlans.Where(x => x.Id == planId).Select(x => (decimal?)x.BasePrice).FirstOrDefaultAsync() : null;
                var nightly = booking.TotalAmount > 0 ? Math.Round(booking.TotalAmount / nights, 0) : planPrice ?? await db.RoomTypes.Where(x => x.Id == mapping.RoomTypeId).Select(x => x.BaseRate).SingleAsync();
                var guest = new Guest { FullName = booking.GuestName, Phone = booking.Phone ?? "", Email = booking.Email };
                db.Guests.Add(guest); await db.SaveChangesAsync();
                var reservation = new Reservation {
                    Code = "BK-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), PropertyId = connection.PropertyId, RoomId = roomId.Value, GuestId = guest.Id,
                    CheckInDate = booking.CheckInDate, CheckOutDate = booking.CheckOutDate, Adults = booking.Adults, Children = booking.Children,
                    NightlyRate = nightly, BookingSource = "OTA", BookingMode = "OVERNIGHT", RatePlanId = mapping.RatePlanId, Status = "CONFIRMED",
                    Note = $"{(booking.IsSandbox ? "[SANDBOX] " : "")}{connection.Provider} #{booking.ChannelBookingId}"
                };
                db.Reservations.Add(reservation); await db.SaveChangesAsync();
                booking.ReservationId = reservation.Id; booking.Status = "PENDING_CONFIRMATION"; booking.FirstConfirmedAtUtc = DateTime.UtcNow;
                booking.Message = $"Đã giữ phòng và tạo booking {reservation.Code}; chờ lễ tân xác nhận lớp 2";
            }
        }
        if (booking.Id == 0) db.ChannelBookings.Add(booking);
        try { await db.SaveChangesAsync(); await tx.CommitAsync(); }
        catch (DbUpdateException) {
            await tx.RollbackAsync(); db.ChangeTracker.Clear();
            var raced = await db.ChannelBookings.FirstAsync(x => x.Provider == booking.Provider && x.ChannelBookingId == booking.ChannelBookingId && x.PayloadHash == booking.PayloadHash);
            return (raced, null, true);
        }
        connection.LastInboundAtUtc = DateTime.UtcNow;
        await WriteLog(db, connection, connection.Provider, booking.EventType, booking.Status, booking.Message ?? "", booking.Id);
        return (booking, null, false);
    }

    public static void MapOtaIntegration(this WebApplication app)
    {
        app.MapPost("/api/integrations/ota/{provider}/webhook", async (string provider, HttpRequest request, PmsDbContext db, IConfiguration conf) => {
            provider = provider.ToUpperInvariant();
            if (!Providers.Contains(provider)) return Results.NotFound(new { message = "Nhà cung cấp kênh không được hỗ trợ" });
            var secret = WebhookSecret(conf, provider);
            if (secret == null) return Results.Json(new { message = $"Chưa cấu hình khóa webhook cho {provider}; chưa nhận booking thật" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            var timestamp = request.Headers["X-Lotus-Timestamp"].ToString();
            if (!long.TryParse(timestamp, out var unix) || (DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unix)).Duration() > MaxClockSkew)
                return Results.Json(new { message = "Thiếu hoặc sai thời điểm ký (lệch quá 5 phút)" }, statusCode: StatusCodes.Status401Unauthorized);
            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            if (body.Length > 64 * 1024) return Results.BadRequest(new { message = "Bản tin quá lớn" });
            if (!VerifySignature(secret, timestamp, body, request.Headers["X-Lotus-Signature"]))
                return Results.Json(new { message = "Chữ ký webhook không hợp lệ" }, statusCode: StatusCodes.Status401Unauthorized);
            OtaBookingPayload? payload;
            try { payload = JsonSerializer.Deserialize<OtaBookingPayload>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch (JsonException) { return Results.BadRequest(new { message = "Bản tin JSON không hợp lệ" }); }
            if (payload == null || string.IsNullOrWhiteSpace(payload.HotelCode)) return Results.BadRequest(new { message = "Thiếu mã khách sạn trên kênh" });
            var connection = await db.ChannelConnections.FirstOrDefaultAsync(x => x.Provider == provider && x.HotelCode == payload.HotelCode && x.Status == "ACTIVE");
            if (connection == null) return Results.NotFound(new { message = "Không có kết nối kênh đang hoạt động cho mã khách sạn này" });
            var (booking, error, replay) = await Ingest(db, connection, payload, provider == "SANDBOX");
            return error != null ? Ops.Fail(error) : Results.Ok(new { receiptId = booking!.Id, booking.Status, booking.ReservationId, replay });
        }).AllowAnonymous();

        var api = app.MapGroup("/api/ota").RequireAuthorization();
        api.AddEndpointFilter(Ops.DepartmentAccess);

        api.MapGet("/providers", (IConfiguration conf) => Results.Ok(Providers.Select(p => new {
            code = p, webhookConfigured = WebhookSecret(conf, p) != null,
            note = p == "SANDBOX" ? "Môi trường thử: tạo booking giả lập để kiểm tra luồng, không kết nối OTA thật" : "Cần hợp đồng, tài liệu API và khóa webhook từ đối tác"
        })));

        api.MapGet("/connections", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.CanManage(user) || !Ops.HasScope(user, propertyId)) return Results.Forbid();
            return Results.Ok(await db.ChannelConnections.AsNoTracking().Where(x => x.PropertyId == propertyId).OrderBy(x => x.Id).ToListAsync());
        });

        api.MapPost("/connections", async (ChannelConnectionInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            var provider = req.Provider?.Trim().ToUpperInvariant() ?? "";
            var hotelCode = req.HotelCode?.Trim() ?? "";
            if (!Providers.Contains(provider)) return Ops.Fail("Nhà cung cấp phải là SANDBOX hoặc HOTELINK");
            if (string.IsNullOrWhiteSpace(req.DisplayName) || hotelCode.Length is 0 or > 60) return Ops.Fail("Cần tên kết nối và mã khách sạn trên kênh (tối đa 60 ký tự)");
            if (!await db.Properties.AnyAsync(x => x.Id == req.PropertyId)) return Results.NotFound();
            if (await db.ChannelConnections.AnyAsync(x => x.Provider == provider && x.HotelCode == hotelCode)) return Ops.Conflict("Mã khách sạn này đã được kết nối với nhà cung cấp");
            var connection = new ChannelConnection { PropertyId = req.PropertyId, Provider = provider, DisplayName = req.DisplayName.Trim(), HotelCode = hotelCode, CreatedByUserId = Ops.UserId(actor) };
            db.ChannelConnections.Add(connection); await db.SaveChangesAsync();
            await Ops.Log(db, actor, req.PropertyId, "OTA_CONNECTION_CREATE", "ChannelConnection", connection.Id, $"{provider} {hotelCode}");
            return Results.Ok(connection);
        });

        api.MapPatch("/connections/{id:int}/status", async (int id, ChannelStatusInput req, ClaimsPrincipal actor, PmsDbContext db, IConfiguration conf) => {
            var connection = await db.ChannelConnections.FirstOrDefaultAsync(x => x.Id == id);
            if (connection == null) return Results.NotFound();
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, connection.PropertyId)) return Results.Forbid();
            var status = req.Status?.Trim().ToUpperInvariant();
            if (status is not ("ACTIVE" or "DISABLED")) return Ops.Fail("Trạng thái phải là ACTIVE hoặc DISABLED");
            if (status == "ACTIVE" && connection.Provider != "SANDBOX" && WebhookSecret(conf, connection.Provider) == null)
                return Ops.Conflict($"Chưa cấu hình khóa webhook {connection.Provider} trên máy chủ; không thể bật kết nối thật");
            var before = connection.Status;
            connection.Status = status; await db.SaveChangesAsync();
            await Ops.Log(db, actor, connection.PropertyId, "OTA_CONNECTION_STATUS", "ChannelConnection", connection.Id, $"{before} -> {status}");
            return Results.Ok(connection);
        });

        api.MapGet("/mappings", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.CanManage(user) || !Ops.HasScope(user, propertyId)) return Results.Forbid();
            return Results.Ok(await (from m in db.ChannelRoomMappings.AsNoTracking() join t in db.RoomTypes on m.RoomTypeId equals t.Id
                where m.PropertyId == propertyId orderby m.ConnectionId, m.ChannelRoomCode
                select new { m.Id, m.ConnectionId, m.RoomTypeId, RoomTypeName = t.Name, m.ChannelRoomCode, m.ChannelRateCode, m.RatePlanId, m.IsActive }).ToListAsync());
        });

        api.MapPost("/mappings", async (ChannelMappingInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            var connection = await db.ChannelConnections.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.ConnectionId);
            if (connection == null) return Results.NotFound(new { message = "Không tìm thấy kết nối kênh" });
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, connection.PropertyId)) return Results.Forbid();
            var roomCode = req.ChannelRoomCode?.Trim() ?? "";
            var rateCode = req.ChannelRateCode?.Trim() ?? "";
            if (roomCode.Length is 0 or > 60 || rateCode.Length > 60) return Ops.Fail("Mã phòng kênh bắt buộc, tối đa 60 ký tự");
            if (!await db.RoomTypes.AnyAsync(x => x.Id == req.RoomTypeId && x.PropertyId == connection.PropertyId)) return Ops.Fail("Hạng phòng không thuộc cơ sở của kết nối");
            if (req.RatePlanId.HasValue && !await db.RatePlans.AnyAsync(x => x.Id == req.RatePlanId && x.PropertyId == connection.PropertyId && x.IsActive))
                return Ops.Fail("Bảng giá không thuộc cơ sở hoặc đã ngừng");
            if (await db.ChannelRoomMappings.AnyAsync(x => x.ConnectionId == connection.Id && x.ChannelRoomCode == roomCode && x.ChannelRateCode == rateCode))
                return Ops.Conflict("Mã phòng/giá kênh này đã được mapping");
            var mapping = new ChannelRoomMapping { ConnectionId = connection.Id, PropertyId = connection.PropertyId, RoomTypeId = req.RoomTypeId,
                ChannelRoomCode = roomCode, ChannelRateCode = rateCode, RatePlanId = req.RatePlanId };
            db.ChannelRoomMappings.Add(mapping); await db.SaveChangesAsync();
            await Ops.Log(db, actor, connection.PropertyId, "OTA_MAPPING_CREATE", "ChannelRoomMapping", mapping.Id, $"{roomCode}/{rateCode} -> room type {req.RoomTypeId}");
            return Results.Ok(mapping);
        });

        api.MapGet("/bookings", async (int propertyId, string? status, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.CanManage(user) || !Ops.HasScope(user, propertyId)) return Results.Forbid();
            var query = from b in db.ChannelBookings.AsNoTracking() where b.PropertyId == propertyId
                        join r in db.Reservations on b.ReservationId equals r.Id into rj from r in rj.DefaultIfEmpty()
                        join room in db.Rooms on r.RoomId equals room.Id into roj from room in roj.DefaultIfEmpty()
                        select new { b.Id, b.Provider, b.ChannelBookingId, b.EventType, b.Status, b.GuestName, b.CheckInDate, b.CheckOutDate, b.ChannelRoomCode,
                            b.Adults, b.TotalAmount, b.Currency, b.IsSandbox, b.Message, b.ReceivedAtUtc, b.FirstConfirmedAtUtc, b.SecondConfirmedAtUtc,
                            b.ReservationId, ReservationCode = r == null ? null : r.Code, RoomNumber = room == null ? null : room.Number, b.RelatedBookingId, b.ReviewNote };
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.ToUpper());
            return Results.Ok(await query.OrderByDescending(x => x.Id).Take(200).ToListAsync());
        });

        // Layer 2: a staff member verifies the channel booking against the extranet before it can be modified or cancelled.
        api.MapPost("/bookings/{id:int}/confirm", async (int id, ClaimsPrincipal actor, PmsDbContext db) => {
            var booking = await db.ChannelBookings.FirstOrDefaultAsync(x => x.Id == id);
            if (booking == null) return Results.NotFound();
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, booking.PropertyId)) return Results.Forbid();
            if (booking.Status != "PENDING_CONFIRMATION") return Ops.Conflict("Chỉ xác nhận được booking đang chờ xác nhận lớp 2");
            booking.Status = "CONFIRMED"; booking.SecondConfirmedAtUtc = DateTime.UtcNow; booking.SecondConfirmedByUserId = Ops.UserId(actor);
            booking.Message = "Đã xác nhận đủ 2 lớp";
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, booking.PropertyId, "OTA_BOOKING_CONFIRM", "ChannelBooking", booking.Id, $"{booking.Provider} #{booking.ChannelBookingId}");
            return Results.Ok(booking);
        });

        api.MapPost("/bookings/{id:int}/review", async (int id, ChannelReviewInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            var booking = await db.ChannelBookings.FirstOrDefaultAsync(x => x.Id == id);
            if (booking == null) return Results.NotFound();
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, booking.PropertyId)) return Results.Forbid();
            if (booking.Status is not ("NEEDS_REVIEW" or "FAILED")) return Ops.Conflict("Chỉ rà soát booking đang chờ xử lý hoặc lỗi");
            var decision = req.Decision?.Trim().ToUpperInvariant();
            if (decision is not ("ACCEPT" or "REJECT")) return Ops.Fail("Quyết định phải là ACCEPT hoặc REJECT");
            if (string.IsNullOrWhiteSpace(req.Note) || req.Note.Trim().Length < 5) return Ops.Fail("Cần ghi chú lý do xử lý (tối thiểu 5 ký tự)");
            booking.ReviewedAtUtc = DateTime.UtcNow; booking.ReviewedByUserId = Ops.UserId(actor); booking.ReviewNote = req.Note.Trim()[..Math.Min(500, req.Note.Trim().Length)];
            if (decision == "ACCEPT" && booking.EventType == "NEW" && booking.ReservationId == null) {
                var connection = await db.ChannelConnections.FirstAsync(x => x.Id == booking.ConnectionId);
                await Allocate(db, connection, booking);
            } else {
                booking.Status = decision == "REJECT" ? "REJECTED" : "RESOLVED";
                await db.SaveChangesAsync();
            }
            await Ops.Log(db, actor, booking.PropertyId, "OTA_BOOKING_REVIEW", "ChannelBooking", booking.Id, $"{decision} -> {booking.Status}");
            return Results.Ok(booking);
        });

        api.MapGet("/logs", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.CanManage(user) || !Ops.HasScope(user, propertyId)) return Results.Forbid();
            return Results.Ok(await db.ChannelSyncLogs.AsNoTracking().Where(x => x.PropertyId == propertyId).OrderByDescending(x => x.Id).Take(100).ToListAsync());
        });

        api.MapPost("/sandbox/bookings", async (SandboxBookingInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            var connection = await db.ChannelConnections.FirstOrDefaultAsync(x => x.Id == req.ConnectionId);
            if (connection == null) return Results.NotFound(new { message = "Không tìm thấy kết nối kênh" });
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, connection.PropertyId)) return Results.Forbid();
            if (connection.Provider != "SANDBOX") return Ops.Conflict("Chỉ gửi booking thử qua kết nối SANDBOX; kênh thật nhận qua webhook ký số");
            if (connection.Status != "ACTIVE") return Ops.Conflict("Kết nối SANDBOX chưa được bật");
            var payload = new OtaBookingPayload(connection.HotelCode, string.IsNullOrWhiteSpace(req.ChannelBookingId) ? "SBX-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant() : req.ChannelBookingId.Trim(),
                req.EventType, req.GuestName, req.Phone, req.Email, req.CheckInDate, req.CheckOutDate, req.RoomCode, req.RateCode, req.Adults, req.Children, req.TotalAmount, "VND");
            var (booking, error, replay) = await Ingest(db, connection, payload, true);
            if (error != null) return Ops.Fail(error);
            await Ops.Log(db, actor, connection.PropertyId, "OTA_SANDBOX_PUSH", "ChannelBooking", booking!.Id, $"{payload.EventType} #{payload.ChannelBookingId} -> {booking.Status}");
            return Results.Ok(new { booking, replay });
        });
    }
}
