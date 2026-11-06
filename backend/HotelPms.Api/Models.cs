using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace HotelPms.Api;

public sealed class Property
{
    public int Id { get; set; }
    [MaxLength(16)] public string Code { get; set; } = "";
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(150)] public string City { get; set; } = "";
    [MaxLength(300)] public string Address { get; set; } = "";
    [MaxLength(15)] public string Phone { get; set; } = "";
    public TimeOnly CheckInTime { get; set; } = new(14, 0);
    public TimeOnly CheckOutTime { get; set; } = new(12, 0);
    public bool IsActive { get; set; } = true;
}
public sealed class RoomType
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [MaxLength(20)] public string Code { get; set; } = "";
    [MaxLength(100)] public string Name { get; set; } = "";
    public int Capacity { get; set; } = 2;
    [Precision(18,2)] public decimal BaseRate { get; set; }
}
public sealed class Room
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int RoomTypeId { get; set; }
    public int? BuildingId { get; set; }
    [MaxLength(12)] public string Number { get; set; } = "";
    public int Floor { get; set; }
    [MaxLength(20)] public string HousekeepingStatus { get; set; } = "INSPECTED";
    public bool IsOutOfOrder { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
}
public sealed class Guest
{
    public int Id { get; set; }
    [MaxLength(150)] public string FullName { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    [MaxLength(200)] public string? Email { get; set; }
    [MaxLength(100)] public string? Nationality { get; set; }
    [MaxLength(32)] public string? IdentityLast4 { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class Reservation
{
    public int Id { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    public int PropertyId { get; set; }
    public int RoomId { get; set; }
    public int GuestId { get; set; }
    public int? AgencyId { get; set; }
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int Adults { get; set; } = 1;
    public int Children { get; set; }
    [Precision(18,2)] public decimal NightlyRate { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "CONFIRMED";
    [MaxLength(20)] public string BookingSource { get; set; } = "DIRECT";
    [MaxLength(30)] public string BookingMode { get; set; } = "OVERNIGHT";
    [MaxLength(30)] public string? GroupCode { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class Stay
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public int RoomId { get; set; }
    public DateTime CheckedInAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CheckedOutAtUtc { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "IN_HOUSE";
}
// One booking/group room can have multiple registered occupants; Guest is the group contact.
public sealed class ReservationOccupant
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    [MaxLength(150)] public string FullName { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    [MaxLength(80)] public string Nationality { get; set; } = "";
    [MaxLength(4)] public string IdentityLast4 { get; set; } = "";
    public bool IsPrimary { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class ServiceItem
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(150)] public string Name { get; set; } = "";
    [MaxLength(20)] public string Unit { get; set; } = "Lần";
    [Precision(18,2)] public decimal Price { get; set; }
    public bool TrackInventory { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class FolioCharge
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public int? ServiceItemId { get; set; }
    [MaxLength(24)] public string Category { get; set; } = "SERVICE";
    [MaxLength(160)] public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
    [Precision(18,2)] public decimal UnitPrice { get; set; }
    [Precision(18,2)] public decimal Amount { get; set; }
    public DateTime PostedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class Payment
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public int? ShiftId { get; set; }
    [Precision(18,2)] public decimal Amount { get; set; }
    [MaxLength(20)] public string Method { get; set; } = "CASH";
    [MaxLength(20)] public string Kind { get; set; } = "PAYMENT";
    [MaxLength(80)] public string? Reference { get; set; }
    [MaxLength(80)] public string? IdempotencyKey { get; set; }
    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class CashShift
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int UserId { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "OPEN";
    [Precision(18,2)] public decimal OpeningAmount { get; set; }
    [Precision(18,2)] public decimal? ClosingAmount { get; set; }
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
}
public sealed class Agency
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [MaxLength(40)] public string Code { get; set; } = "";
    [MaxLength(180)] public string Name { get; set; } = "";
    [Precision(5,2)] public decimal CommissionPercent { get; set; }
    [MaxLength(60)] public string? ContactPhone { get; set; }
}
public sealed class AuditEvent
{
    public long Id { get; set; }
    public int PropertyId { get; set; }
    public int ActorUserId { get; set; }
    [MaxLength(40)] public string Action { get; set; } = "";
    [MaxLength(60)] public string Entity { get; set; } = "";
    public int EntityId { get; set; }
    [MaxLength(500)] public string Details { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class AppUser
{
    public int Id { get; set; }
    [MaxLength(160)] public string FullName { get; set; } = "";
    [MaxLength(150)] public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    [MaxLength(30)] public string Role { get; set; } = "FRONT_DESK_AGENT";
    public int? PropertyId { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class PmsDbContext(DbContextOptions<PmsDbContext> options) : DbContext(options)
{
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<HotelBuilding> Buildings => Set<HotelBuilding>();
    public DbSet<RatePlan> RatePlans => Set<RatePlan>();
    public DbSet<WaitlistEntry> Waitlist => Set<WaitlistEntry>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<RetailSale> RetailSales => Set<RetailSale>();
    public DbSet<CashVoucher> Vouchers => Set<CashVoucher>();
    public DbSet<DebtReceivable> Debts => Set<DebtReceivable>();
    public DbSet<GuestAsset> GuestAssets => Set<GuestAsset>();
    public DbSet<DailyClose> DailyCloses => Set<DailyClose>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Stay> Stays => Set<Stay>();
    public DbSet<ReservationOccupant> Occupants => Set<ReservationOccupant>();
    public DbSet<ServiceItem> Services => Set<ServiceItem>();
    public DbSet<FolioCharge> Charges => Set<FolioCharge>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CashShift> Shifts => Set<CashShift>();
    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Property>().ToTable("properties").HasIndex(x => x.Code).IsUnique();
        b.Entity<RoomType>().ToTable("room_types").HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
        b.Entity<Room>().ToTable("rooms").HasIndex(x => new { x.PropertyId, x.Number }).IsUnique();
        b.Entity<Guest>().ToTable("guests");
        b.Entity<Reservation>().ToTable("reservations").HasIndex(x => x.Code).IsUnique();
        b.Entity<Reservation>().HasIndex(x => new { x.PropertyId, x.RoomId, x.CheckInDate, x.CheckOutDate });
        b.Entity<Stay>().ToTable("stays").HasIndex(x => x.ReservationId).IsUnique();
        b.Entity<ReservationOccupant>().ToTable("reservation_occupants").HasIndex(x => x.ReservationId);
        b.Entity<ServiceItem>().ToTable("services").HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
        b.Entity<FolioCharge>().ToTable("folio_charges");
        b.Entity<Payment>().ToTable("payments").HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[idempotency_key] IS NOT NULL");
        b.Entity<CashShift>().ToTable("cash_shifts");
        b.Entity<Agency>().ToTable("agencies").HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
        b.Entity<AuditEvent>().ToTable("audit_events");
        b.Entity<AppUser>().ToTable("app_users").HasIndex(x => x.Email).IsUnique();
        b.Entity<HotelBuilding>().ToTable("hotel_buildings").HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
        b.Entity<RatePlan>().ToTable("rate_plans").HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
        b.Entity<WaitlistEntry>().ToTable("waitlist_entries");
        b.Entity<Supplier>().ToTable("suppliers").HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
        b.Entity<StockMovement>().ToTable("stock_movements");
        b.Entity<RetailSale>().ToTable("retail_sales");
        b.Entity<CashVoucher>().ToTable("cash_vouchers").HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
        b.Entity<DebtReceivable>().ToTable("debt_receivables").HasIndex(x => x.ReservationId).IsUnique();
        b.Entity<GuestAsset>().ToTable("guest_assets");
        b.Entity<DailyClose>().ToTable("daily_closes").HasIndex(x => new { x.PropertyId, x.BusinessDate }).IsUnique();
        // Foreign-key constraints: prevent dangling bookings, stays and payments.
        b.Entity<RoomType>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Room>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Room>().HasOne<RoomType>().WithMany().HasForeignKey(x => x.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Reservation>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Reservation>().HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Reservation>().HasOne<Guest>().WithMany().HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Stay>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReservationOccupant>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Stay>().HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ServiceItem>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FolioCharge>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Payment>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Payment>().HasOne<CashShift>().WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CashShift>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CashShift>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Agency>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AuditEvent>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AuditEvent>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AppUser>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Room>().HasOne<HotelBuilding>().WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Reservation>().HasOne<Agency>().WithMany().HasForeignKey(x => x.AgencyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FolioCharge>().HasOne<ServiceItem>().WithMany().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<HotelBuilding>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RatePlan>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WaitlistEntry>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WaitlistEntry>().HasOne<RoomType>().WithMany().HasForeignKey(x => x.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WaitlistEntry>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Supplier>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockMovement>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockMovement>().HasOne<ServiceItem>().WithMany().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockMovement>().HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RetailSale>().HasOne<ServiceItem>().WithMany().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CashVoucher>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<DebtReceivable>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<GuestAsset>().HasOne<Stay>().WithMany().HasForeignKey(x => x.StayId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<DailyClose>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        // Word V1.0 naming convention: Database = snake_case, C# = PascalCase, JSON = camelCase.
        foreach (var type in b.Model.GetEntityTypes())
            foreach (var column in type.GetProperties())
                column.SetColumnName(Regex.Replace(column.Name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant());
    }
}
