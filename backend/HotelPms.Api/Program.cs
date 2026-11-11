using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using FluentValidation;
using HotelPms.Api;
using MediatR;
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
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CheckStayQuoteHandler>());
builder.Services.AddValidatorsFromAssemblyContaining<CheckStayQuoteValidator>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
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
app.MapGet("/api/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/api/health/ready", async (PmsDbContext db, ILogger<PmsDbContext> logger, CancellationToken ct) => {
    // Touches the V5 columns/tables so a deploy fails readiness until sql/06-v5-foundation-postgres.sql is applied.
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
    timeout.CancelAfter(TimeSpan.FromSeconds(5));
    try {
        await db.Database.ExecuteSqlRawAsync(
            "SELECT p.time_zone_id, p.fax, p.email, p.website, r.rate_plan_snapshot, r.deposit_required_amount, r.rate_confidential, r.loyalty_tier, a.id, t.id, h.id, e.id, cc.id, cm.id, cb.fingerprint, cl.id, g.title, g.gift_note, g.preferences, rt.weekday_rate, rt.min_nightly_rate, rt.weekly_rate, ht.priority, dn.id, mv.id, ge.id, bn.id, hd.id, nl.id, rf.id, sh.id, au.permissions_customized, up.permission_key, vf.id, d.origin, ca.id, mvn.id, gc.id, pds.id, gmf.id FROM properties p, reservations r, adjustment_vouchers a, tax_rules t, holiday_rates h, e_invoices e, channel_connections cc, channel_room_mappings cm, channel_bookings cb, channel_sync_logs cl, guests g, room_types rt, housekeeping_tasks ht, desk_notes dn, room_transfer_histories mv, group_membership_events ge, booking_note_types bn, reservation_holds hd, night_audit_lines nl, payment_refunds rf, shift_handovers sh, app_users au, user_permissions up, virtual_folios vf, debt_receivables d, cash_accounts ca, cash_movements mvn, group_cutoffs gc, property_day_snapshots pds, group_master_folios gmf WHERE 1 = 0",
            timeout.Token);
        return Results.Ok(new { status = "ready", database = "ok" });
    } catch (Exception ex) {
        logger.LogWarning("Readiness check failed: {ErrorType}", ex.GetType().Name);
        return Results.Json(new { status = "unavailable", database = "error" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
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
    var permissions = user.PermissionsCustomized
        ? await db.UserPermissions.Where(x => x.UserId == user.Id).Select(x => x.PermissionKey).ToListAsync()
        : StaffAccess.TemplateOf(user.Role).ToList();
    return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token), user = new {
        user.Id, user.FullName, user.Email, user.Role, user.PropertyId, permissions
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
    var contact = Ops.ReadContact(req.Fax, req.Email, req.Website);
    if (contact.Error != null) return Ops.Fail(contact.Error);
    var property = new Property { Code = req.Code.Trim().ToUpperInvariant(), Name = req.Name.Trim(), City = req.City?.Trim() ?? "",
        Address = req.Address?.Trim() ?? "", Phone = req.Phone?.Trim() ?? "", Fax = contact.Fax, Email = contact.Email, Website = contact.Website };
    db.Properties.Add(property); await db.SaveChangesAsync();
    await Ops.Log(db, user, property.Id, "PROPERTY_CREATE", "Property", property.Id);
    return Results.Ok(property);
});
api.MapPatch("/properties/{id:int}/contact", async (int id, PropertyContactInput req, ClaimsPrincipal actor, PmsDbContext db) => {
    if (!Ops.CanManage(actor) || !Ops.HasScope(actor, id)) return Results.Forbid();
    var property = await db.Properties.FindAsync(id);
    if (property == null) return Results.NotFound();
    var contact = Ops.ReadContact(req.Fax, req.Email, req.Website);
    if (contact.Error != null) return Ops.Fail(contact.Error);
    property.Fax = contact.Fax; property.Email = contact.Email; property.Website = contact.Website;
    await db.SaveChangesAsync();
    await Ops.Log(db, actor, property.Id, "PROPERTY_CONTACT", "Property", property.Id);
    return Results.Ok(new { property.Id, property.Fax, property.Email, property.Website });
});
api.MapGet("/users", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.CanManage(user) || !Ops.HasScope(user, propertyId)) return Results.Forbid();
    var users = await db.Users.Where(x => x.PropertyId == propertyId ||
        (user.IsInRole("CHAIN_ADMIN") && x.PropertyId == null && x.Role == "CHAIN_ADMIN"))
        .OrderBy(x => x.Id).ToListAsync();
    return Results.Ok(users.Select(x => new { x.Id, x.FullName, x.Email, x.Role, Department = Ops.DepartmentName(x.Role), x.PropertyId, x.IsActive, x.PermissionsCustomized }));
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
    var roleChanged = item.Role != req.Role;
    item.FullName = req.FullName.Trim(); item.Email = email; item.Role = req.Role;
    if (roleChanged) {
        item.PermissionsCustomized = false;
        db.UserPermissions.RemoveRange(db.UserPermissions.Where(x => x.UserId == item.Id));
    }
    item.PropertyId = req.Role == "CHAIN_ADMIN" ? null : req.PropertyId;
    item.IsActive = req.IsActive;
    if (!string.IsNullOrWhiteSpace(req.Password)) {
        if (req.Password.Length < 8) return Ops.Fail("Mật khẩu tối thiểu 8 ký tự");
        item.PasswordHash = hasher.HashPassword(item, req.Password);
    }
    await db.SaveChangesAsync();
    StaffAccess.Forget(item.Id);
    var auditPropertyId = item.PropertyId ?? await db.Properties.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
    await Ops.Log(db, actor, auditPropertyId, "USER_UPDATE", "AppUser", item.Id);
    return Results.Ok(new { item.Id, item.FullName, item.Email, item.Role, Department = Ops.DepartmentName(item.Role), item.PropertyId, item.IsActive, item.PermissionsCustomized });
});
api.MapGet("/users/{id:int}/permissions", async (int id, ClaimsPrincipal actor, PmsDbContext db) => {
    if (!Ops.CanManage(actor)) return Results.Forbid();
    var item = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (item == null) return Results.NotFound();
    if (item.PropertyId.HasValue ? !Ops.HasScope(actor, item.PropertyId.Value) : !actor.IsInRole("CHAIN_ADMIN")) return Results.Forbid();
    var keys = item.PermissionsCustomized
        ? await db.UserPermissions.Where(x => x.UserId == id).Select(x => x.PermissionKey).ToListAsync()
        : StaffAccess.TemplateOf(item.Role).ToList();
    return Results.Ok(new { item.PermissionsCustomized, keys, template = StaffAccess.TemplateOf(item.Role), catalog = StaffAccess.Catalog });
});
api.MapPut("/users/{id:int}/permissions", async (int id, PermissionInput req, ClaimsPrincipal actor, PmsDbContext db) => {
    if (!Ops.CanManage(actor)) return Results.Forbid();
    if (id == Ops.UserId(actor)) return Ops.Conflict("Không tự sửa quyền tài khoản đang đăng nhập");
    var item = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
    if (item == null) return Results.NotFound();
    if (item.PropertyId.HasValue ? !Ops.HasScope(actor, item.PropertyId.Value) : !actor.IsInRole("CHAIN_ADMIN")) return Results.Forbid();
    var existing = await db.UserPermissions.Where(x => x.UserId == id).ToListAsync();
    db.UserPermissions.RemoveRange(existing);
    if (req.Reset) item.PermissionsCustomized = false;
    else {
        var allowed = StaffAccess.Catalog.Select(x => x.Key).ToHashSet();
        var chosen = (req.Keys ?? []).Where(allowed.Contains).Distinct().ToList();
        if (!actor.IsInRole("CHAIN_ADMIN")) chosen.RemoveAll(k => k is "price.guard" or "chain.property");
        foreach (var key in chosen) db.UserPermissions.Add(new UserPermission { UserId = id, PermissionKey = key });
        item.PermissionsCustomized = true;
    }
    await db.SaveChangesAsync();
    StaffAccess.Forget(id);
    var auditPropertyId = item.PropertyId ?? await db.Properties.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
    await Ops.Log(db, actor, auditPropertyId, "USER_PERMISSIONS", "AppUser", item.Id);
    var keys = item.PermissionsCustomized
        ? await db.UserPermissions.Where(x => x.UserId == id).Select(x => x.PermissionKey).ToListAsync()
        : StaffAccess.TemplateOf(item.Role).ToList();
    return Results.Ok(new { item.PermissionsCustomized, keys });
});
api.MapGet("/dashboard", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    var zone = await db.Properties.Where(x => x.Id == propertyId).Select(x => x.TimeZoneId).FirstOrDefaultAsync();
    var today = V5Closeout.PropertyToday(zone);
    var nightEnd = today.AddDays(1);
    var operating = await db.Rooms.Where(x => x.PropertyId == propertyId && !x.IsOutOfOrder).Select(x => new { x.Id, x.HousekeepingStatus }).ToListAsync();
    var readyIds = operating.Where(x => x.HousekeepingStatus == "INSPECTED").Select(x => x.Id).ToList();
    var stayedRoomIds = await db.Reservations.Where(r => r.PropertyId == propertyId && readyIds.Contains(r.RoomId)
        && (r.Status == "CONFIRMED" || r.Status == "CHECKED_IN") && r.CheckInDate < nightEnd && r.CheckOutDate > today)
        .Select(r => r.RoomId).Distinct().ToListAsync();
    var heldRoomIds = await db.ReservationHolds.Where(h => h.PropertyId == propertyId && h.Status == "ACTIVE" && h.ExpiresAtUtc > DateTime.UtcNow
        && readyIds.Contains(h.RoomId) && h.CheckInDate < nightEnd && h.CheckOutDate > today)
        .Select(h => h.RoomId).Distinct().ToListAsync();
    var rooms = operating.Count;
    var available = readyIds.Count(id => !stayedRoomIds.Contains(id) && !heldRoomIds.Contains(id));
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
    return Results.Ok(new { rooms, inHouse, available, arrivals, departures, dirty,
        occupancy = rooms == 0 ? 0 : Math.Round(inHouse * 100m / rooms, 1), charges, receipts, recent });
});
api.MapGet("/room-types", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) =>
    Ops.HasScope(user, propertyId) ? (IResult)Results.Ok(await CatalogCache.RoomTypesFor(db, propertyId)) : Results.Forbid());
api.MapPost("/room-types", async (RoomTypeInput req, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.CanManage(user) || !Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
    if (req.BaseRate < 0 || req.Capacity < 1 || string.IsNullOrWhiteSpace(req.Name)) return Ops.Fail("Thông tin loại phòng không hợp lệ");
    if (await db.RoomTypes.AnyAsync(x => x.PropertyId == req.PropertyId && x.Code == req.Code)) return Ops.Conflict("Mã loại phòng đã tồn tại");
    var item = new RoomType { PropertyId = req.PropertyId, Code = req.Code.Trim().ToUpperInvariant(), Name = req.Name.Trim(), Capacity = req.Capacity, BaseRate = req.BaseRate,
        WeekdayRate = req.WeekdayRate, WeekendRate = req.WeekendRate, HourlyRate = req.HourlyRate, ExtraHourRate = req.ExtraHourRate, ExtraPersonRate = req.ExtraPersonRate, MaxDiscountPercent = 15 };
    db.RoomTypes.Add(item); await db.SaveChangesAsync(); CatalogCache.ForgetRoomTypes(req.PropertyId); await Ops.Log(db, user, req.PropertyId, "ROOM_TYPE_CREATE", "RoomType", item.Id);
    return Results.Ok(item);
});
api.MapGet("/rooms", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    var held = (await db.ReservationHolds.Where(h => h.PropertyId == propertyId && h.Status == "ACTIVE" && h.ExpiresAtUtc > DateTime.UtcNow).Select(h => h.RoomId).ToListAsync()).ToHashSet();
    var rooms = await (from rm in db.Rooms join rt in db.RoomTypes on rm.RoomTypeId equals rt.Id
        where rm.PropertyId == propertyId orderby rm.Floor, rm.Number
        select new { rm.Id, rm.PropertyId, rm.RoomTypeId, rm.BuildingId, rm.Number, rm.Floor, rm.HousekeepingStatus, rm.IsOutOfOrder, rm.Note,
            RoomTypeName = rt.Name, rt.BaseRate, rt.Capacity, rt.WeekdayRate, rt.WeekendRate, rt.HourlyRate, rt.ExtraHourRate, rt.ExtraPersonRate,
            Current = (from r in db.Reservations join g in db.Guests on r.GuestId equals g.Id
                       where r.RoomId == rm.Id && r.Status == "CHECKED_IN"
                       select new { r.Id, r.Code, g.FullName, r.CheckOutDate }).FirstOrDefault(),
            Next = (from r in db.Reservations where r.RoomId == rm.Id && r.Status == "CONFIRMED"
                    orderby r.CheckInDate select new { r.Id, r.Code, r.CheckInDate }).FirstOrDefault()
        }).ToListAsync();
    return Results.Ok(rooms.Select(x => new {
        x.Id, x.PropertyId, x.RoomTypeId, x.BuildingId, x.Number, x.Floor, x.HousekeepingStatus, x.IsOutOfOrder, x.Note,
        x.RoomTypeName, x.BaseRate, x.WeekdayRate, x.WeekendRate, x.HourlyRate, x.ExtraHourRate, x.ExtraPersonRate, x.Capacity, x.Current, x.Next,
        DisplayStatus = x.IsOutOfOrder ? "MAINTENANCE" : x.Current != null ? "OCCUPIED" : held.Contains(x.Id) ? "HOLD" : x.HousekeepingStatus == "DIRTY" ? "DIRTY" : x.HousekeepingStatus == "CLEAN" ? "CLEAN" : x.Next != null && x.Next.CheckInDate == DateOnly.FromDateTime(DateTime.Today) ? "ARRIVAL" : "VACANT"
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
    var openTask = await db.HousekeepingTasks.Where(t => t.RoomId == room.Id && t.Status != "FINISH").OrderByDescending(t => t.Id).FirstOrDefaultAsync();
    if (req.Status == "INSPECTED" && openTask != null && await db.HousekeepingTaskItems.AnyAsync(i => i.TaskId == openTask.Id && i.IsRequired && !i.IsDone))
        return Ops.Fail("Còn hạng mục bắt buộc chưa xong, chưa nghiệm thu");
    if (req.Status == "INSPECTED" && openTask != null && await db.HousekeepingTaskItems.AnyAsync(i => i.TaskId == openTask.Id) && !Ops.CanManage(user))
        return Ops.Fail("Chỉ quản lý nghiệm thu phòng");
    if (req.Status == "INSPECTED" && (openTask == null || !await db.HousekeepingTaskMedia.AnyAsync(m => m.TaskId == openTask.Id)))
        return Ops.Fail("Cần ảnh nghiệm thu trước khi trả phòng về sẵn sàng");
    if (req.Status == "DIRTY" && openTask == null) {
        var inHouse = await db.Reservations.AnyAsync(r => r.RoomId == room.Id && r.Status == "CHECKED_IN");
        await V5Closeout.EnsureCleaningTask(db, room, inHouse ? "STAY" : "CHECKOUT");
    } else if (req.Status == "CLEAN" && openTask != null) {
        openTask.Status = "PENDING"; openTask.FinishedAtUtc = DateTime.UtcNow;
    } else if (req.Status == "INSPECTED" && openTask != null) {
        openTask.Status = "FINISH"; openTask.InspectorName = user.FindFirstValue(ClaimTypes.Name); openTask.FinishedAtUtc ??= DateTime.UtcNow;
    }
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
api.MapGet("/housekeeping-tasks", async (int propertyId, DateOnly? from, DateOnly? to, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    var rangeError = Ops.DateWindow(from, to, out var start, out var end);
    if (rangeError != null) return Ops.Fail(rangeError);
    var tasks = db.HousekeepingTasks.Where(t => t.PropertyId == propertyId);
    if (from.HasValue) tasks = tasks.Where(t => t.StartedAtUtc >= start && t.StartedAtUtc < end);
    var rows = await (from t in tasks join room in db.Rooms on t.RoomId equals room.Id
        orderby t.Id descending
        select new { t.Id, t.RoomId, RoomNumber = room.Number, room.Floor, t.Kind, t.Status, t.Priority, t.ShiftCode, t.StartedAtUtc, t.FinishedAtUtc, t.Cleaner1, t.Cleaner2, t.Cleaner3, t.InspectorName, t.Note }).Take(from.HasValue ? 300 : 80).ToListAsync();
    var ids = rows.Select(x => x.Id).ToArray();
    var items = await db.HousekeepingTaskItems.Where(i => ids.Contains(i.TaskId)).ToListAsync();
    var photos = await db.HousekeepingTaskMedia.Where(m => ids.Contains(m.TaskId)).GroupBy(m => m.TaskId).Select(g => new { TaskId = g.Key, Count = g.Count() }).ToListAsync();
    return Results.Ok(rows.Select(t => new { t.Id, t.RoomId, t.RoomNumber, t.Floor, t.Kind, t.Status, t.Priority, t.ShiftCode, t.StartedAtUtc, t.FinishedAtUtc, t.Cleaner1, t.Cleaner2, t.Cleaner3, t.InspectorName, t.Note, photoCount = photos.FirstOrDefault(p => p.TaskId == t.Id)?.Count ?? 0, items = items.Where(i => i.TaskId == t.Id).Select(i => new { i.Id, i.ChecklistCode, i.Label, i.IsRequired, i.IsDone }) }));
});
api.MapPatch("/housekeeping-tasks/{id:int}", async (int id, HousekeepingTaskInput req, ClaimsPrincipal user, PmsDbContext db) => {
    var task = await db.HousekeepingTasks.FindAsync(id);
    if (task == null) return Results.NotFound();
    if (!Ops.HasScope(user, task.PropertyId)) return Results.Forbid();
    task.Cleaner1 = Ops.TrimOrNull(req.Cleaner1); task.Cleaner2 = Ops.TrimOrNull(req.Cleaner2); task.Cleaner3 = Ops.TrimOrNull(req.Cleaner3); task.Note = Ops.TrimOrNull(req.Note);
    if (req.Priority is "LOW" or "NORMAL" or "HIGH") task.Priority = req.Priority;
    if (req.ShiftCode is "MORNING" or "AFTERNOON" or "NIGHT") task.ShiftCode = req.ShiftCode;
    await db.SaveChangesAsync();
    return Results.Ok(task);
});
api.MapGet("/desk-notes", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    return Results.Ok(await db.DeskNotes.Where(n => n.PropertyId == propertyId && n.Status == "OPEN").OrderByDescending(n => n.Id).Take(20).ToListAsync());
});
api.MapPost("/desk-notes", async (DeskNoteInput req, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(req.Text) || req.Text.Trim().Length > 240) return Ops.Fail("Nội dung ghi chú không hợp lệ");
    var note = new DeskNote { PropertyId = req.PropertyId, Text = req.Text.Trim(), CreatedByUserId = Ops.UserId(user) };
    db.DeskNotes.Add(note); await db.SaveChangesAsync();
    return Results.Ok(note);
});
api.MapPost("/desk-notes/{id:int}/done", async (int id, ClaimsPrincipal user, PmsDbContext db) => {
    var note = await db.DeskNotes.FindAsync(id);
    if (note == null) return Results.NotFound();
    if (!Ops.HasScope(user, note.PropertyId)) return Results.Forbid();
    note.Status = "DONE"; await db.SaveChangesAsync();
    return Results.Ok(note);
});
api.MapPost("/pricing/room-quote", async (RoomCardQuoteRequest req, ClaimsPrincipal user, PmsDbContext db, IMediator mediator) => {
    if (!Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
    if (req.CheckInDate >= req.CheckOutDate || req.Adults < 1) return Ops.Fail("Khoảng ngày và số khách phải hợp lệ");
    var room = await db.Rooms.FirstOrDefaultAsync(x => x.Id == req.RoomId && x.PropertyId == req.PropertyId);
    if (room == null) return Results.NotFound();
    var type = await db.RoomTypes.FirstAsync(x => x.Id == room.RoomTypeId);
    var mode = req.BookingMode ?? "OVERNIGHT";
    RatePlan? plan = null;
    if (req.RatePlanId is int planId)
    {
        plan = await db.RatePlans.FirstOrDefaultAsync(x => x.Id == planId && x.PropertyId == req.PropertyId && x.IsActive);
        if (plan == null) return Ops.Fail("Bảng giá không thuộc cơ sở");
        mode = plan.Mode;
    }
    if (mode is not ("OVERNIGHT" or "DAY_USE" or "HOURLY" or "WEEK" or "MONTH")) return Ops.Fail("Loại lưu trú không hợp lệ");
    var packageError = await mediator.Send(new CheckStayQuote(mode, req.CheckInDate, req.CheckOutDate));
    if (packageError != null) return Ops.Fail(packageError);
    var (_, cardUnit) = RoomCardPricing.Build(type, plan, mode, req.CheckInDate, req.CheckOutDate, req.Adults, req.Children, req.PlannedHours, null);
    var percent = Loyalty.ClampPercent(req.LoyaltyPercent, req.LoyaltyTier);
    var evidence = await Loyalty.LookUp(db, req.PropertyId, req.Phone, req.GiftNote);
    var weekday = plan?.BasePrice ?? type.WeekdayRate ?? type.BaseRate;
    var weekend = plan != null ? plan.BasePrice : type.WeekendRate ?? type.WeekdayRate ?? type.BaseRate;
    var nightCards = mode == "OVERNIGHT" ? RoomCardPricing.Cards(req.CheckInDate, req.CheckOutDate, weekday, weekend, type, req.RoomRateOverride is null ? percent : 0) : [];
    decimal? priced = req.RoomRateOverride;
    var clamped = false;
    string? nightReject = null;
    List<RoomCardPricing.Line> lines;
    decimal roomUnit;
    if (mode == "OVERNIGHT" && req.NightRates is { Length: > 0 })
    {
        nightReject = RoomCardPricing.RejectNights(nightCards, req.NightRates);
        var (baseLines, _) = RoomCardPricing.Build(type, plan, mode, req.CheckInDate, req.CheckOutDate, req.Adults, req.Children, req.PlannedHours, null);
        lines = nightReject == null ? RoomCardPricing.WithNights(baseLines, req.NightRates) : baseLines;
        roomUnit = nightReject == null ? decimal.Round(req.NightRates.Average(x => x.Amount), 0, MidpointRounding.AwayFromZero) : baseLines.Where(x => x.Category == "ROOM").Select(x => x.UnitPrice).FirstOrDefault();
    }
    else
    {
        if (req.RoomRateOverride is null && percent > 0)
        {
            var floor = V5Desk.Floor(type, mode, cardUnit);
            var raw = decimal.Round(cardUnit * (1m - percent / 100m), 0, MidpointRounding.AwayFromZero);
            priced = Loyalty.Payable(cardUnit, percent, floor);
            clamped = priced > raw;
        }
        (lines, roomUnit) = RoomCardPricing.Build(type, plan, mode, req.CheckInDate, req.CheckOutDate, req.Adults, req.Children, req.PlannedHours, priced);
    }
    return Results.Ok(new { mode, roomUnit, cardUnit, lines, total = lines.Sum(x => x.Amount), locked = true, floor = V5Desk.Floor(type, mode, cardUnit),
        rejected = nightReject ?? V5Desk.Guard(type, mode, cardUnit, req.RoomRateOverride is null || req.NightRates is { Length: > 0 } ? null : req.RoomRateOverride),
        nights = nightCards, loyalty = new { code = Loyalty.CardFor(percent, req.LoyaltyTier), label = Loyalty.LabelFor(percent), percent = req.RoomRateOverride is null && req.NightRates is not { Length: > 0 } ? percent : 0, evidence.StayCount, evidence.Qualified, evidence.Reason, evidence.GiftNote, clamped } });
});
api.MapGet("/availability", async (int propertyId, DateOnly startDate, DateOnly endDate, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    if (startDate >= endDate) return Ops.Fail("Ngày trả phải sau ngày nhận");
    await db.ReservationHolds.Where(h => h.PropertyId == propertyId && h.Status == "ACTIVE" && h.ExpiresAtUtc <= DateTime.UtcNow)
        .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, "EXPIRED").SetProperty(h => h.ReleasedAtUtc, DateTime.UtcNow));
    var rooms = await (from room in db.Rooms join type in db.RoomTypes on room.RoomTypeId equals type.Id
        where room.PropertyId == propertyId && !room.IsOutOfOrder && (startDate > DateOnly.FromDateTime(DateTime.Today) || room.HousekeepingStatus == "INSPECTED") &&
        !db.Reservations.Any(r => r.RoomId == room.Id && (r.Status == "CONFIRMED" || r.Status == "CHECKED_IN") && r.CheckInDate < endDate && r.CheckOutDate > startDate) &&
        !db.ReservationHolds.Any(h => h.RoomId == room.Id && h.Status == "ACTIVE" && h.ExpiresAtUtc > DateTime.UtcNow && h.CheckInDate < endDate && h.CheckOutDate > startDate)
        orderby room.Floor, room.Number select new { room.Id, room.Number, room.Floor, room.RoomTypeId, RoomTypeName = type.Name, type.BaseRate, type.WeekdayRate, type.WeekendRate, type.HourlyRate, type.ExtraHourRate, type.ExtraPersonRate, type.Capacity }).ToListAsync();
    return Results.Ok(rooms);
});
api.MapGet("/guests", async (int propertyId, string? q, DateOnly? from, DateOnly? to, int? page, int? pageSize, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    var rangeError = Ops.DateWindow(from, to, out _, out _);
    if (rangeError != null) return Ops.Fail(rangeError);
    var staysInRange = db.Reservations.Where(r => r.PropertyId == propertyId);
    if (from.HasValue) staysInRange = staysInRange.Where(r => r.CheckInDate <= to && r.CheckOutDate > from);
    var guestIds = staysInRange.Select(r => r.GuestId);
    var guests = db.Guests.Where(g => guestIds.Contains(g.Id));
    if (!string.IsNullOrWhiteSpace(q)) guests = guests.Where(g => g.FullName.Contains(q) || g.Phone.Contains(q));
    var ordered = guests.OrderByDescending(g => g.CreatedAtUtc);
    var paged = page is > 0;
    var size = pageSize is > 0 and <= 100 ? pageSize.Value : 25;
    var total = paged ? await ordered.CountAsync() : 0;
    var list = paged
        ? await ordered.Skip((page!.Value - 1) * size).Take(size).ToListAsync()
        : await ordered.Take(from.HasValue ? 500 : 100).ToListAsync();
    var debt = await V5Desk.OpenDebtByGuest(db, list.Select(g => g.Id));
    var phones = list.Where(g => g.Phone != "").Select(g => g.Phone).Distinct().ToArray();
    var stays = new Dictionary<string, int>();
    if (phones.Length > 0)
    {
        var stayRows = await db.Reservations.Where(r => r.PropertyId == propertyId && (r.Status == "CHECKED_IN" || r.Status == "CHECKED_OUT"))
            .Join(db.Guests.Where(g => phones.Contains(g.Phone)), r => r.GuestId, g => g.Id, (r, g) => g.Phone)
            .GroupBy(phone => phone).Select(grp => new { Phone = grp.Key, Count = grp.Count() }).ToListAsync();
        stays = stayRows.ToDictionary(x => x.Phone, x => x.Count);
    }
    var rows = list.Select(g => new { g.Id, g.FullName, g.Phone, g.Email, g.Nationality, g.IdentityLast4, g.Title, g.Gender, g.IdDocumentType, g.Ethnicity, g.Religion, g.StayPurpose, g.LoyaltyCard, loyaltyLabel = Loyalty.LabelOf(g.LoyaltyCard), g.GiftNote, g.Preferences, g.CreatedAtUtc,
        stayCount = stays.GetValueOrDefault(g.Phone), loyal = stays.GetValueOrDefault(g.Phone) >= Loyalty.StayMark || !string.IsNullOrWhiteSpace(g.GiftNote),
        outstandingDebt = debt.GetValueOrDefault(g.Id) });
    return paged ? Results.Ok(new { items = rows, total, page, pageSize = size }) : Results.Ok(rows);
});
api.MapGet("/guests/loyalty-check", async (int propertyId, string? phone, string? giftNote, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    var evidence = await Loyalty.LookUp(db, propertyId, phone, giftNote);
    return Results.Ok(new { evidence.GuestId, evidence.FullName, tier = evidence.Tier, tierLabel = Loyalty.LabelOf(evidence.Tier), percent = Loyalty.Find(evidence.Tier).Percent, evidence.GiftNote, evidence.Preferences, evidence.StayCount, evidence.Qualified, evidence.Reason, tiers = Loyalty.Tiers });
});
api.MapPatch("/guests/{id:int}/loyalty", async (int id, LoyaltyUpdate req, ClaimsPrincipal user, PmsDbContext db) => {
    var guest = await db.Guests.FirstOrDefaultAsync(g => g.Id == id);
    if (guest == null) return Results.NotFound();
    var propertyId = await db.Reservations.Where(r => r.GuestId == id).Select(r => r.PropertyId).FirstOrDefaultAsync();
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    var percent = Loyalty.ClampPercent(req.Percent, req.Tier);
    var evidence = await Loyalty.LookUp(db, propertyId, guest.Phone, req.GiftNote ?? guest.GiftNote);
    guest.LoyaltyCard = Loyalty.CardFor(percent, req.Tier);
    if (req.GiftNote != null) guest.GiftNote = string.IsNullOrWhiteSpace(req.GiftNote) ? null : req.GiftNote.Trim();
    await db.SaveChangesAsync();
    return Results.Ok(new { guest.Id, guest.LoyaltyCard, loyaltyLabel = Loyalty.LabelOf(guest.LoyaltyCard), guest.GiftNote, evidence.StayCount, evidence.Qualified, evidence.Reason });
});
api.MapGet("/reservations", async (int propertyId, string? status, DateOnly? from, DateOnly? to, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    var rangeError = Ops.DateWindow(from, to, out _, out _);
    if (rangeError != null) return Ops.Fail(rangeError);
    var query = from r in db.Reservations join g in db.Guests on r.GuestId equals g.Id
        join room in db.Rooms on r.RoomId equals room.Id where r.PropertyId == propertyId
        select new { r.Id, r.Code, r.PropertyId, r.RoomId, r.GuestId, g.FullName, g.Phone, RoomNumber = room.Number,
            r.CheckInDate, r.CheckOutDate, r.NightlyRate, r.Adults, r.Children, r.Status, r.BookingSource, r.BookingMode, r.GroupCode, r.Note, r.CreatedAtUtc,
            r.DepositRequiredAmount, r.DepositDueAtUtc, r.RateConfidential,
            DepositReceived = db.Payments.Where(p => p.ReservationId == r.Id && p.Kind == "DEPOSIT").Sum(p => (decimal?)p.Amount) ?? 0,
            StayId = db.Stays.Where(s => s.ReservationId == r.Id).Select(s => (int?)s.Id).FirstOrDefault() };
    if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
    if (from.HasValue) query = query.Where(x => x.CheckInDate <= to && x.CheckOutDate > from);
    var reservations = await query.OrderByDescending(x => x.CheckInDate).ThenByDescending(x => x.Id).Take(from.HasValue ? 500 : 250).ToListAsync();
    var guestDebt = await V5Desk.OpenDebtByGuest(db, reservations.Select(x => x.GuestId));
    var seeRate = V5Desk.CanSeeRate(user);
    return Results.Ok(reservations.Select(x => new { x.Id, x.Code, x.PropertyId, x.RoomId, x.GuestId, x.FullName, x.Phone, x.RoomNumber, x.CheckInDate, x.CheckOutDate,
        nightlyRate = V5Desk.HideRate(user, x.RateConfidential) ? (decimal?)null : x.NightlyRate, rateHidden = x.RateConfidential && !seeRate, x.RateConfidential,
        x.Adults, x.Children, x.Status, x.BookingSource, x.BookingMode, x.GroupCode, x.Note, x.CreatedAtUtc, x.DepositRequiredAmount, x.DepositDueAtUtc, x.DepositReceived, x.StayId,
        outstandingDebt = guestDebt.GetValueOrDefault(x.GuestId) }));
});
api.MapPost("/reservations", async (ReservationInput req, ClaimsPrincipal user, PmsDbContext db, IMediator mediator) => {
    if (!Ops.HasScope(user, req.PropertyId)) return Results.Forbid();
    if (req.CheckInDate >= req.CheckOutDate || req.Adults < 1 || string.IsNullOrWhiteSpace(req.GuestName))
        return Ops.Fail("Họ tên khách và khoảng ngày nhận/trả phải hợp lệ");
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    if (!await Ops.LockRoom(db, req.RoomId)) return Ops.Conflict("Phòng không tồn tại");
    var room = await db.Rooms.FirstOrDefaultAsync(x => x.Id == req.RoomId && x.PropertyId == req.PropertyId);
    if (room == null || room.IsOutOfOrder || (req.CheckInDate <= DateOnly.FromDateTime(DateTime.Today) && room.HousekeepingStatus != "INSPECTED")) return Ops.Conflict("Phòng không sẵn sàng bán");
    if (await Ops.IsBooked(db, room.Id, req.CheckInDate, req.CheckOutDate)) return Ops.Conflict("Phòng đã có đặt phòng trùng khoảng ngày");
    if (await V5Closeout.ActiveHold(db, room.Id, req.CheckInDate, req.CheckOutDate)) return Ops.Conflict("Phòng đang được giữ tạm");
    var mode = req.BookingMode ?? "OVERNIGHT";
    if (mode is not ("OVERNIGHT" or "DAY_USE" or "HOURLY" or "WEEK" or "MONTH")) return Ops.Fail("Loại lưu trú không hợp lệ");
    var packageError = await mediator.Send(new CheckStayQuote(mode, req.CheckInDate, req.CheckOutDate));
    if (packageError != null) return Ops.Fail(packageError);
    var roomType = await db.RoomTypes.FirstAsync(x => x.Id == room.RoomTypeId);
    RatePlan? plan = null;
    if (req.RatePlanId is int planId)
    {
        plan = await db.RatePlans.FirstOrDefaultAsync(x => x.Id == planId && x.PropertyId == req.PropertyId && x.IsActive);
        if (plan == null) return Ops.Fail("Bảng giá không thuộc cơ sở");
        mode = plan.Mode;
    }
    var (_, cardUnit) = RoomCardPricing.Build(roomType, plan, mode, req.CheckInDate, req.CheckOutDate, req.Adults, req.Children, req.PlannedHours, null);
    var tierCode = req.LoyaltyTier ?? req.LoyaltyCard;
    var percent = Loyalty.ClampPercent(req.LoyaltyPercent, tierCode);
    var evidence = await Loyalty.LookUp(db, req.PropertyId, req.Phone, req.GiftNote);
    var weekday = plan?.BasePrice ?? roomType.WeekdayRate ?? roomType.BaseRate;
    var weekend = plan != null ? plan.BasePrice : roomType.WeekendRate ?? roomType.WeekdayRate ?? roomType.BaseRate;
    var nightCards = mode == "OVERNIGHT" ? RoomCardPricing.Cards(req.CheckInDate, req.CheckOutDate, weekday, weekend, roomType, req.NightlyRate is null ? percent : 0) : [];
    decimal? priced = req.NightlyRate;
    var applied = false;
    List<RoomCardPricing.Line> lines;
    decimal roomUnit;
    if (mode == "OVERNIGHT" && req.NightRates is { Length: > 0 })
    {
        var nightReject = RoomCardPricing.RejectNights(nightCards, req.NightRates);
        if (nightReject != null) return Ops.Fail(nightReject);
        var (baseLines, _) = RoomCardPricing.Build(roomType, plan, mode, req.CheckInDate, req.CheckOutDate, req.Adults, req.Children, req.PlannedHours, null);
        lines = RoomCardPricing.WithNights(baseLines, req.NightRates);
        roomUnit = decimal.Round(req.NightRates.Average(x => x.Amount), 0, MidpointRounding.AwayFromZero);
        priced = null;
    }
    else
    {
        if (req.NightlyRate is null && percent > 0)
        {
            priced = Loyalty.Payable(cardUnit, percent, V5Desk.Floor(roomType, mode, cardUnit));
            applied = true;
        }
        (lines, roomUnit) = RoomCardPricing.Build(roomType, plan, mode, req.CheckInDate, req.CheckOutDate, req.Adults, req.Children, req.PlannedHours, priced);
    }
    var belowFloor = mode == "OVERNIGHT" && req.NightRates is { Length: > 0 } ? null : V5Desk.Guard(roomType, mode, cardUnit, req.NightlyRate);
    if (belowFloor != null) return Ops.Fail(belowFloor);
    if (req.RateConfidential && !Ops.CanManage(user)) return Ops.Fail("Chỉ quản lý được gắn bảo mật giá");
    if (lines.Any(x => x.UnitPrice < 0)) return Ops.Fail("Giá phòng không hợp lệ");
    if (req.AgencyId.HasValue && !await db.Agencies.AnyAsync(x => x.Id == req.AgencyId && x.PropertyId == req.PropertyId)) return Ops.Fail("Đại lý không thuộc cơ sở");
    var guest = evidence.GuestId is int existingId ? await db.Guests.FirstAsync(g => g.Id == existingId) : new Guest();
    guest.FullName = req.GuestName.Trim(); guest.Phone = req.Phone?.Trim() ?? ""; guest.Email = req.Email?.Trim(); guest.Nationality = Ops.TrimOrNull(req.Nationality);
    guest.Title = Ops.TrimOrNull(req.Title); guest.Gender = Ops.TrimOrNull(req.Gender); guest.IdDocumentType = Ops.TrimOrNull(req.IdDocumentType);
    guest.Ethnicity = Ops.TrimOrNull(req.Ethnicity); guest.Religion = Ops.TrimOrNull(req.Religion); guest.StayPurpose = Ops.TrimOrNull(req.StayPurpose);
    guest.LoyaltyCard = Loyalty.CardFor(percent, tierCode);
    if (!string.IsNullOrWhiteSpace(req.GiftNote)) guest.GiftNote = req.GiftNote.Trim();
    if (!string.IsNullOrWhiteSpace(req.Preferences)) guest.Preferences = req.Preferences.Trim();
    if (guest.Id == 0) db.Guests.Add(guest);
    await db.SaveChangesAsync();
    var item = new Reservation { Code = "BK-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), PropertyId = req.PropertyId, RoomId = req.RoomId,
        GuestId = guest.Id, CheckInDate = req.CheckInDate, CheckOutDate = req.CheckOutDate, Adults = req.Adults, Children = req.Children,
        NightlyRate = roomUnit, BookingSource = req.BookingSource ?? "DIRECT", BookingMode = mode, AgencyId = req.AgencyId,
        RatePlanId = plan?.Id, RatePlanSnapshot = RoomCardPricing.Serialize(plan == null ? "ROOM_CARD" : "RATE_PLAN", mode, lines, roomType, plan, priced, req.NightRates),
        GroupCode = req.GroupCode, Note = req.Note, Status = "CONFIRMED", RateConfidential = req.RateConfidential,
        LoyaltyTier = applied ? (tierCode is "BAC" or "VANG" && Loyalty.Find(tierCode).Percent == percent ? tierCode : "CUSTOM") : null, LoyaltyPercent = applied ? percent : 0 };
    var requiredNoteTypes = await db.BookingNoteTypes.Where(t => t.IsActive && t.IsRequired && t.PropertyId == req.PropertyId).Select(t => t.Id).ToListAsync();
    var providedNotes = req.Notes ?? [];
    if (requiredNoteTypes.Any(typeId => !providedNotes.Any(n => n.NoteTypeId == typeId && !string.IsNullOrWhiteSpace(n.NoteText))))
        return Ops.Fail("Thiếu ghi chú bắt buộc trước khi xác nhận phiếu");
    db.Reservations.Add(item); await db.SaveChangesAsync();
    foreach (var note in providedNotes.Where(n => !string.IsNullOrWhiteSpace(n.NoteText)))
    {
        if (!await db.BookingNoteTypes.AnyAsync(t => t.Id == note.NoteTypeId && t.IsActive && (t.PropertyId == null || t.PropertyId == req.PropertyId)))
            return Ops.Fail("Loại ghi chú không hợp lệ");
        db.ReservationNotes.Add(new ReservationNote { ReservationId = item.Id, NoteTypeId = note.NoteTypeId, NoteText = note.NoteText!.Trim(), CreatedByUserId = Ops.UserId(user) });
    }
    await db.SaveChangesAsync();
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
    if (await Ops.OtaPendingConfirmation(db, id)) return Ops.Conflict("Booking OTA chưa hoàn tất xác nhận 2 lớp; xác nhận tại màn hình Kết nối OTA trước khi hủy");
    var paid = await db.Payments.Where(x => x.ReservationId == id).SumAsync(x => (decimal?)x.Amount) ?? 0;
    if (paid != 0) return Ops.Conflict("Booking còn tiền cọc, quản lý phải hoàn cọc trước khi hủy");
    r.Status = "CANCELLED"; await db.SaveChangesAsync();
    await Ops.Log(db, user, r.PropertyId, "RESERVATION_CANCEL", "Reservation", r.Id);
    await tx.CommitAsync(); return Results.Ok(new { r.Id, r.Status });
});
// Refund is an explicit negative ledger entry; cancellation is never allowed to lose a deposit.
api.MapPost("/reservations/{id:int}/refund-deposit", async (int id, PaymentInput req, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.CanManage(user)) return Results.Forbid();
        if (req.Method == "CITY_LEDGER") return Ops.Fail("Không hoàn tiền bằng City Ledger");
        if (req.Amount <= 0 || req.Amount > 10000000000m || !Ops.PayMethods.Contains(req.Method) ||
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
    var stayZone = await db.Properties.Where(x => x.Id == r.PropertyId).Select(x => x.TimeZoneId).FirstOrDefaultAsync();
    if (r.CheckInDate > V5Closeout.PropertyToday(stayZone)) return Ops.Conflict("Chưa đến ngày nhận phòng.");
    if (!await Ops.LockRoom(db, r.RoomId)) return Ops.Conflict("Phòng không tồn tại");
    var room = await db.Rooms.FindAsync(r.RoomId);
    if (room == null || room.IsOutOfOrder || room.HousekeepingStatus != "INSPECTED") return Ops.Conflict("Phòng chưa sẵn sàng nhận khách");
    if (await db.Stays.AnyAsync(s => s.RoomId == r.RoomId && s.Status == "IN_HOUSE")) return Ops.Conflict("Phòng đã có khách lưu trú");
    r.Status = "CHECKED_IN";
    var stay = new Stay { ReservationId = r.Id, RoomId = r.RoomId };
    db.Stays.Add(stay);
    var locked = RoomCardPricing.Read(r.RatePlanSnapshot);
    if (locked.Count > 0)
    {
        foreach (var line in locked)
            db.Charges.Add(new FolioCharge { ReservationId = r.Id, Category = line.Category, Description = line.Description, Quantity = line.Quantity, UnitPrice = line.UnitPrice, Amount = line.Amount });
    }
    else
    {
        var nights = r.BookingMode == "OVERNIGHT" ? r.CheckOutDate.DayNumber - r.CheckInDate.DayNumber : 1;
        db.Charges.Add(new FolioCharge { ReservationId = r.Id, Category = "ROOM", Description = r.BookingMode == "OVERNIGHT" ? $"Tiền phòng {nights} đêm" : $"Gói {r.BookingMode}", Quantity = nights,
            UnitPrice = r.NightlyRate, Amount = r.NightlyRate * nights });
    }
    await db.SaveChangesAsync();
    await Ops.Log(db, user, r.PropertyId, "CHECK_IN", "Stay", stay.Id);
    await tx.CommitAsync();
    return Results.Ok(new { stay.Id, r.Code, r.Status });
});
api.MapGet("/stays", async (int propertyId, DateOnly? from, DateOnly? to, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    var rangeError = Ops.DateWindow(from, to, out _, out _);
    if (rangeError != null) return Ops.Fail(rangeError);
    var ranged = from.HasValue;
    var rangeFrom = from;
    var rangeTo = to;
    var stays = await (from stay in db.Stays join r in db.Reservations on stay.ReservationId equals r.Id
        join g in db.Guests on r.GuestId equals g.Id join room in db.Rooms on stay.RoomId equals room.Id
        where r.PropertyId == propertyId && stay.Status == "IN_HOUSE" && (!ranged || (r.CheckInDate <= rangeTo && r.CheckOutDate > rangeFrom))
        orderby stay.CheckedInAtUtc descending
        select new { stay.Id, stay.ReservationId, r.Code, g.FullName, g.Phone, RoomNumber = room.Number, r.CheckInDate, r.CheckOutDate, r.NightlyRate, r.RateConfidential,
            Charges = db.Charges.Where(x => x.ReservationId == r.Id).Sum(x => (decimal?)x.Amount) ?? 0,
            Payments = db.Payments.Where(x => x.ReservationId == r.Id).Sum(x => (decimal?)x.Amount) ?? 0 }).ToListAsync();
    return Results.Ok(stays.Select(s => { var hide = V5Desk.HideRate(user, s.RateConfidential); return new { s.Id, s.ReservationId, s.Code, s.FullName, s.Phone, s.RoomNumber, s.CheckInDate, s.CheckOutDate, nightlyRate = hide ? (decimal?)null : s.NightlyRate, rateHidden = hide, charges = hide ? (decimal?)null : s.Charges, payments = s.Payments, balance = hide ? (decimal?)null : s.Charges - s.Payments }; }));
});
api.MapGet("/stays/{stayId:int}/folio", async (int stayId, ClaimsPrincipal user, PmsDbContext db) => {
    var details = await (from s in db.Stays join r in db.Reservations on s.ReservationId equals r.Id
        join g in db.Guests on r.GuestId equals g.Id join room in db.Rooms on s.RoomId equals room.Id
        where s.Id == stayId select new { s.Id, s.ReservationId, s.Status, r.PropertyId, r.Code, g.FullName, g.Phone, RoomNumber = room.Number, r.CheckInDate, r.CheckOutDate, r.RateConfidential }).FirstOrDefaultAsync();
    if (details == null) return Results.NotFound();
    if (!Ops.HasScope(user, details.PropertyId)) return Results.Forbid();
    var hideRate = V5Desk.HideRate(user, details.RateConfidential);
    var charges = await db.Charges.Where(x => x.ReservationId == details.ReservationId).OrderBy(x => x.PostedAtUtc).ToListAsync();
    var payments = await db.Payments.Where(x => x.ReservationId == details.ReservationId).OrderBy(x => x.PaidAtUtc).ToListAsync();
    var transfers = await Ops.TransferHistory(db, stayId, hideRate);
    var chargeRows = charges.Select(c => hideRate && c.Category == "ROOM"
        ? new { c.Id, c.Category, Description = "Tiền phòng (bảo mật)", c.Quantity, UnitPrice = (decimal?)null, Amount = (decimal?)null, hidden = true }
        : new { c.Id, c.Category, c.Description, c.Quantity, UnitPrice = (decimal?)c.UnitPrice, Amount = (decimal?)c.Amount, hidden = false });
    return Results.Ok(new { details = new { details.Id, details.ReservationId, details.Status, details.PropertyId, details.Code, details.FullName, details.Phone, details.RoomNumber, details.CheckInDate, details.CheckOutDate, rateHidden = hideRate },
        charges = chargeRows, payments, transfers, rateHidden = hideRate,
        total = hideRate ? (decimal?)null : charges.Sum(c => c.Amount), paid = payments.Sum(p => p.Amount),
        balance = hideRate ? (decimal?)null : charges.Sum(c => c.Amount) - payments.Sum(p => p.Amount) });
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
    var access = await StaffAccess.Load(db, user);
    if (StaffAccess.MinibarOnly(access) && !StaffAccess.IsMinibarOrLaundry(svc.Code))
        return Ops.Conflict("Buồng phòng chỉ ghi được minibar và giặt ủi.");
    if (svc.TrackInventory && svc.StockQuantity < req.Quantity) return Ops.Conflict("Không đủ số lượng tồn kho");
    var stayReservation = await db.Reservations.FirstAsync(r => r.Id == data.Id);
    var alreadyCharged = await db.Charges.Where(c => c.ReservationId == stayReservation.Id).SumAsync(c => (decimal?)c.Amount) ?? 0;
    var alreadyPaid = await db.Payments.Where(p => p.ReservationId == stayReservation.Id).SumAsync(p => (decimal?)p.Amount) ?? 0;
    if (!Ops.CanManage(user) && alreadyCharged + svc.Price * req.Quantity > alreadyPaid + stayReservation.ApprovedExposure)
        return Ops.Conflict("Vượt số tiền đã thu. Cần quản lý duyệt hạn mức nợ trước khi ghi thêm dịch vụ.");
    if (svc.TrackInventory) svc.StockQuantity -= req.Quantity;
    var charge = new FolioCharge { ReservationId = data.Id, ServiceItemId = svc.Id, Category = "SERVICE", Description = svc.Name, Quantity = req.Quantity,
        UnitPrice = svc.Price, Amount = svc.Price * req.Quantity };
    db.Charges.Add(charge); await db.SaveChangesAsync();
    if (svc.TrackInventory) db.StockMovements.Add(new StockMovement { PropertyId = data.PropertyId, ServiceItemId = svc.Id, Type = "CONSUMPTION", QuantityChange = -req.Quantity, BalanceAfter = svc.StockQuantity, Reference = $"CHARGE-{charge.Id}", ActorUserId = Ops.UserId(user) });
    await db.SaveChangesAsync();
    await Ops.Log(db, user, data.PropertyId, "SERVICE_POST", "FolioCharge", charge.Id);
    await tx.CommitAsync(); return Results.Ok(charge);
});
api.MapGet("/shifts", async (int propertyId, DateOnly? from, DateOnly? to, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    var rangeError = Ops.DateWindow(from, to, out var start, out var end);
    if (rangeError != null) return Ops.Fail(rangeError);
    var shifts = db.Shifts.Where(s => s.PropertyId == propertyId);
    if (from.HasValue) shifts = shifts.Where(s => s.Status == "OPEN" || (s.OpenedAtUtc >= start && s.OpenedAtUtc < end));
    return Results.Ok(await shifts.OrderByDescending(s => s.OpenedAtUtc).Take(from.HasValue ? 200 : 30).ToListAsync());
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
    if (!await ShiftHandoverEndpoints.IsAcknowledged(db, shift.Id)) return Ops.Conflict("Cần người nhận xác nhận sổ giao ca trước khi đóng ca");
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
    if (V5Desk.HideRate(user, r.RateConfidential)) return Ops.Conflict("Booking bảo mật giá. Chỉ quản lý hoặc kế toán được trả phòng");
    var lockedDay = await Ops.RejectIfLocked(db, r.PropertyId);
    if (lockedDay != null) return lockedDay;
    var total = await db.Charges.Where(x => x.ReservationId == r.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
    var paid = await db.Payments.Where(x => x.ReservationId == r.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
    if (total != paid) return Ops.Conflict($"Chưa cân bằng folio. Còn phải thanh toán: {total - paid:N0} VND");
    stay.Status = "CHECKED_OUT"; stay.CheckedOutAtUtc = DateTime.UtcNow; r.Status = "CHECKED_OUT";
    var room = await db.Rooms.FindAsync(stay.RoomId);
    if (room != null) { room.HousekeepingStatus = "DIRTY"; await V5Closeout.EnsureCleaningTask(db, room, "CHECKOUT"); }
    await db.SaveChangesAsync(); await Ops.Log(db, user, r.PropertyId, "CHECK_OUT", "Stay", stay.Id);
    await tx.CommitAsync(); return Results.Ok(new { stay.Id, r.Code, stay.Status });
});
api.MapGet("/stays/{stayId:int}/transfers", async (int stayId, ClaimsPrincipal user, PmsDbContext db) => {
    var stay = await (from s in db.Stays join r in db.Reservations on s.ReservationId equals r.Id where s.Id == stayId select new { r.PropertyId }).FirstOrDefaultAsync();
    if (stay == null) return Results.NotFound();
    if (!Ops.HasScope(user, stay.PropertyId)) return Results.Forbid();
    return Results.Ok(await Ops.TransferHistory(db, stayId));
});
api.MapPost("/stays/{stayId:int}/transfer", async (int stayId, TransferInput req, ClaimsPrincipal user, PmsDbContext db) => {
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    var stay = await db.Stays.FindAsync(stayId); if (stay == null) return Results.NotFound();
    var r = await db.Reservations.FindAsync(stay.ReservationId); if (r == null) return Results.NotFound();
    if (!Ops.HasScope(user, r.PropertyId)) return Results.Forbid();
    if (stay.Status != "IN_HOUSE") return Ops.Conflict("Chỉ đổi phòng cho khách đang ở");
    if (stay.RoomId == req.NewRoomId) return Ops.Fail("Phòng mới phải khác phòng hiện tại");
    var reason = req.ReasonCode?.Trim().ToUpperInvariant() ?? "";
    if (!Ops.TransferReasons.Contains(reason)) return Ops.Fail("Chọn lý do đổi phòng");
    var note = Ops.TrimOrNull(req.ReasonNote);
    if (note != null && note.Length > 200) return Ops.Fail("Ghi chú tối đa 200 ký tự");
    if (reason == "OTHER" && note == null) return Ops.Fail("Lý do khác cần ghi chú");
    var newRoom = await db.Rooms.FirstOrDefaultAsync(x => x.Id == req.NewRoomId && x.PropertyId == r.PropertyId);
    var oldRoom = await db.Rooms.FirstOrDefaultAsync(x => x.Id == stay.RoomId);
    var newType = newRoom == null ? null : await db.RoomTypes.FirstOrDefaultAsync(x => x.Id == newRoom.RoomTypeId);
    if (newRoom == null || oldRoom == null || newType == null || newRoom.IsOutOfOrder || newRoom.HousekeepingStatus != "INSPECTED") return Ops.Conflict("Phòng đích không sẵn sàng");
    var zoneId = await db.Properties.Where(x => x.Id == r.PropertyId).Select(x => x.TimeZoneId).FirstAsync();
    TimeZoneInfo zone;
    try { zone = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(zoneId) ? "Asia/Ho_Chi_Minh" : zoneId); }
    catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { zone = TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "VN", "VN"); }
    var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
    var windowEnd = r.CheckOutDate > today ? r.CheckOutDate : today.AddDays(1);
    await Ops.LockReservation(db, r.Id);
    foreach (var roomId in new[] { oldRoom.Id, newRoom.Id }.OrderBy(x => x))
        if (!await Ops.LockRoom(db, roomId)) return Ops.Conflict("Phòng không tồn tại");
    await db.Entry(newRoom).ReloadAsync();
    if (newRoom.IsOutOfOrder || newRoom.HousekeepingStatus != "INSPECTED") return Ops.Conflict("Phòng đích không sẵn sàng");
    if (await db.Stays.AnyAsync(x => x.RoomId == newRoom.Id && x.Id != stay.Id && x.Status == "IN_HOUSE")) return Ops.Conflict("Phòng đích đang có khách");
    if (await db.Reservations.AnyAsync(x => x.Id != r.Id && x.RoomId == newRoom.Id && (x.Status == "CONFIRMED" || x.Status == "CHECKED_IN") && x.CheckInDate < windowEnd && x.CheckOutDate > today))
        return Ops.Conflict("Phòng đích bị trùng lịch");
    var oldRate = r.NightlyRate;
    var newRate = oldRate;
    var delta = 0m;
    if (!req.KeepPrice)
    {
        if (!Ops.CanManage(user)) return Ops.Fail("Chỉ quản lý được bỏ giá đã khóa khi đổi phòng");
        var from = today > r.CheckInDate ? today : r.CheckInDate;
        decimal oldRemaining;
        if (r.BookingMode == "OVERNIGHT")
        {
            var stayed = RoomCardPricing.ExtendNights(r.RatePlanSnapshot, from, r.CheckOutDate, r.Adults, r.Children);
            var nights = Math.Max(0, r.CheckOutDate.DayNumber - from.DayNumber);
            oldRemaining = stayed.Count > 0 ? stayed.Sum(x => x.Amount) : oldRate * nights;
        }
        else oldRemaining = RoomCardPricing.LockedTotal(r.RatePlanSnapshot) is var locked && locked > 0 ? locked : oldRate;
        var quoteStart = r.BookingMode == "OVERNIGHT" && from < r.CheckOutDate ? from : r.CheckInDate;
        var quoteEnd = r.CheckOutDate > quoteStart ? r.CheckOutDate : quoteStart.AddDays(1);
        var (lines, unit) = RoomCardPricing.Build(newType, null, r.BookingMode, quoteStart, quoteEnd, r.Adults, r.Children, null, null);
        var quoted = r.BookingMode == "OVERNIGHT" && from >= r.CheckOutDate ? 0 : lines.Sum(x => x.Amount);
        delta = quoted - oldRemaining;
        newRate = unit;
        if (delta != 0)
            db.Charges.Add(new FolioCharge { ReservationId = r.Id, Category = "ROOM", Description = $"Chênh lệch đổi phòng {oldRoom.Number} sang {newRoom.Number}", Quantity = 1, UnitPrice = delta, Amount = delta });
        r.NightlyRate = newRate;
        r.RatePlanSnapshot = RoomCardPricing.RetargetCard(r.RatePlanSnapshot, newType, r.BookingMode, r.CheckInDate, r.CheckOutDate, r.Adults, r.Children);
    }
    var now = DateTime.UtcNow;
    db.RoomTransfers.Add(new RoomTransferHistory {
        PropertyId = r.PropertyId, ReservationId = r.Id, StayId = stay.Id, OldRoomId = oldRoom.Id, NewRoomId = newRoom.Id,
        ReasonCode = reason, ReasonNote = note, OldRate = oldRate, NewRate = newRate, RateDelta = delta, EffectiveAtUtc = now,
        CreatedByUserId = Ops.UserId(user), ApprovedByUserId = req.KeepPrice ? null : Ops.UserId(user), ApprovedAtUtc = req.KeepPrice ? null : now, CreatedAtUtc = now
    });
    oldRoom.HousekeepingStatus = "DIRTY";
    stay.RoomId = newRoom.Id; r.RoomId = newRoom.Id;
    await db.SaveChangesAsync();
    await Ops.Log(db, user, r.PropertyId, "ROOM_TRANSFER", "Stay", stay.Id, $"{oldRoom.Number} -> {newRoom.Number} {reason} {delta}");
    await tx.CommitAsync();
    return Results.Ok(new { stay.Id, newRoomNumber = newRoom.Number, oldRate, newRate, rateDelta = delta, keptPrice = req.KeepPrice });
});
api.MapPost("/stays/{stayId:int}/extend", async (int stayId, ExtendInput req, ClaimsPrincipal user, PmsDbContext db) => {
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    var stay = await db.Stays.FindAsync(stayId); if (stay == null) return Results.NotFound();
    var r = await db.Reservations.FindAsync(stay.ReservationId); if (r == null) return Results.NotFound();
    if (!Ops.HasScope(user, r.PropertyId)) return Results.Forbid();
    if (stay.Status != "IN_HOUSE" || req.NewCheckOutDate <= r.CheckOutDate) return Ops.Fail("Ngày trả mới phải lớn hơn ngày trả hiện tại");
    if (r.BookingMode is "WEEK" or "MONTH") return Ops.Fail("Gói tuần và tháng không gia hạn lẻ. Tạo phiếu mới cho kỳ tiếp.");
    if (await db.Reservations.AnyAsync(x => x.Id != r.Id && x.RoomId == stay.RoomId && x.Status == "CONFIRMED" && x.CheckInDate < req.NewCheckOutDate && x.CheckOutDate > r.CheckOutDate))
        return Ops.Conflict("Có đặt phòng khác trong thời gian muốn gia hạn");
    var nights = req.NewCheckOutDate.DayNumber - r.CheckOutDate.DayNumber;
    var extension = r.BookingMode == "OVERNIGHT"
        ? RoomCardPricing.ExtendNights(r.RatePlanSnapshot, r.CheckOutDate, req.NewCheckOutDate, r.Adults, r.Children)
        : [];
    if (extension.Count == 0)
        extension.Add(new RoomCardPricing.Line("ROOM", $"Gia hạn {nights} đêm", nights, r.NightlyRate, nights * r.NightlyRate));
    foreach (var line in extension)
        db.Charges.Add(new FolioCharge { ReservationId = r.Id, Category = line.Category, Description = line.Description, Quantity = line.Quantity, UnitPrice = line.UnitPrice, Amount = line.Amount });
    r.CheckOutDate = req.NewCheckOutDate;
    await db.SaveChangesAsync(); await Ops.Log(db, user, r.PropertyId, "EXTEND_STAY", "Stay", stay.Id);
    await tx.CommitAsync(); return Results.Ok(new { stay.Id, r.CheckOutDate, amount = extension.Sum(x => x.Amount) });
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
    var charges = await db.Charges.Where(x => resIds.Contains(x.ReservationId) && x.PostedAtUtc >= start && x.PostedAtUtc < end)
        .GroupBy(x => x.Category).Select(g => new { Category = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync();
    var payments = await db.Payments.Where(x => resIds.Contains(x.ReservationId) && x.PaidAtUtc >= start && x.PaidAtUtc < end)
        .GroupBy(x => x.Method).Select(g => new { Method = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync();
    decimal Cat(string name) => charges.Where(x => x.Category == name).Sum(x => x.Amount);
    decimal Pay(string name) => payments.Where(x => x.Method == name).Sum(x => x.Amount);
    var roomRevenue = Cat("ROOM");
    var serviceRevenue = Cat("SERVICE");
    var noShowRevenue = Cat("NO_SHOW");
    var lateRevenue = Cat("LATE_CHECKOUT");
    var retailRevenue = await db.RetailSales.Where(x => x.PropertyId == propertyId && x.SoldAtUtc >= start && x.SoldAtUtc < end).SumAsync(x => (decimal?)x.TotalAmount) ?? 0;
    var outsideRevenue = await (from line in db.VirtualFolioLines join folio in db.VirtualFolios on line.FolioId equals folio.Id
        where folio.PropertyId == propertyId && line.PostedAtUtc >= start && line.PostedAtUtc < end select (decimal?)line.Amount).SumAsync() ?? 0;
    var expenses = await db.Vouchers.Where(x => x.PropertyId == propertyId && x.Type == "EXPENSE" && x.CreatedAtUtc >= start && x.CreatedAtUtc < end).SumAsync(x => (decimal?)x.Amount) ?? 0;
    var profit = roomRevenue + serviceRevenue + noShowRevenue + lateRevenue + retailRevenue + outsideRevenue - expenses;
    var lines = new[] { new { name = "Tiền phòng", amount = roomRevenue }, new { name = "Dịch vụ", amount = serviceRevenue }, new { name = "No-show", amount = noShowRevenue }, new { name = "Trả muộn", amount = lateRevenue }, new { name = "Bán lẻ", amount = retailRevenue }, new { name = "Ngoài phòng", amount = outsideRevenue } };
    return Results.Ok(new { from, to, roomRevenue, serviceRevenue, noShowRevenue, lateRevenue, retailRevenue, outsideRevenue, expenses, profit, lines,
        totalCharges = charges.Sum(x => x.Amount), totalCashIn = payments.Sum(x => x.Amount),
        cash = Pay("CASH"),
        transfer = Pay("TRANSFER"),
        reservationCount = await db.Reservations.CountAsync(x => x.PropertyId == propertyId && x.CreatedAtUtc >= start && x.CreatedAtUtc < end),
        note = "Tiền phòng chỉ gồm hạng ROOM. No-show và phụ thu trả muộn ghi riêng. Lãi lỗ là doanh thu vận hành trừ phiếu chi, không phải báo cáo thuế." });
});
api.MapGet("/reports/summary/export", async (int propertyId, DateOnly from, DateOnly to, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
    if (from > to) return Ops.Fail("Khoảng ngày không hợp lệ");
    var resIds = db.Reservations.Where(r => r.PropertyId == propertyId).Select(r => r.Id);
    var start = from.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
    var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
    var chargeSums = await db.Charges.Where(x => resIds.Contains(x.ReservationId) && x.PostedAtUtc >= start && x.PostedAtUtc < end)
        .GroupBy(x => x.Category).Select(g => new { Category = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync();
    decimal Sum(string category) => chargeSums.Where(x => x.Category == category).Sum(x => x.Amount);
    var csv = new System.Text.StringBuilder();
    csv.AppendLine("Khoan,So tien");
    csv.AppendLine($"Tien phong,{Sum("ROOM")}");
    csv.AppendLine($"Dich vu,{Sum("SERVICE")}");
    csv.AppendLine($"No-show,{Sum("NO_SHOW")}");
    csv.AppendLine($"Tra muon,{Sum("LATE_CHECKOUT")}");
    return Results.Text("\uFEFF" + csv, "text/csv; charset=utf-8");
});
api.MapGet("/audit", async (int propertyId, DateOnly? from, DateOnly? to, int? page, int? pageSize, ClaimsPrincipal user, PmsDbContext db) => {
    if (!Ops.CanManage(user) || !Ops.HasScope(user, propertyId)) return Results.Forbid();
    var rangeError = Ops.DateWindow(from, to, out var start, out var end);
    if (rangeError != null) return Ops.Fail(rangeError);
    var events = db.AuditEvents.Where(x => x.PropertyId == propertyId);
    if (from.HasValue) events = events.Where(x => x.OccurredAtUtc >= start && x.OccurredAtUtc < end);
    var ordered = events.OrderByDescending(x => x.OccurredAtUtc);
    if (page is > 0)
    {
        var size = pageSize is > 0 and <= 100 ? pageSize.Value : 25;
        var total = await ordered.CountAsync();
        var items = await ordered.Skip((page.Value - 1) * size).Take(size).ToListAsync();
        return Results.Ok(new { items, total, page, pageSize = size });
    }
    return Results.Ok(await ordered.Take(from.HasValue ? 500 : 100).ToListAsync());
});
app.MapPmsV2();
app.MapPricingV4();
app.MapNightAuditV4();
app.MapV5Foundation();
app.MapV5Closeout();
app.MapShiftHandover();
app.MapV5Desk();
app.MapDeskPhase();
app.MapDeskNext();
DeskScale.Map(app);
app.MapOtaIntegration();
app.Run();

public record LoginRequest(string Email, string Password);
public record PropertyInput(string Code, string Name, string? City, string? Address, string? Phone, string? Fax = null, string? Email = null, string? Website = null);
public record PropertyContactInput(string? Fax, string? Email, string? Website);
public record UserInput(int? PropertyId, string FullName, string Email, string Password, string Role);
public record UserUpdateInput(int? PropertyId, string FullName, string Email, string? Password, string Role, bool IsActive);
public record PermissionInput(string[]? Keys, bool Reset);
public record RoomTypeInput(int PropertyId, string Code, string Name, int Capacity, decimal BaseRate,
    decimal? WeekdayRate = null, decimal? WeekendRate = null, decimal? HourlyRate = null, decimal? ExtraHourRate = null, decimal? ExtraPersonRate = null);
public record RoomInput(int PropertyId, int RoomTypeId, string Number, int Floor);
public record HousekeepingInput(string Status);
public record HousekeepingTaskInput(string? Cleaner1, string? Cleaner2, string? Cleaner3, string? Note, string? Priority = null, string? ShiftCode = null);
public record DeskNoteInput(int PropertyId, string Text);
public record MaintenanceInput(bool IsOutOfOrder, string? Note);
public record RoomCardQuoteRequest(int PropertyId, int RoomId, DateOnly CheckInDate, DateOnly CheckOutDate, string? BookingMode, int Adults, int Children, int? PlannedHours, decimal? RoomRateOverride, int? RatePlanId, string? LoyaltyTier = null, string? Phone = null, string? GiftNote = null, decimal? LoyaltyPercent = null, RoomCardPricing.NightAmount[]? NightRates = null);
public record ReservationInput(int PropertyId, int RoomId, string GuestName, string Phone, string? Email,
    DateOnly CheckInDate, DateOnly CheckOutDate, int Adults, int Children, decimal? NightlyRate,
    string? BookingSource, string? GroupCode, string? Note, string? BookingMode = null, int? AgencyId = null, int? RatePlanId = null, int? PlannedHours = null,
    string? Title = null, string? Gender = null, string? IdDocumentType = null, string? Ethnicity = null, string? Religion = null, string? StayPurpose = null, string? LoyaltyCard = null,
    string? Nationality = null, bool RateConfidential = false, ReservationNoteInput[]? Notes = null, string? LoyaltyTier = null, string? GiftNote = null, decimal? LoyaltyPercent = null, RoomCardPricing.NightAmount[]? NightRates = null, string? Preferences = null);
public record LoyaltyUpdate(string? Tier, string? GiftNote, decimal? Percent = null);
public record ServiceInput(int PropertyId, string Code, string Name, string Unit, decimal Price, bool TrackInventory, int StockQuantity);
public record RestockInput(int Quantity);
public record PostServiceInput(int ServiceId, int Quantity);
public record PaymentInput(decimal Amount, string Method, string? Reference, string? IdempotencyKey = null);
public record ShiftOpenInput(int PropertyId, decimal OpeningAmount);
public record ShiftCloseInput(decimal ClosingAmount, string? Note);
public record TransferInput(int NewRoomId, string ReasonCode, string? ReasonNote, bool KeepPrice = true);
public record ExtendInput(DateOnly NewCheckOutDate);
public record AgencyInput(int PropertyId, string Code, string Name, decimal CommissionPercent, string? ContactPhone);

public static class Ops
{
    private static readonly string[] DepartmentRoles = ["FRONT_DESK_AGENT", "SALES_AGENT", "HOUSEKEEPING_AGENT", "MAINTENANCE_AGENT", "ACCOUNTING_AGENT"];
    public static readonly string[] PayMethods = ["CASH", "TRANSFER", "CARD", "MOMO", "VNPAY", "CITY_LEDGER"];
    public static readonly string[] TransferReasons = ["GUEST_REQUEST", "UPGRADE", "DOWNGRADE", "MAINTENANCE", "OPERATIONS", "OTHER"];
    public static string TransferReasonLabel(string code) => code switch {
        "GUEST_REQUEST" => "Khách yêu cầu", "UPGRADE" => "Nâng hạng", "DOWNGRADE" => "Hạ hạng",
        "MAINTENANCE" => "Phòng cũ phải sửa", "OPERATIONS" => "Điều phối", "OTHER" => "Lý do khác", _ => code
    };
    public static async Task<List<object>> TransferHistory(PmsDbContext db, int stayId, bool hideRates = false) {
        var rows = await (from h in db.RoomTransfers
            join oldRoom in db.Rooms on h.OldRoomId equals oldRoom.Id
            join newRoom in db.Rooms on h.NewRoomId equals newRoom.Id
            join actor in db.Users on h.CreatedByUserId equals actor.Id
            where h.StayId == stayId
            orderby h.EffectiveAtUtc
            select new { h.EffectiveAtUtc, FromRoom = oldRoom.Number, ToRoom = newRoom.Number, h.ReasonCode, h.ReasonNote, h.OldRate, h.NewRate, h.RateDelta, Actor = actor.FullName }).ToListAsync();
        return rows.Select(x => (object)new { at = x.EffectiveAtUtc, fromRoom = x.FromRoom, toRoom = x.ToRoom, reason = TransferReasonLabel(x.ReasonCode), note = x.ReasonNote ?? "", oldRate = hideRates ? (decimal?)null : x.OldRate, newRate = hideRates ? (decimal?)null : x.NewRate, rateDelta = hideRates ? (decimal?)null : x.RateDelta, actor = x.Actor }).ToList();
    }
    public static int UserId(ClaimsPrincipal user) => int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
    public static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public static int? UserProperty(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue("propertyId"), out var id) ? id : null;
    public static bool HasScope(ClaimsPrincipal user, int propertyId) => propertyId > 0 && (UserProperty(user) == null || UserProperty(user) == propertyId);
    public static string? DateWindow(DateOnly? from, DateOnly? to, out DateTime startUtc, out DateTime endUtc)
    {
        startUtc = default; endUtc = default;
        if (!from.HasValue && !to.HasValue) return null;
        if (!from.HasValue || !to.HasValue) return "Chọn đủ từ ngày và đến ngày";
        if (from > to) return "Từ ngày không được sau đến ngày";
        startUtc = from.Value.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        endUtc = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        return null;
    }
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
        var db = context.HttpContext.RequestServices.GetRequiredService<PmsDbContext>();
        var path = context.HttpContext.Request.Path.Value?.TrimEnd('/').ToLowerInvariant() ?? "";
        var method = context.HttpContext.Request.Method;
        var access = await StaffAccess.Load(db, user);
        return StaffAccess.Allows(method, path, user, access) ? await next(context) : Results.Forbid();
    }
    public static Task<bool> OtaPendingConfirmation(PmsDbContext db, int reservationId) =>
        db.ChannelBookings.AnyAsync(x => x.ReservationId == reservationId && x.Status == "PENDING_CONFIRMATION");
    public static IResult Fail(string msg) => Results.BadRequest(new { message = msg });
    public static (string? Fax, string? Email, string? Website, string? Error) ReadContact(string? fax, string? email, string? website)
    {
        fax = TrimOrNull(fax); email = TrimOrNull(email); website = TrimOrNull(website);
        if (fax?.Length > 30) return (null, null, null, "Số fax tối đa 30 ký tự");
        if (email != null && (email.Length > 120 || !email.Contains('@'))) return (null, null, null, "Email cơ sở không hợp lệ");
        if (website?.Length > 200) return (null, null, null, "Website tối đa 200 ký tự");
        return (fax, email, website, null);
    }
    public static IResult Conflict(string msg) => Results.Conflict(new { message = msg });
    public static string? PackageLength(string mode, DateOnly checkIn, DateOnly checkOut) =>
        StayRules.PackageLength(mode, checkIn, checkOut);
    public static Task<bool> IsBooked(PmsDbContext db, int roomId, DateOnly start, DateOnly end, int exceptReservationId = 0) =>
        db.Reservations.AnyAsync(r => r.Id != exceptReservationId && r.RoomId == roomId && (r.Status == "CONFIRMED" || r.Status == "CHECKED_IN") && r.CheckInDate < end && r.CheckOutDate > start);
    public static async Task<IResult?> RejectIfLocked(PmsDbContext db, int propertyId) {
        var zone = await db.Properties.Where(x => x.Id == propertyId).Select(x => x.TimeZoneId).FirstOrDefaultAsync();
        var today = V5Closeout.PropertyToday(zone);
        if (await db.NightAuditLogs.AnyAsync(x => x.PropertyId == propertyId && x.BusinessDate == today && x.Status == "LOCKED"))
            return Conflict($"Ngày {today:dd/MM/yyyy} đã khóa sổ, không ghi thêm giao dịch");
        return null;
    }
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
        var lockedDay = await RejectIfLocked(db, propertyId);
        if (lockedDay != null) return lockedDay;
        if (!Ops.PayMethods.Contains(req.Method)) return Fail("Phương thức thanh toán không hợp lệ");
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
            var locked = RoomCardPricing.LockedTotal(reservation.RatePlanSnapshot);
            var expected = locked > 0 ? locked : reservation.NightlyRate * (reservation.BookingMode == "OVERNIGHT" ?
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
        if (req.Method == "CITY_LEDGER")
        {
            var debt = await db.Debts.FirstOrDefaultAsync(x => x.ReservationId == reservationId);
            if (debt != null && debt.Status == "OPEN" && debt.Origin == "CHECKOUT") return Conflict("Phiếu đang nợ trả phòng. Thu tại màn công nợ.");
            if (debt == null) { debt = new DebtReceivable { PropertyId = propertyId, ReservationId = reservationId, Origin = "CITY_LEDGER", Note = "City Ledger" }; db.Debts.Add(debt); }
            debt.Status = "OPEN"; debt.SettledAtUtc = null; debt.Origin = "CITY_LEDGER";
            debt.OriginalAmount += req.Amount; debt.OutstandingAmount += req.Amount;
            await db.SaveChangesAsync();
        }
        await Log(db, user, propertyId, "PAYMENT_" + kind, "Payment", payment.Id, req.Method);
        await tx.CommitAsync(); return Results.Ok(payment);
    }
}
