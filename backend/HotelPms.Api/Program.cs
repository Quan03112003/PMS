using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using HotelPms.Api;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Data;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("PmsDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:PmsDb");
var databaseProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
builder.Services.AddDbContext<PmsDbContext>(options => {
    if (string.Equals(databaseProvider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
        options.UseNpgsql(connection);
    else if (string.Equals(databaseProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        options.UseSqlServer(connection);
    else
        throw new InvalidOperationException($"Unsupported Database:Provider '{databaseProvider}'");
});
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var key = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Missing Jwt:Secret");
if (Encoding.UTF8.GetByteCount(key) < 32) throw new InvalidOperationException("Jwt:Secret must be 32+ bytes");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30)
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddPolicy("angular", policy => policy
    .WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"])
    .AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors("angular");
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) {
    app.UseSwagger(); app.UseSwaggerUI();
}
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Database:InitializeForDemo")) {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DemoSeeder.Seed(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>());
    await AdvancedSeeder.Seed(db);
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", version = "1.0-demo" }));
app.MapPost("/api/auth/login", async (LoginRequest req, PmsDbContext db, IPasswordHasher<AppUser> hasher, IConfiguration conf) => {
    var user = await db.Users.FirstOrDefaultAsync(x => x.Email == req.Email.ToLower().Trim() && x.IsActive);
    if (user == null || hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
        return Results.Unauthorized();
    var claims = new List<Claim> {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.FullName),
        new(ClaimTypes.Role, user.Role), new("email", user.Email), new("propertyId", user.PropertyId?.ToString() ?? "")
    };
    var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(conf["Jwt:Secret"]!)), SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(issuer: conf["Jwt:Issuer"], audience: conf["Jwt:Audience"], claims: claims,
        expires: DateTime.UtcNow.AddHours(8), signingCredentials: credentials);
    return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token), user = new {
        user.Id, user.FullName, user.Email, user.Role, user.PropertyId
    }});
});

var api = app.MapGroup("/api").RequireAuthorization();
api.AddEndpointFilter(Ops.DepartmentAccess);
api.MapGet("/properties", async (ClaimsPrincipal user, PmsDbContext db) => {
    var scope = Ops.UserProperty(user);
    var query = db.Properties.AsNoTracking().Where(p => p.IsActive);
    if (scope.HasValue) query = query.Where(p => p.Id == scope.Value);
    return Results.Ok(await query.OrderBy(p => p.Name).ToListAsync());
});
api.MapPost("/properties", async (PropertyInput req, ClaimsPrincipal user, PmsDbContext db) => {
    if (!user.IsInRole("CHAIN_ADMIN")) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name)) return Ops.Fail("Cần nhập mã và tên khách sạn");
    if (await db.Properties.AnyAsync(p => p.Code == req.Code)) return Ops.Conflict("Mã khách sạn đã tồn tại");
    var property = new Property { Code = req.Code.Trim().ToUpperInvariant(), Name = req.Name.Trim(), City = req.City?.Trim() ?? "",
        Address = req.Address?.Trim() ?? "", Phone = req.Phone?.Trim() ?? "" };
    db.Properties.Add(property); await db.SaveChangesAsync();
    await Ops.Log(db, user, property.Id, "PROPERTY_CREATE", "Property", property.Id);
    return Results.Ok(property);
});
api.MapGet("/users", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.CanManage(user) || !Ops.HasScope(user, propertyId)) return Results.Forbid();
    var users = await db.Users.Where(x => x.PropertyId == propertyId ||
        (user.IsInRole("CHAIN_ADMIN") && x.PropertyId == null && x.Role == "CHAIN_ADMIN"))
        .OrderBy(x => x.Id).ToListAsync();
    return Results.Ok(users.Select(x => new { x.Id, x.FullName, x.Email, x.Role, Department = Ops.DepartmentName(x.Role), x.PropertyId, x.IsActive }));
});
api.MapPost("/users", async (UserInput req, ClaimsPrincipal actor, PmsDbContext db, IPasswordHasher<AppUser> hasher) => {
    if (!Ops.CanManage(actor)) return Results.Forbid();
    if (!Ops.IsValidRole(req.Role) || (req.Role == "CHAIN_ADMIN" && (!actor.IsInRole("CHAIN_ADMIN") || req.PropertyId != null)) ||
        (req.Role != "CHAIN_ADMIN" && !req.PropertyId.HasValue)) return Ops.Fail("Vai trò hoặc cơ sở không hợp lệ");
    if (!actor.IsInRole("CHAIN_ADMIN") && (req.Role is "CHAIN_ADMIN" or "PROPERTY_MANAGER" || !req.PropertyId.HasValue || !Ops.HasScope(actor, req.PropertyId.Value))) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@') || string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
        return Ops.Fail("Thông tin tài khoản không hợp lệ (mật khẩu tối thiểu 8 ký tự)");
    var email = req.Email.Trim().ToLowerInvariant();
    if (await db.Users.AnyAsync(x => x.Email == email)) return Ops.Conflict("Email đã đăng ký");
    if (req.PropertyId.HasValue && !await db.Properties.AnyAsync(x => x.Id == req.PropertyId.Value)) return Ops.Fail("Khách sạn không tồn tại");
    var item = new AppUser { FullName = req.FullName.Trim(), Email = email, Role = req.Role, PropertyId = req.PropertyId };
    item.PasswordHash = hasher.HashPassword(item, req.Password);
    db.Users.Add(item); await db.SaveChangesAsync();
    var auditPropertyId = item.PropertyId ?? await db.Properties.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
    await Ops.Log(db, actor, auditPropertyId, "USER_CREATE", "AppUser", item.Id);
    return Results.Ok(new { item.Id, item.FullName, item.Email, item.Role, Department = Ops.DepartmentName(item.Role), item.PropertyId, item.IsActive });
});
api.MapPatch("/users/{id:int}", async (int id, UserUpdateInput req, ClaimsPrincipal actor, PmsDbContext db, IPasswordHasher<AppUser> hasher) => {
    if (!Ops.CanManage(actor)) return Results.Forbid();
    var item = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
    if (item == null) return Results.NotFound();
    if (item.PropertyId.HasValue ? !Ops.HasScope(actor, item.PropertyId.Value) : !actor.IsInRole("CHAIN_ADMIN")) return Results.Forbid();
    if (id == Ops.UserId(actor) && (req.Role != item.Role || req.PropertyId != item.PropertyId || !req.IsActive))
        return Ops.Conflict("Không thể thay đổi vai trò, cơ sở hoặc khóa tài khoản đang đăng nhập");
    if (!Ops.IsValidRole(req.Role) || (req.Role == "CHAIN_ADMIN" && (!actor.IsInRole("CHAIN_ADMIN") || req.PropertyId != null)) ||
        (req.Role != "CHAIN_ADMIN" && !req.PropertyId.HasValue)) return Ops.Fail("Vai trò hoặc cơ sở không hợp lệ");
    if (!actor.IsInRole("CHAIN_ADMIN") && (req.Role is "CHAIN_ADMIN" or "PROPERTY_MANAGER" || !req.PropertyId.HasValue || !Ops.HasScope(actor, req.PropertyId.Value))) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@')) return Ops.Fail("Họ tên hoặc email không hợp lệ");
    var email = req.Email.Trim().ToLowerInvariant();
    if (await db.Users.AnyAsync(x => x.Id != id && x.Email == email)) return Ops.Conflict("Email đã đăng ký");
    if (req.PropertyId.HasValue && !await db.Properties.AnyAsync(x => x.Id == req.PropertyId.Value)) return Ops.Fail("Khách sạn không tồn tại");
    item.FullName = req.FullName.Trim(); item.Email = email; item.Role = req.Role;
    item.PropertyId = req.Role == "CHAIN_ADMIN" ? null : req.PropertyId;
    item.IsActive = req.IsActive;
    if (!string.IsNullOrWhiteSpace(req.Password)) {
        if (req.Password.Length < 8) return Ops.Fail("Mật khẩu tối thiểu 8 ký tự");
        item.PasswordHash = hasher.HashPassword(item, req.Password);
    }
    await db.SaveChangesAsync();
    var auditPropertyId = item.PropertyId ?? await db.Properties.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
    await Ops.Log(db, actor, auditPropertyId, "USER_UPDATE", "AppUser", item.Id);
    return Results.Ok(new { item.Id, item.FullName, item.Email, item.Role, Department = Ops.DepartmentName(item.Role), item.PropertyId, item.IsActive });
});
api.MapGet("/dashboard", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    var today = DateOnly.FromDateTime(DateTime.Today);
    var rooms = await db.Rooms.CountAsync(x => x.PropertyId == propertyId && !x.IsOutOfOrder);
    var inHouse = await db.Reservations.CountAsync(x => x.PropertyId == propertyId && x.Status == "CHECKED_IN");
    var arrivals = await db.Reservations.CountAsync(x => x.PropertyId == propertyId && x.Status == "CONFIRMED" && x.CheckInDate == today);
    var departures = await db.Reservations.CountAsync(x => x.PropertyId == propertyId && x.Status == "CHECKED_IN" && x.CheckOutDate == today);
    var dirty = await db.Rooms.CountAsync(x => x.PropertyId == propertyId && x.HousekeepingStatus == "DIRTY");
    var reservationIds = db.Reservations.Where(x => x.PropertyId == propertyId).Select(x => x.Id);
    var charges = await db.Charges.Where(x => reservationIds.Contains(x.ReservationId)).SumAsync(x => (decimal?)x.Amount) ?? 0;
    var receipts = await db.Payments.Where(x => reservationIds.Contains(x.ReservationId)).SumAsync(x => (decimal?)x.Amount) ?? 0;
    var recent = await (from r in db.Reservations join g in db.Guests on r.GuestId equals g.Id join rm in db.Rooms on r.RoomId equals rm.Id
        where r.PropertyId == propertyId orderby r.CreatedAtUtc descending
        select new { r.Id, r.Code, g.FullName, RoomNumber = rm.Number, r.CheckInDate, r.CheckOutDate, r.Status, r.BookingSource })
        .Take(6).ToListAsync();
    return Results.Ok(new { rooms, inHouse, available = Math.Max(0, rooms - inHouse), arrivals, departures, dirty,
        occupancy = rooms == 0 ? 0 : Math.Round(inHouse * 100m / rooms, 1), charges, receipts, recent });
});
api.MapGet("/room-types", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) =>
    Ops.HasScope(user, propertyId) ? (IResult)Results.Ok(await db.RoomTypes.Where(x => x.PropertyId == propertyId).OrderBy(x => x.Name).ToListAsync()) : Results.Forbid());
api.MapPost("/room-types", async (RoomTypeInput req, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.CanManage(user) || !Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
    if (req.BaseRate < 0 || req.Capacity < 1 || string.IsNullOrWhiteSpace(req.Name)) return Ops.Fail("Thông tin loại phòng không hợp lệ");
    if (await db.RoomTypes.AnyAsync(x => x.PropertyId == req.PropertyId && x.Code == req.Code)) return Ops.Conflict("Mã loại phòng đã tồn tại");
    var item = new RoomType { PropertyId = req.PropertyId, Code = req.Code.Trim().ToUpperInvariant(), Name = req.Name.Trim(), Capacity = req.Capacity, BaseRate = req.BaseRate };
    db.RoomTypes.Add(item); await db.SaveChangesAsync(); await Ops.Log(db, user, req.PropertyId, "ROOM_TYPE_CREATE", "RoomType", item.Id);
    return Results.Ok(item);
});
api.MapGet("/rooms", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    var rooms = await (from rm in db.Rooms join rt in db.RoomTypes on rm.RoomTypeId equals rt.Id
        where rm.PropertyId == propertyId orderby rm.Floor, rm.Number
        select new { rm.Id, rm.PropertyId, rm.RoomTypeId, rm.BuildingId, rm.Number, rm.Floor, rm.HousekeepingStatus, rm.IsOutOfOrder, rm.Note,
            RoomTypeName = rt.Name, rt.BaseRate, rt.Capacity,
            Current = (from r in db.Reservations join g in db.Guests on r.GuestId equals g.Id
                       where r.RoomId == rm.Id && r.Status == "CHECKED_IN"
                       select new { r.Id, r.Code, g.FullName, r.CheckOutDate }).FirstOrDefault(),
            Next = (from r in db.Reservations where r.RoomId == rm.Id && r.Status == "CONFIRMED"
                    orderby r.CheckInDate select new { r.Id, r.Code, r.CheckInDate }).FirstOrDefault()
        }).ToListAsync();
    return Results.Ok(rooms.Select(x => new {
        x.Id, x.PropertyId, x.RoomTypeId, x.BuildingId, x.Number, x.Floor, x.HousekeepingStatus, x.IsOutOfOrder, x.Note,
        x.RoomTypeName, x.BaseRate, x.Capacity, x.Current, x.Next,
        DisplayStatus = x.IsOutOfOrder ? "MAINTENANCE" : x.Current != null ? "OCCUPIED" : x.HousekeepingStatus == "DIRTY" ? "DIRTY" : x.Next != null && x.Next.CheckInDate == DateOnly.FromDateTime(DateTime.Today) ? "ARRIVAL" : "VACANT"
    }));
});
api.MapPost("/rooms", async (RoomInput req, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.CanManage(user) || !Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(req.Number) || req.Floor < 0) return Ops.Fail("Số phòng và tầng không hợp lệ");
    if (!await db.RoomTypes.AnyAsync(x => x.Id == req.RoomTypeId && x.PropertyId == req.PropertyId)) return Ops.Fail("Loại phòng không thuộc cơ sở");
    if (await db.Rooms.AnyAsync(x => x.PropertyId == req.PropertyId && x.Number == req.Number)) return Ops.Conflict("Số phòng đã tồn tại trong cơ sở");
    var item = new Room { PropertyId = req.PropertyId, RoomTypeId = req.RoomTypeId, Number = req.Number.Trim(), Floor = req.Floor };
    db.Rooms.Add(item); await db.SaveChangesAsync(); await Ops.Log(db, user, req.PropertyId, "ROOM_CREATE", "Room", item.Id);
    return Results.Ok(item);
});
api.MapPatch("/rooms/{roomId:int}/housekeeping", async (int roomId, HousekeepingInput req, ClaimsPrincipal user, PmsDbContext db) => {
    var room = await db.Rooms.FindAsync(roomId);
    if (room == null) return Results.NotFound();
    if (!Ops.HasScope(user, room.PropertyId)) return Results.Forbid();
    if (!new[] {"DIRTY", "CLEAN", "INSPECTED"}.Contains(req.Status)) return Ops.Fail("Trạng thái phòng không hợp lệ");
    room.HousekeepingStatus = req.Status;
    await db.SaveChangesAsync(); await Ops.Log(db, user, room.PropertyId, "HOUSEKEEPING_UPDATE", "Room", room.Id, req.Status);
    return Results.Ok(room);
});
api.MapPatch("/rooms/{roomId:int}/maintenance", async (int roomId, MaintenanceInput req, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.CanManage(user) && !user.IsInRole("MAINTENANCE_AGENT")) return Results.Forbid();
    var room = await db.Rooms.FindAsync(roomId);
    if (room == null) return Results.NotFound();
    if (!Ops.HasScope(user, room.PropertyId)) return Results.Forbid();
    if (req.IsOutOfOrder && await db.Reservations.AnyAsync(x => x.RoomId == roomId && x.Status == "CHECKED_IN"))
        return Ops.Conflict("Không thể khóa bảo trì phòng đang có khách");
    room.IsOutOfOrder = req.IsOutOfOrder; room.Note = req.Note;
    await db.SaveChangesAsync(); await Ops.Log(db, user, room.PropertyId, "MAINTENANCE_UPDATE", "Room", room.Id);
    return Results.Ok(room);
});
api.MapGet("/availability", async (int propertyId, DateOnly startDate, DateOnly endDate, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    if (startDate >= endDate) return Ops.Fail("Ngày trả phải sau ngày nhận");
    var rooms = await (from room in db.Rooms join type in db.RoomTypes on room.RoomTypeId equals type.Id
        where room.PropertyId == propertyId && !room.IsOutOfOrder && (startDate > DateOnly.FromDateTime(DateTime.Today) || room.HousekeepingStatus == "INSPECTED") &&
        !db.Reservations.Any(r => r.RoomId == room.Id && (r.Status == "CONFIRMED" || r.Status == "CHECKED_IN") && r.CheckInDate < endDate && r.CheckOutDate > startDate)
        orderby room.Floor, room.Number select new { room.Id, room.Number, room.Floor, room.RoomTypeId, RoomTypeName = type.Name, type.BaseRate, type.Capacity }).ToListAsync();
    return Results.Ok(rooms);
});
api.MapGet("/guests", async (int propertyId, string? q, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    var guestIds = db.Reservations.Where(r => r.PropertyId == propertyId).Select(r => r.GuestId);
    var guests = db.Guests.Where(g => guestIds.Contains(g.Id));
    if (!string.IsNullOrWhiteSpace(q)) guests = guests.Where(g => g.FullName.Contains(q) || g.Phone.Contains(q));
    return Results.Ok(await guests.OrderByDescending(g => g.CreatedAtUtc).Take(100).ToListAsync());
});
api.MapGet("/reservations", async (int propertyId, string? status, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    var query = from r in db.Reservations join g in db.Guests on r.GuestId equals g.Id
        join room in db.Rooms on r.RoomId equals room.Id where r.PropertyId == propertyId
        select new { r.Id, r.Code, r.PropertyId, r.RoomId, r.GuestId, g.FullName, g.Phone, RoomNumber = room.Number,
            r.CheckInDate, r.CheckOutDate, r.NightlyRate, r.Adults, r.Children, r.Status, r.BookingSource, r.BookingMode, r.GroupCode, r.Note, r.CreatedAtUtc,
            StayId = db.Stays.Where(s => s.ReservationId == r.Id).Select(s => (int?)s.Id).FirstOrDefault() };
    if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
    return Results.Ok(await query.OrderByDescending(x => x.CreatedAtUtc).Take(250).ToListAsync());
});
api.MapPost("/reservations", async (ReservationInput req, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
    if (req.CheckInDate >= req.CheckOutDate || req.Adults < 1 || string.IsNullOrWhiteSpace(req.GuestName))
        return Ops.Fail("Họ tên khách và khoảng ngày nhận/trả phải hợp lệ");
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    if (!await Ops.LockRoom(db, req.RoomId)) return Ops.Conflict("Phòng không tồn tại");
    var room = await db.Rooms.FirstOrDefaultAsync(x => x.Id == req.RoomId && x.PropertyId == req.PropertyId);
    if (room == null || room.IsOutOfOrder || (req.CheckInDate <= DateOnly.FromDateTime(DateTime.Today) && room.HousekeepingStatus != "INSPECTED")) return Ops.Conflict("Phòng không sẵn sàng bán");
    if (await Ops.IsBooked(db, room.Id, req.CheckInDate, req.CheckOutDate)) return Ops.Conflict("Phòng đã có đặt phòng trùng khoảng ngày");
    var rate = req.NightlyRate ?? await db.RoomTypes.Where(x => x.Id == room.RoomTypeId).Select(x => x.BaseRate).SingleAsync();
    if (rate < 0) return Ops.Fail("Giá phòng không hợp lệ");
    if (req.BookingMode != null && !new[] { "OVERNIGHT", "DAY_USE", "HOURLY" }.Contains(req.BookingMode)) return Ops.Fail("Loại lưu trú không hợp lệ");
    if (req.AgencyId.HasValue && !await db.Agencies.AnyAsync(x => x.Id == req.AgencyId && x.PropertyId == req.PropertyId)) return Ops.Fail("Đại lý không thuộc cơ sở");
    var guest = new Guest { FullName = req.GuestName.Trim(), Phone = req.Phone?.Trim() ?? "", Email = req.Email?.Trim() };
    db.Guests.Add(guest); await db.SaveChangesAsync();
    var item = new Reservation { Code = "BK-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), PropertyId = req.PropertyId, RoomId = req.RoomId,
        GuestId = guest.Id, CheckInDate = req.CheckInDate, CheckOutDate = req.CheckOutDate, Adults = req.Adults, Children = req.Children,
        NightlyRate = rate, BookingSource = req.BookingSource ?? "DIRECT", BookingMode = req.BookingMode ?? "OVERNIGHT", AgencyId = req.AgencyId,
        GroupCode = req.GroupCode, Note = req.Note, Status = "CONFIRMED" };
    db.Reservations.Add(item); await db.SaveChangesAsync();
    await Ops.Log(db, user, req.PropertyId, "RESERVATION_CREATE", "Reservation", item.Id, item.Code);
    await tx.CommitAsync();
    return Results.Ok(new { item.Id, item.Code, item.Status });
});
api.MapPost("/reservations/{id:int}/cancel", async (int id, ClaimsPrincipal user, PmsDbContext db) => {
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    var r = await db.Reservations.FindAsync(id); if (r == null) return Results.NotFound();
    if (!Ops.HasScope(user, r.PropertyId)) return Results.Forbid();
    await Ops.LockReservation(db, id);
    await db.Entry(r).ReloadAsync();
    if (r.Status != "CONFIRMED") return Ops.Conflict("Chỉ được hủy phòng chưa check-in");
    var paid = await db.Payments.Where(x => x.ReservationId == id).SumAsync(x => (decimal?)x.Amount) ?? 0;
    if (paid != 0) return Ops.Conflict("Booking còn tiền cọc, quản lý phải hoàn cọc trước khi hủy");
    r.Status = "CANCELLED"; await db.SaveChangesAsync();
    await Ops.Log(db, user, r.PropertyId, "RESERVATION_CANCEL", "Reservation", r.Id);
    await tx.CommitAsync(); return Results.Ok(new { r.Id, r.Status });
});
// Refund is an explicit negative ledger entry; cancellation is never allowed to lose a deposit.
api.MapPost("/reservations/{id:int}/refund-deposit", async (int id, PaymentInput req, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.CanManage(user)) return Results.Forbid();
    if (req.Amount <= 0 || req.Amount > 10000000000m || !new[] { "CASH", "TRANSFER", "CARD" }.Contains(req.Method) ||
        !Ops.ValidIdempotency(req.IdempotencyKey) || string.IsNullOrWhiteSpace(req.Reference))
        return Ops.Fail("Cần số tiền hoàn, phương thức, mã giao dịch và idempotencyKey UUID hợp lệ");
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    var r = await db.Reservations.FindAsync(id); if (r == null) return Results.NotFound();
    if (!Ops.HasScope(user, r.PropertyId)) return Results.Forbid();
    await Ops.LockReservation(db, id);
    var existing = await db.Payments.FirstOrDefaultAsync(x => x.IdempotencyKey == req.IdempotencyKey);
    if (existing != null) {
        if (existing.ReservationId != id || existing.Kind != "REFUND" || existing.Amount != -req.Amount || existing.Method != req.Method)
            return Ops.Conflict("idempotencyKey đã dùng cho giao dịch khác");
        await tx.CommitAsync(); return Results.Ok(existing);
    }
    await db.Entry(r).ReloadAsync();
    if (r.Status != "CONFIRMED") return Ops.Conflict("Chỉ hoàn cọc trước khi check-in");
    var balance = await db.Payments.Where(x => x.ReservationId == id).SumAsync(x => (decimal?)x.Amount) ?? 0;
    if (balance != req.Amount) return Ops.Conflict($"Phải hoàn đúng số cọc còn lại: {balance:N0} VND");
    int? shiftId = null;
    if (req.Method == "CASH") {
        var shift = await db.Shifts.FirstOrDefaultAsync(x => x.PropertyId == r.PropertyId && x.UserId == Ops.UserId(user) && x.Status == "OPEN");
        if (shift == null) return Ops.Conflict("Cần ca thu ngân đang mở để hoàn tiền mặt");
        shiftId = shift.Id;
    }
    var refund = new Payment { ReservationId = id, ShiftId = shiftId, Amount = -req.Amount, Method = req.Method,
        Kind = "REFUND", Reference = req.Reference.Trim(), IdempotencyKey = req.IdempotencyKey };
    db.Payments.Add(refund); await db.SaveChangesAsync();
    await Ops.Log(db, user, r.PropertyId, "DEPOSIT_REFUND", "Payment", refund.Id, req.Reference.Trim());
    await tx.CommitAsync(); return Results.Ok(refund);
});
api.MapPost("/reservations/{id:int}/check-in", async (int id, ClaimsPrincipal user, PmsDbContext db) => {
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    var r = await db.Reservations.FindAsync(id); if (r == null) return Results.NotFound();
    if (!Ops.HasScope(user, r.PropertyId)) return Results.Forbid();
    if (r.Status != "CONFIRMED") return Ops.Conflict("Đặt phòng không ở trạng thái đã xác nhận");
    if (!await Ops.LockRoom(db, r.RoomId)) return Ops.Conflict("Phòng không tồn tại");
    var room = await db.Rooms.FindAsync(r.RoomId);
    if (room == null || room.IsOutOfOrder || room.HousekeepingStatus != "INSPECTED") return Ops.Conflict("Phòng chưa sẵn sàng nhận khách");
    if (await db.Stays.AnyAsync(s => s.RoomId == r.RoomId && s.Status == "IN_HOUSE")) return Ops.Conflict("Phòng đã có khách lưu trú");
    r.Status = "CHECKED_IN";
    var stay = new Stay { ReservationId = r.Id, RoomId = r.RoomId };
    db.Stays.Add(stay);
    // DAY_USE and HOURLY are one prepaid tariff block; advanced overtime calculations are not automated in this demo.
    var nights = r.BookingMode == "OVERNIGHT" ? r.CheckOutDate.DayNumber - r.CheckInDate.DayNumber : 1;
    db.Charges.Add(new FolioCharge { ReservationId = r.Id, Category = "ROOM", Description = r.BookingMode == "OVERNIGHT" ? $"Tiền phòng {nights} đêm" : $"Gói {r.BookingMode}", Quantity = nights,
        UnitPrice = r.NightlyRate, Amount = r.NightlyRate * nights });
    await db.SaveChangesAsync();
    await Ops.Log(db, user, r.PropertyId, "CHECK_IN", "Stay", stay.Id);
    await tx.CommitAsync();
    return Results.Ok(new { stay.Id, r.Code, r.Status });
});
api.MapGet("/stays", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    return Results.Ok(await (from stay in db.Stays join r in db.Reservations on stay.ReservationId equals r.Id
        join g in db.Guests on r.GuestId equals g.Id join room in db.Rooms on stay.RoomId equals room.Id
        where r.PropertyId == propertyId && stay.Status == "IN_HOUSE" orderby stay.CheckedInAtUtc descending
        select new { stay.Id, stay.ReservationId, r.Code, g.FullName, g.Phone, RoomNumber = room.Number, r.CheckInDate, r.CheckOutDate, r.NightlyRate,
            Charges = db.Charges.Where(x => x.ReservationId == r.Id).Sum(x => (decimal?)x.Amount) ?? 0,
            Payments = db.Payments.Where(x => x.ReservationId == r.Id).Sum(x => (decimal?)x.Amount) ?? 0 }).ToListAsync());
});
api.MapGet("/stays/{stayId:int}/folio", async (int stayId, ClaimsPrincipal user, PmsDbContext db) => {
    var details = await (from s in db.Stays join r in db.Reservations on s.ReservationId equals r.Id
        join g in db.Guests on r.GuestId equals g.Id join room in db.Rooms on s.RoomId equals room.Id
        where s.Id == stayId select new { s.Id, s.ReservationId, s.Status, r.PropertyId, r.Code, g.FullName, g.Phone, RoomNumber = room.Number, r.CheckInDate, r.CheckOutDate }).FirstOrDefaultAsync();
    if (details == null) return Results.NotFound();
    if (!Ops.HasScope(user, details.PropertyId)) return Results.Forbid();
    var charges = await db.Charges.Where(x => x.ReservationId == details.ReservationId).OrderBy(x => x.PostedAtUtc).ToListAsync();
    var payments = await db.Payments.Where(x => x.ReservationId == details.ReservationId).OrderBy(x => x.PaidAtUtc).ToListAsync();
    return Results.Ok(new { details, charges, payments, total = charges.Sum(c => c.Amount), paid = payments.Sum(p => p.Amount),
        balance = charges.Sum(c => c.Amount) - payments.Sum(p => p.Amount) });
});
api.MapGet("/services", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) =>
    Ops.HasScope(user, propertyId) ? (IResult)Results.Ok(await db.Services.Where(x => x.PropertyId == propertyId).OrderBy(x => x.Name).ToListAsync()) : Results.Forbid());
api.MapPost("/services", async (ServiceInput req, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.CanManage(user) || !Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(req.Name) || req.Price < 0 || req.StockQuantity < 0) return Ops.Fail("Dữ liệu dịch vụ không hợp lệ");
    if (await db.Services.AnyAsync(x => x.PropertyId == req.PropertyId && x.Code == req.Code)) return Ops.Conflict("Trùng mã dịch vụ");
    var svc = new ServiceItem { PropertyId = req.PropertyId, Code = req.Code.Trim().ToUpperInvariant(), Name = req.Name.Trim(), Unit = req.Unit,
        Price = req.Price, TrackInventory = req.TrackInventory, StockQuantity = req.StockQuantity };
    db.Services.Add(svc); await db.SaveChangesAsync(); await Ops.Log(db, user, req.PropertyId, "SERVICE_CREATE", "Service", svc.Id);
    return Results.Ok(svc);
});
api.MapPost("/services/{id:int}/restock", async (int id, RestockInput req, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.CanManage(user)) return Results.Forbid();
    if (req.Quantity < 1 || req.Quantity > 100000) return Ops.Fail("Số lượng nhập kho không hợp lệ");
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    if (!await Ops.LockService(db, id)) return Results.NotFound();
    var svc = await db.Services.FindAsync(id); if (svc == null) return Results.NotFound();
    if (!Ops.HasScope(user, svc.PropertyId)) return Results.Forbid();
    if (!svc.TrackInventory || req.Quantity < 1) return Ops.Fail("Chỉ nhập kho dịch vụ theo dõi tồn, số lượng > 0");
    svc.StockQuantity += req.Quantity;
    db.StockMovements.Add(new StockMovement { PropertyId = svc.PropertyId, ServiceItemId = svc.Id, Type = "IN", QuantityChange = req.Quantity,
        BalanceAfter = svc.StockQuantity, Reference = "LEGACY_RESTOCK", ActorUserId = Ops.UserId(user) });
    await db.SaveChangesAsync();
    await Ops.Log(db, user, svc.PropertyId, "SERVICE_RESTOCK", "Service", svc.Id, $"+{req.Quantity}");
    await tx.CommitAsync(); return Results.Ok(svc);
});
api.MapPost("/stays/{stayId:int}/services", async (int stayId, PostServiceInput req, ClaimsPrincipal user, PmsDbContext db) => {
    if (req.Quantity < 1 || req.Quantity > 500) return Ops.Fail("Số lượng phải từ 1 đến 500");
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    var data = await (from s in db.Stays join r in db.Reservations on s.ReservationId equals r.Id where s.Id == stayId
        select new { s.Status, r.Id, r.PropertyId }).FirstOrDefaultAsync();
    if (data == null) return Results.NotFound(); if (!Ops.HasScope(user, data.PropertyId)) return Results.Forbid();
    if (data.Status != "IN_HOUSE") return Ops.Conflict("Không thể thêm dịch vụ sau trả phòng");
    if (!await Ops.LockService(db, req.ServiceId)) return Ops.Fail("Dịch vụ không tồn tại");
    var svc = await db.Services.FirstOrDefaultAsync(x => x.Id == req.ServiceId && x.PropertyId == data.PropertyId && x.IsActive);
    if (svc == null) return Ops.Fail("Dịch vụ không tồn tại");
    if (svc.TrackInventory && svc.StockQuantity < req.Quantity) return Ops.Conflict("Không đủ số lượng tồn kho");
    if (svc.TrackInventory) svc.StockQuantity -= req.Quantity;
    var charge = new FolioCharge { ReservationId = data.Id, ServiceItemId = svc.Id, Category = "SERVICE", Description = svc.Name, Quantity = req.Quantity,
        UnitPrice = svc.Price, Amount = svc.Price * req.Quantity };
    db.Charges.Add(charge); await db.SaveChangesAsync();
    if (svc.TrackInventory) db.StockMovements.Add(new StockMovement { PropertyId = data.PropertyId, ServiceItemId = svc.Id, Type = "CONSUMPTION", QuantityChange = -req.Quantity, BalanceAfter = svc.StockQuantity, Reference = $"CHARGE-{charge.Id}", ActorUserId = Ops.UserId(user) });
    await db.SaveChangesAsync();
    await Ops.Log(db, user, data.PropertyId, "SERVICE_POST", "FolioCharge", charge.Id);
    await tx.CommitAsync(); return Results.Ok(charge);
});
api.MapGet("/shifts", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    return Results.Ok(await db.Shifts.Where(s => s.PropertyId == propertyId).OrderByDescending(s => s.OpenedAtUtc).Take(30).ToListAsync());
});
api.MapPost("/shifts/open", async (ShiftOpenInput req, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
    if (req.OpeningAmount < 0) return Ops.Fail("Quỹ đầu ca không âm");
    var uid = Ops.UserId(user);
    if (await db.Shifts.AnyAsync(x => x.PropertyId == req.PropertyId && x.UserId == uid && x.Status == "OPEN")) return Ops.Conflict("Bạn đã có ca đang mở");
    var shift = new CashShift { PropertyId = req.PropertyId, UserId = uid, OpeningAmount = req.OpeningAmount };
    db.Shifts.Add(shift); await db.SaveChangesAsync(); await Ops.Log(db, user, req.PropertyId, "SHIFT_OPEN", "CashShift", shift.Id);
    return Results.Ok(shift);
});
api.MapPost("/shifts/{id:int}/close", async (int id, ShiftCloseInput req, ClaimsPrincipal user, PmsDbContext db) => {
    var shift = await db.Shifts.FindAsync(id); if (shift == null) return Results.NotFound();
    if (!Ops.HasScope(user, shift.PropertyId) || (shift.UserId != Ops.UserId(user) && !Ops.CanManageFinance(user))) return Results.Forbid();
    if (shift.Status != "OPEN" || req.ClosingAmount < 0) return Ops.Conflict("Ca đã khóa hoặc số tiền không hợp lệ");
    shift.Status = "CLOSED"; shift.ClosingAmount = req.ClosingAmount; shift.Note = req.Note; shift.ClosedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync(); await Ops.Log(db, user, shift.PropertyId, "SHIFT_CLOSE", "CashShift", shift.Id);
    return Results.Ok(shift);
});
api.MapPost("/reservations/{id:int}/deposit", async (int id, PaymentInput req, ClaimsPrincipal user, PmsDbContext db) => {
    var r = await db.Reservations.FindAsync(id); if (r == null) return Results.NotFound();
    if (!Ops.HasScope(user, r.PropertyId)) return Results.Forbid();
    if (r.Status != "CONFIRMED") return Ops.Conflict("Chỉ nhận cọc đặt phòng chưa check-in");
    return await Ops.RecordPayment(db, user, r.PropertyId, r.Id, req, "DEPOSIT");
});
api.MapPost("/stays/{stayId:int}/payments", async (int stayId, PaymentInput req, ClaimsPrincipal user, PmsDbContext db) => {
    var data = await (from s in db.Stays join r in db.Reservations on s.ReservationId equals r.Id
        where s.Id == stayId select new { s.Status, r.Id, r.PropertyId }).FirstOrDefaultAsync();
    if (data == null) return Results.NotFound();
    if (!Ops.HasScope(user, data.PropertyId)) return Results.Forbid();
    if (data.Status != "IN_HOUSE") return Ops.Conflict("Lưu trú đã đóng, không thể ghi thêm thu tiền");
    return await Ops.RecordPayment(db, user, data.PropertyId, data.Id, req, "PAYMENT");
});
api.MapPost("/stays/{stayId:int}/checkout", async (int stayId, ClaimsPrincipal user, PmsDbContext db) => {
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    var stay = await db.Stays.FindAsync(stayId); if (stay == null) return Results.NotFound();
    var r = await db.Reservations.FindAsync(stay.ReservationId); if (r == null) return Results.NotFound();
    if (!Ops.HasScope(user, r.PropertyId)) return Results.Forbid();
    if (stay.Status != "IN_HOUSE") return Ops.Conflict("Khách đã trả phòng");
    var total = await db.Charges.Where(x => x.ReservationId == r.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
    var paid = await db.Payments.Where(x => x.ReservationId == r.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
    if (total != paid) return Ops.Conflict($"Chưa cân bằng folio. Còn phải thanh toán: {total - paid:N0} VND");
    stay.Status = "CHECKED_OUT"; stay.CheckedOutAtUtc = DateTime.UtcNow; r.Status = "CHECKED_OUT";
    var room = await db.Rooms.FindAsync(stay.RoomId); if (room != null) room.HousekeepingStatus = "DIRTY";
    await db.SaveChangesAsync(); await Ops.Log(db, user, r.PropertyId, "CHECK_OUT", "Stay", stay.Id);
    await tx.CommitAsync(); return Results.Ok(new { stay.Id, r.Code, stay.Status });
});
api.MapPost("/stays/{stayId:int}/transfer", async (int stayId, TransferInput req, ClaimsPrincipal user, PmsDbContext db) => {
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    var stay = await db.Stays.FindAsync(stayId); if (stay == null) return Results.NotFound();
    var r = await db.Reservations.FindAsync(stay.ReservationId); if (r == null) return Results.NotFound();
    if (!Ops.HasScope(user, r.PropertyId)) return Results.Forbid();
    if (stay.Status != "IN_HOUSE") return Ops.Conflict("Chỉ đổi phòng cho khách đang ở");
    if (stay.RoomId == req.NewRoomId) return Ops.Fail("Phòng mới phải khác phòng hiện tại");
    var newRoom = await db.Rooms.FirstOrDefaultAsync(x => x.Id == req.NewRoomId && x.PropertyId == r.PropertyId);
    if (newRoom == null || newRoom.IsOutOfOrder || newRoom.HousekeepingStatus != "INSPECTED") return Ops.Conflict("Phòng đích không sẵn sàng");
    if (await Ops.IsBooked(db, newRoom.Id, DateOnly.FromDateTime(DateTime.Today), r.CheckOutDate)) return Ops.Conflict("Phòng đích bị trùng lịch");
    var oldRoom = await db.Rooms.FindAsync(stay.RoomId);
    if (oldRoom != null) oldRoom.HousekeepingStatus = "DIRTY";
    var previous = stay.RoomId; stay.RoomId = newRoom.Id; r.RoomId = newRoom.Id;
    await db.SaveChangesAsync(); await Ops.Log(db, user, r.PropertyId, "ROOM_TRANSFER", "Stay", stay.Id, $"{previous} -> {newRoom.Id}");
    await tx.CommitAsync(); return Results.Ok(new { stay.Id, NewRoomNumber = newRoom.Number });
});
api.MapPost("/stays/{stayId:int}/extend", async (int stayId, ExtendInput req, ClaimsPrincipal user, PmsDbContext db) => {
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    var stay = await db.Stays.FindAsync(stayId); if (stay == null) return Results.NotFound();
    var r = await db.Reservations.FindAsync(stay.ReservationId); if (r == null) return Results.NotFound();
    if (!Ops.HasScope(user, r.PropertyId)) return Results.Forbid();
    if (stay.Status != "IN_HOUSE" || req.NewCheckOutDate <= r.CheckOutDate) return Ops.Fail("Ngày trả mới phải lớn hơn ngày trả hiện tại");
    if (await db.Reservations.AnyAsync(x => x.Id != r.Id && x.RoomId == stay.RoomId && x.Status == "CONFIRMED" && x.CheckInDate < req.NewCheckOutDate && x.CheckOutDate > r.CheckOutDate))
        return Ops.Conflict("Có đặt phòng khác trong thời gian muốn gia hạn");
    var nights = req.NewCheckOutDate.DayNumber - r.CheckOutDate.DayNumber;
    var charge = new FolioCharge { ReservationId = r.Id, Category = "ROOM", Description = $"Gia hạn {nights} đêm", Quantity = nights,
        UnitPrice = r.NightlyRate, Amount = nights * r.NightlyRate };
    db.Charges.Add(charge); r.CheckOutDate = req.NewCheckOutDate;
    await db.SaveChangesAsync(); await Ops.Log(db, user, r.PropertyId, "EXTEND_STAY", "Stay", stay.Id);
    await tx.CommitAsync(); return Results.Ok(new { stay.Id, r.CheckOutDate, charge.Amount });
});
api.MapGet("/agencies", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) =>
    Ops.HasScope(user, propertyId) ? (IResult)Results.Ok(await db.Agencies.Where(x => x.PropertyId == propertyId).OrderBy(x => x.Name).ToListAsync()) : Results.Forbid());
api.MapPost("/agencies", async (AgencyInput req, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.CanManage(user) || !Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
    if (req.CommissionPercent < 0 || req.CommissionPercent > 100 || string.IsNullOrWhiteSpace(req.Name)) return Ops.Fail("Đại lý hoặc tỷ lệ hoa hồng không hợp lệ");
    var item = new Agency { PropertyId = req.PropertyId, Code = req.Code.Trim(), Name = req.Name.Trim(), CommissionPercent = req.CommissionPercent,
        ContactPhone = req.ContactPhone }; db.Agencies.Add(item); await db.SaveChangesAsync();
    await Ops.Log(db, user, req.PropertyId, "AGENCY_CREATE", "Agency", item.Id); return Results.Ok(item);
});
api.MapGet("/reports/summary", async (int propertyId, DateOnly from, DateOnly to, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    if (from > to) return Ops.Fail("Khoảng ngày không hợp lệ");
    var resIds = db.Reservations.Where(r => r.PropertyId == propertyId).Select(r => r.Id);
    var start = from.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
    var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
    var charges = await db.Charges.Where(x => resIds.Contains(x.ReservationId) && x.PostedAtUtc >= start && x.PostedAtUtc < end).ToListAsync();
    var payments = await db.Payments.Where(x => resIds.Contains(x.ReservationId) && x.PaidAtUtc >= start && x.PaidAtUtc < end).ToListAsync();
    return Results.Ok(new { from, to, roomRevenue = charges.Where(x => x.Category == "ROOM").Sum(x => x.Amount),
        serviceRevenue = charges.Where(x => x.Category == "SERVICE").Sum(x => x.Amount),
        totalCharges = charges.Sum(x => x.Amount), totalCashIn = payments.Sum(x => x.Amount),
        cash = payments.Where(x => x.Method == "CASH").Sum(x => x.Amount),
        transfer = payments.Where(x => x.Method == "TRANSFER").Sum(x => x.Amount),
        reservationCount = await db.Reservations.CountAsync(x => x.PropertyId == propertyId && x.CreatedAtUtc >= start && x.CreatedAtUtc < end),
        note = "Demo: doanh thu theo ngày ghi charge, không phải doanh thu night audit/phân bổ theo đêm." });
});
api.MapGet("/audit", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.CanManage(user) || !Ops.HasScope(user, propertyId)) return Results.Forbid();
    return Results.Ok(await db.AuditEvents.Where(x => x.PropertyId == propertyId).OrderByDescending(x => x.OccurredAtUtc).Take(100).ToListAsync());
});
app.MapPmsV2();
app.Run();

public record LoginRequest(string Email, string Password);
public record PropertyInput(string Code, string Name, string? City, string? Address, string? Phone);
public record UserInput(int? PropertyId, string FullName, string Email, string Password, string Role);
public record UserUpdateInput(int? PropertyId, string FullName, string Email, string? Password, string Role, bool IsActive);
public record RoomTypeInput(int PropertyId, string Code, string Name, int Capacity, decimal BaseRate);
public record RoomInput(int PropertyId, int RoomTypeId, string Number, int Floor);
public record HousekeepingInput(string Status);
public record MaintenanceInput(bool IsOutOfOrder, string? Note);
public record ReservationInput(int PropertyId, int RoomId, string GuestName, string Phone, string? Email,
    DateOnly CheckInDate, DateOnly CheckOutDate, int Adults, int Children, decimal? NightlyRate,
    string? BookingSource, string? GroupCode, string? Note, string? BookingMode = null, int? AgencyId = null);
public record ServiceInput(int PropertyId, string Code, string Name, string Unit, decimal Price, bool TrackInventory, int StockQuantity);
public record RestockInput(int Quantity);
public record PostServiceInput(int ServiceId, int Quantity);
public record PaymentInput(decimal Amount, string Method, string? Reference, string? IdempotencyKey = null);
public record ShiftOpenInput(int PropertyId, decimal OpeningAmount);
public record ShiftCloseInput(decimal ClosingAmount, string? Note);
public record TransferInput(int NewRoomId);
public record ExtendInput(DateOnly NewCheckOutDate);
public record AgencyInput(int PropertyId, string Code, string Name, decimal CommissionPercent, string? ContactPhone);

public static class Ops
{
    private static readonly string[] DepartmentRoles = ["FRONT_DESK_AGENT", "SALES_AGENT", "HOUSEKEEPING_AGENT", "MAINTENANCE_AGENT", "ACCOUNTING_AGENT"];
    public static int UserId(ClaimsPrincipal user) => int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
    public static int? UserProperty(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue("propertyId"), out var id) ? id : null;
    public static bool HasScope(ClaimsPrincipal user, int propertyId) => propertyId > 0 && (UserProperty(user) == null || UserProperty(user) == propertyId);
    public static bool CanManage(ClaimsPrincipal user) => user.IsInRole("CHAIN_ADMIN") || user.IsInRole("PROPERTY_MANAGER");
    public static bool CanManageFinance(ClaimsPrincipal user) => CanManage(user) || user.IsInRole("ACCOUNTING_AGENT");
    public static bool IsValidRole(string role) => role is "CHAIN_ADMIN" or "PROPERTY_MANAGER" || DepartmentRoles.Contains(role);
    public static string DepartmentName(string role) => role switch {
        "CHAIN_ADMIN" => "Quản trị toàn chuỗi", "PROPERTY_MANAGER" => "Quản lý khách sạn", "FRONT_DESK_AGENT" => "Lễ tân",
        "SALES_AGENT" => "Kinh doanh", "HOUSEKEEPING_AGENT" => "Buồng phòng", "MAINTENANCE_AGENT" => "Bảo trì",
        "ACCOUNTING_AGENT" => "Kế toán", _ => "Chưa phân loại"
    };
    public static async ValueTask<object?> DepartmentAccess(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        var user = context.HttpContext.User;
        if (user.IsInRole("CHAIN_ADMIN") || user.IsInRole("PROPERTY_MANAGER") || user.IsInRole("FRONT_DESK_AGENT")) return await next(context);
        var role = user.FindFirstValue(ClaimTypes.Role) ?? "";
        var path = context.HttpContext.Request.Path.Value?.TrimEnd('/').ToLowerInvariant() ?? "";
        var method = context.HttpContext.Request.Method;
        var commonReads = method == "GET" && new[] { "/api/properties", "/api/dashboard", "/api/rooms", "/api/room-types" }.Contains(path);
        var allowed = role switch {
            "SALES_AGENT" => commonReads || (method == "GET" && new[] { "/api/availability", "/api/guests", "/api/reservations", "/api/agencies", "/api/rate-plans" }.Contains(path)) ||
                (method == "POST" && (path is "/api/reservations" or "/api/group-bookings" || Regex.IsMatch(path, @"^/api/reservations/\d+/(cancel|deposit)$"))),
            "HOUSEKEEPING_AGENT" => commonReads || (method == "PATCH" && Regex.IsMatch(path, @"^/api/rooms/\d+/housekeeping$")),
            "MAINTENANCE_AGENT" => commonReads || (method == "PATCH" && Regex.IsMatch(path, @"^/api/rooms/\d+/maintenance$")),
            "ACCOUNTING_AGENT" => commonReads || (method == "GET" && (new[] { "/api/reservations", "/api/stays", "/api/services", "/api/shifts", "/api/agencies", "/api/reports/summary", "/api/reports/chain", "/api/reports/stock", "/api/vouchers", "/api/debts", "/api/daily-closes" }.Contains(path) || Regex.IsMatch(path, @"^/api/(stays/\d+/folio|shifts/\d+/reconcile)$"))) ||
                (method == "POST" && (path is "/api/shifts/open" or "/api/vouchers" or "/api/daily-closes" || Regex.IsMatch(path, @"^/api/(shifts/\d+/close|reservations/\d+/deposit|stays/\d+/(payments|checkout))$"))),
            _ => false
        };
        return allowed ? await next(context) : Results.Forbid();
    }
    public static IResult Fail(string msg) => Results.BadRequest(new { message = msg });
    public static IResult Conflict(string msg) => Results.Conflict(new { message = msg });
    public static Task<bool> IsBooked(PmsDbContext db, int roomId, DateOnly start, DateOnly end) =>
        db.Reservations.AnyAsync(r => r.RoomId == roomId && (r.Status == "CONFIRMED" || r.Status == "CHECKED_IN") && r.CheckInDate < end && r.CheckOutDate > start);
    public static async Task Log(PmsDbContext db, ClaimsPrincipal user, int propertyId, string action, string entity, int id, string details = "") {
        db.AuditEvents.Add(new AuditEvent { PropertyId = propertyId, ActorUserId = UserId(user), Action = action, Entity = entity, EntityId = id, Details = details });
        await db.SaveChangesAsync();
    }
    public static bool ValidIdempotency(string? key) => Guid.TryParse(key, out _);
    public static async Task<bool> LockRoom(PmsDbContext db, int roomId) {
        // A room row is the serialization point for booking/waitlist/group assignments.
        if (db.Database.IsNpgsql())
            return await db.Database.SqlQuery<int>($"SELECT id AS \"Value\" FROM rooms WHERE id = {roomId} FOR UPDATE").FirstOrDefaultAsync() != 0;
        return await db.Database.SqlQuery<int>($"SELECT id AS Value FROM dbo.rooms WITH (UPDLOCK, HOLDLOCK) WHERE id = {roomId}").FirstOrDefaultAsync() != 0;
    }
    public static async Task<bool> LockService(PmsDbContext db, int serviceId) {
        // Prevent concurrent stock decrements from reading the same old balance.
        if (db.Database.IsNpgsql())
            return await db.Database.SqlQuery<int>($"SELECT id AS \"Value\" FROM services WHERE id = {serviceId} FOR UPDATE").FirstOrDefaultAsync() != 0;
        return await db.Database.SqlQuery<int>($"SELECT id AS Value FROM dbo.services WITH (UPDLOCK, HOLDLOCK) WHERE id = {serviceId}").FirstOrDefaultAsync() != 0;
    }
    public static async Task LockReservation(PmsDbContext db, int reservationId) {
        // The UPDLOCK lock is kept until COMMIT, serializing all financial writers per folio.
        if (db.Database.IsNpgsql()) {
            await db.Database.SqlQuery<int>($"SELECT id AS \"Value\" FROM reservations WHERE id = {reservationId} FOR UPDATE").SingleAsync();
            return;
        }
        await db.Database.SqlQuery<int>($"SELECT id AS Value FROM dbo.reservations WITH (UPDLOCK, HOLDLOCK) WHERE id = {reservationId}").SingleAsync();
    }
    public static async Task<IResult> RecordPayment(PmsDbContext db, ClaimsPrincipal user, int propertyId, int reservationId, PaymentInput req, string kind) {
        if (req.Amount <= 0 || req.Amount > 10000000000m) return Fail("Số tiền phải lớn hơn 0 và trong giới hạn");
        if (!new[] { "CASH", "TRANSFER", "CARD" }.Contains(req.Method)) return Fail("Phương thức thanh toán không hợp lệ");
        if (!ValidIdempotency(req.IdempotencyKey)) return Fail("idempotencyKey bắt buộc là UUID để chống ghi trùng giao dịch");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await LockReservation(db, reservationId);
        var existing = await db.Payments.FirstOrDefaultAsync(x => x.IdempotencyKey == req.IdempotencyKey);
        if (existing != null) {
            if (existing.ReservationId != reservationId || existing.Amount != req.Amount || existing.Method != req.Method || existing.Kind != kind)
                return Conflict("idempotencyKey đã dùng cho giao dịch khác");
            await tx.CommitAsync();
            return Results.Ok(existing); // Replayed request, never inserts a second payment.
        }
        var reservation = await db.Reservations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reservationId && x.PropertyId == propertyId);
        if (reservation == null) return Results.NotFound();
        if ((kind == "DEPOSIT" && reservation.Status != "CONFIRMED") ||
            (kind == "PAYMENT" && reservation.Status != "CHECKED_IN")) return Conflict("Trạng thái đặt phòng đã thay đổi");
        var total = await db.Charges.Where(x => x.ReservationId == reservationId).SumAsync(x => (decimal?)x.Amount) ?? 0;
        var paid = await db.Payments.Where(x => x.ReservationId == reservationId).SumAsync(x => (decimal?)x.Amount) ?? 0;
        if (kind == "PAYMENT" && paid + req.Amount > total) return Conflict("Thanh toán vượt số dư folio");
        if (kind == "DEPOSIT") {
            var expected = reservation.NightlyRate * (reservation.BookingMode == "OVERNIGHT" ?
                reservation.CheckOutDate.DayNumber - reservation.CheckInDate.DayNumber : 1);
            if (paid + req.Amount > expected) return Conflict("Cọc vượt giá phòng đã xác nhận");
        }
        int? shiftId = null;
        if (req.Method == "CASH") {
            var shift = await db.Shifts.FirstOrDefaultAsync(x => x.PropertyId == propertyId && x.UserId == UserId(user) && x.Status == "OPEN");
            if (shift == null) return Conflict("Cần mở ca thu ngân trước khi thu tiền mặt");
            shiftId = shift.Id;
        }
        var payment = new Payment { ReservationId = reservationId, ShiftId = shiftId, Amount = req.Amount, Method = req.Method,
            Reference = req.Reference?.Trim(), Kind = kind, IdempotencyKey = req.IdempotencyKey };
        db.Payments.Add(payment); await db.SaveChangesAsync();
        await Log(db, user, propertyId, "PAYMENT_" + kind, "Payment", payment.Id, req.Method);
        await tx.CommitAsync(); return Results.Ok(payment);
    }
}
