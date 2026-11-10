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
    [MaxLength(30)] public string? Fax { get; set; }
    [MaxLength(120)] public string? Email { get; set; }
    [MaxLength(200)] public string? Website { get; set; }
    public TimeOnly CheckInTime { get; set; } = new(14, 0);
    public TimeOnly CheckOutTime { get; set; } = new(12, 0);
    [MaxLength(64)] public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh"; // IANA id
    [Precision(5, 2)] public decimal LowOccupancyPercent { get; set; }
    [Precision(18, 2)] public decimal LowRevenueAmount { get; set; }
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
    [Precision(18,2)] public decimal? WeekdayRate { get; set; }
    [Precision(18,2)] public decimal? WeekendRate { get; set; }
    [Precision(18,2)] public decimal? HourlyRate { get; set; }
    [Precision(18,2)] public decimal? ExtraHourRate { get; set; }
    [Precision(18,2)] public decimal? ExtraPersonRate { get; set; }
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
    [MaxLength(10)] public string? Title { get; set; }
    [MaxLength(10)] public string? Gender { get; set; }
    [MaxLength(20)] public string? IdDocumentType { get; set; }
    [MaxLength(40)] public string? Ethnicity { get; set; }
    [MaxLength(40)] public string? Religion { get; set; }
    [MaxLength(30)] public string? StayPurpose { get; set; }
    [MaxLength(30)] public string? LoyaltyCard { get; set; }
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
    public int? RatePlanId { get; set; }
    public string? RatePlanSnapshot { get; set; } // jsonb on PostgreSQL
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
    [Precision(18, 2)] public decimal DepositRequiredAmount { get; set; }
    public DateTime? DepositDueAtUtc { get; set; }
    [MaxLength(40)] public string? DepositPolicyCode { get; set; }
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
public sealed class RoomTransferHistory
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int ReservationId { get; set; }
    public int StayId { get; set; }
    public int OldRoomId { get; set; }
    public int NewRoomId { get; set; }
    [MaxLength(30)] public string ReasonCode { get; set; } = "";
    [MaxLength(200)] public string? ReasonNote { get; set; }
    [Precision(18,2)] public decimal OldRate { get; set; }
    [Precision(18,2)] public decimal NewRate { get; set; }
    [Precision(18,2)] public decimal RateDelta { get; set; }
    public DateTime EffectiveAtUtc { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class GroupMembershipEvent
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int ReservationId { get; set; }
    [MaxLength(20)] public string Action { get; set; } = "";
    [MaxLength(30)] public string? OldGroupCode { get; set; }
    [MaxLength(30)] public string? NewGroupCode { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
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
    public int MinStock { get; set; }
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
    public DateOnly? BusinessDate { get; set; }
    public int? NightAuditLogId { get; set; }
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
public sealed class HousekeepingTask
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int RoomId { get; set; }
    [MaxLength(20)] public string Kind { get; set; } = "CHECKOUT";
    [MaxLength(20)] public string Status { get; set; } = "NEW";
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAtUtc { get; set; }
    [MaxLength(80)] public string? Cleaner1 { get; set; }
    [MaxLength(80)] public string? Cleaner2 { get; set; }
    [MaxLength(80)] public string? Cleaner3 { get; set; }
    [MaxLength(80)] public string? InspectorName { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
}
public sealed class DeskNote
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [MaxLength(240)] public string Text { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "OPEN";
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class BookingNoteType
{
    public int Id { get; set; }
    public int? PropertyId { get; set; }
    [MaxLength(40)] public string Code { get; set; } = "";
    [MaxLength(80)] public string Name { get; set; } = "";
    public bool IsRequired { get; set; }
    public bool AllowFreeText { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
public sealed class ReservationNote
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public int NoteTypeId { get; set; }
    [MaxLength(500)] public string NoteText { get; set; } = "";
    public int Version { get; set; } = 1;
    public bool IsCurrent { get; set; } = true;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class ReservationHold
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int RoomId { get; set; }
    [MaxLength(150)] public string GuestName { get; set; } = "";
    [MaxLength(30)] public string? GuestPhone { get; set; }
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "ACTIVE";
    public DateTime HeldAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    [MaxLength(200)] public string? ReleaseReason { get; set; }
    public int? ConvertedReservationId { get; set; }
    public int CreatedByUserId { get; set; }
    [MaxLength(80)] public string? IdempotencyKey { get; set; }
}
public sealed class HousekeepingTaskItem
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    [MaxLength(40)] public string ChecklistCode { get; set; } = "";
    [MaxLength(120)] public string Label { get; set; } = "";
    public bool IsRequired { get; set; } = true;
    public bool IsDone { get; set; }
    [MaxLength(200)] public string? Note { get; set; }
}
public sealed class HousekeepingTaskMedia
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    [MaxLength(240)] public string ObjectKey { get; set; } = "";
    [MaxLength(80)] public string ContentType { get; set; } = "";
    public long FileSize { get; set; }
    [MaxLength(20)] public string MediaType { get; set; } = "PHOTO";
    public int UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class NightAuditLine
{
    public int Id { get; set; }
    public int NightAuditLogId { get; set; }
    public int ReservationId { get; set; }
    public DateOnly BusinessDate { get; set; }
    [MaxLength(20)] public string Action { get; set; } = "";
    [Precision(18, 2)] public decimal Amount { get; set; }
    public int? FolioChargeId { get; set; }
}
public sealed class NotificationDispatch
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int ReservationId { get; set; }
    [MaxLength(40)] public string NotificationType { get; set; } = "";
    [MaxLength(40)] public string Milestone { get; set; } = "";
    [MaxLength(20)] public string Channel { get; set; } = "IN_APP";
    [MaxLength(20)] public string Status { get; set; } = "LOGGED";
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public int AttemptCount { get; set; } = 1;
    [MaxLength(200)] public string? LastError { get; set; }
    [MaxLength(120)] public string IdempotencyKey { get; set; } = "";
}
public sealed class PaymentRefund
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int ReservationId { get; set; }
    public int OriginalPaymentId { get; set; }
    [Precision(18, 2)] public decimal RequestedAmount { get; set; }
    [Precision(18, 2)] public decimal? ApprovedAmount { get; set; }
    [MaxLength(40)] public string ReasonCode { get; set; } = "";
    [MaxLength(200)] public string? ReasonText { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "PENDING";
    public int RequestedByUserId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; set; }
    public int? PostedPaymentId { get; set; }
    [MaxLength(80)] public string IdempotencyKey { get; set; } = "";
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
    public DbSet<RoomTransferHistory> RoomTransfers => Set<RoomTransferHistory>();
    public DbSet<GroupMembershipEvent> GroupMemberships => Set<GroupMembershipEvent>();
    public DbSet<ReservationOccupant> Occupants => Set<ReservationOccupant>();
    public DbSet<ServiceItem> Services => Set<ServiceItem>();
    public DbSet<FolioCharge> Charges => Set<FolioCharge>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CashShift> Shifts => Set<CashShift>();
    public DbSet<ShiftHandover> ShiftHandovers => Set<ShiftHandover>();
    public DbSet<ShiftHandoverItem> ShiftHandoverItems => Set<ShiftHandoverItem>();
    public DbSet<ShiftHandoverNote> ShiftHandoverNotes => Set<ShiftHandoverNote>();
    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AdjustmentVoucher> AdjustmentVouchers => Set<AdjustmentVoucher>();
    public DbSet<TaxRule> TaxRules => Set<TaxRule>();
    public DbSet<HolidayRate> HolidayRates => Set<HolidayRate>();
    public DbSet<NightAuditLog> NightAuditLogs => Set<NightAuditLog>();
    public DbSet<EInvoice> EInvoices => Set<EInvoice>();
    public DbSet<ActionPermission> ActionPermissions => Set<ActionPermission>();
    public DbSet<ChannelConnection> ChannelConnections => Set<ChannelConnection>();
    public DbSet<ChannelRoomMapping> ChannelRoomMappings => Set<ChannelRoomMapping>();
    public DbSet<ChannelBooking> ChannelBookings => Set<ChannelBooking>();
    public DbSet<ChannelSyncLog> ChannelSyncLogs => Set<ChannelSyncLog>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<HousekeepingTask> HousekeepingTasks => Set<HousekeepingTask>();
    public DbSet<HousekeepingTaskItem> HousekeepingTaskItems => Set<HousekeepingTaskItem>();
    public DbSet<HousekeepingTaskMedia> HousekeepingTaskMedia => Set<HousekeepingTaskMedia>();
    public DbSet<DeskNote> DeskNotes => Set<DeskNote>();
    public DbSet<BookingNoteType> BookingNoteTypes => Set<BookingNoteType>();
    public DbSet<ReservationNote> ReservationNotes => Set<ReservationNote>();
    public DbSet<ReservationHold> ReservationHolds => Set<ReservationHold>();
    public DbSet<NightAuditLine> NightAuditLines => Set<NightAuditLine>();
    public DbSet<NotificationDispatch> NotificationDispatches => Set<NotificationDispatch>();
    public DbSet<PaymentRefund> PaymentRefunds => Set<PaymentRefund>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        var isNpgsql = Database.IsNpgsql();
        string NotNullFilter(string column) => isNpgsql ? $"{column} IS NOT NULL" : $"[{column}] IS NOT NULL";
        b.Entity<Property>().ToTable("properties").HasIndex(x => x.Code).IsUnique();
        b.Entity<RoomType>().ToTable("room_types").HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
        b.Entity<Room>().ToTable("rooms").HasIndex(x => new { x.PropertyId, x.Number }).IsUnique();
        b.Entity<Guest>().ToTable("guests");
        b.Entity<Reservation>().ToTable("reservations").HasIndex(x => x.Code).IsUnique();
        b.Entity<Reservation>().HasIndex(x => new { x.PropertyId, x.RoomId, x.CheckInDate, x.CheckOutDate });
        b.Entity<Stay>().ToTable("stays").HasIndex(x => x.ReservationId).IsUnique();
        b.Entity<RoomTransferHistory>().ToTable("room_transfer_histories").HasIndex(x => new { x.StayId, x.EffectiveAtUtc });
        b.Entity<RoomTransferHistory>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RoomTransferHistory>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RoomTransferHistory>().HasOne<Stay>().WithMany().HasForeignKey(x => x.StayId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RoomTransferHistory>().HasOne<Room>().WithMany().HasForeignKey(x => x.OldRoomId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RoomTransferHistory>().HasOne<Room>().WithMany().HasForeignKey(x => x.NewRoomId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RoomTransferHistory>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RoomTransferHistory>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<GroupMembershipEvent>().ToTable("group_membership_events").HasIndex(x => new { x.PropertyId, x.NewGroupCode, x.CreatedAtUtc });
        b.Entity<GroupMembershipEvent>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<GroupMembershipEvent>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<GroupMembershipEvent>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReservationOccupant>().ToTable("reservation_occupants").HasIndex(x => x.ReservationId);
        b.Entity<ServiceItem>().ToTable("services").HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
        b.Entity<FolioCharge>().ToTable("folio_charges");
        b.Entity<Payment>().ToTable("payments").HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter(NotNullFilter("idempotency_key"));
        b.Entity<CashShift>().ToTable("cash_shifts");
        b.Entity<ShiftHandover>().ToTable("shift_handovers").HasIndex(x => x.CashShiftId).IsUnique();
        b.Entity<ShiftHandoverItem>().ToTable("shift_handover_items").HasIndex(x => x.HandoverId);
        b.Entity<ShiftHandoverNote>().ToTable("shift_handover_notes").HasIndex(x => x.HandoverId);
        b.Entity<ShiftHandover>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ShiftHandover>().HasOne<CashShift>().WithMany().HasForeignKey(x => x.CashShiftId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ShiftHandover>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.FromUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ShiftHandover>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.ToUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ShiftHandover>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.AcknowledgedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ShiftHandoverItem>().HasOne<ShiftHandover>().WithMany().HasForeignKey(x => x.HandoverId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ShiftHandoverNote>().HasOne<ShiftHandover>().WithMany().HasForeignKey(x => x.HandoverId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ShiftHandoverNote>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Agency>().ToTable("agencies").HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
        b.Entity<AuditEvent>().ToTable("audit_events");
        b.Entity<HousekeepingTask>().ToTable("housekeeping_tasks").HasIndex(x => new { x.PropertyId, x.Status });
        b.Entity<DeskNote>().ToTable("desk_notes").HasIndex(x => new { x.PropertyId, x.Status });
        b.Entity<BookingNoteType>().ToTable("booking_note_types");
        b.Entity<ReservationNote>().ToTable("reservation_notes").HasIndex(x => x.ReservationId);
        b.Entity<ReservationHold>().ToTable("reservation_holds").HasIndex(x => new { x.RoomId, x.Status, x.ExpiresAtUtc });
        b.Entity<HousekeepingTaskItem>().ToTable("housekeeping_task_items").HasIndex(x => new { x.TaskId, x.ChecklistCode }).IsUnique();
        b.Entity<HousekeepingTaskMedia>().ToTable("housekeeping_task_media");
        b.Entity<NightAuditLine>().ToTable("night_audit_lines").HasIndex(x => new { x.ReservationId, x.BusinessDate }).IsUnique();
        b.Entity<NotificationDispatch>().ToTable("notification_dispatches").HasIndex(x => x.IdempotencyKey).IsUnique();
        b.Entity<PaymentRefund>().ToTable("payment_refunds").HasIndex(x => x.IdempotencyKey).IsUnique();
        b.Entity<BookingNoteType>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReservationNote>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReservationNote>().HasOne<BookingNoteType>().WithMany().HasForeignKey(x => x.NoteTypeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReservationNote>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReservationHold>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReservationHold>().HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReservationHold>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ConvertedReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReservationHold>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<HousekeepingTaskItem>().HasOne<HousekeepingTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<HousekeepingTaskMedia>().HasOne<HousekeepingTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<HousekeepingTaskMedia>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<NightAuditLine>().HasOne<NightAuditLog>().WithMany().HasForeignKey(x => x.NightAuditLogId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<NightAuditLine>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<NightAuditLine>().HasOne<FolioCharge>().WithMany().HasForeignKey(x => x.FolioChargeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<NotificationDispatch>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<NotificationDispatch>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PaymentRefund>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PaymentRefund>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PaymentRefund>().HasOne<Payment>().WithMany().HasForeignKey(x => x.OriginalPaymentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PaymentRefund>().HasOne<Payment>().WithMany().HasForeignKey(x => x.PostedPaymentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PaymentRefund>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PaymentRefund>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FolioCharge>().HasOne<NightAuditLog>().WithMany().HasForeignKey(x => x.NightAuditLogId).OnDelete(DeleteBehavior.Restrict);
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
        // V5 foundation tables. Existing databases are upgraded by sql/06-v5-foundation-postgres.sql.
        b.Entity<Reservation>().HasOne<RatePlan>().WithMany().HasForeignKey(x => x.RatePlanId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AdjustmentVoucher>().ToTable("adjustment_vouchers").HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
        b.Entity<AdjustmentVoucher>().HasIndex(x => x.IdempotencyKey).IsUnique();
        b.Entity<AdjustmentVoucher>().HasIndex(x => new { x.ReservationId, x.Status });
        b.Entity<AdjustmentVoucher>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AdjustmentVoucher>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AdjustmentVoucher>().HasOne<FolioCharge>().WithMany().HasForeignKey(x => x.OriginalChargeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AdjustmentVoucher>().HasOne<FolioCharge>().WithMany().HasForeignKey(x => x.PostedChargeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TaxRule>().ToTable("tax_rules").HasIndex(x => new { x.PropertyId, x.Code, x.EffectiveFrom }).IsUnique();
        b.Entity<TaxRule>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<HolidayRate>().ToTable("holiday_rates").HasIndex(x => new { x.PropertyId, x.Code }).IsUnique();
        b.Entity<HolidayRate>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<NightAuditLog>().ToTable("night_audit_logs").HasIndex(x => new { x.PropertyId, x.BusinessDate }).IsUnique();
        b.Entity<NightAuditLog>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EInvoice>().ToTable("e_invoices").HasIndex(x => new { x.PropertyId, x.Status });
        b.Entity<EInvoice>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EInvoice>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ActionPermission>().ToTable("action_permissions").HasIndex(x => new { x.Role, x.PermissionKey, x.PropertyId }).IsUnique();
        b.Entity<ActionPermission>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        // OTA channel integration. Existing databases are upgraded by sql/07-ota-integration-postgres.sql.
        b.Entity<ChannelConnection>().ToTable("channel_connections").HasIndex(x => new { x.Provider, x.HotelCode }).IsUnique();
        b.Entity<ChannelConnection>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ChannelRoomMapping>().ToTable("channel_room_mappings").HasIndex(x => new { x.ConnectionId, x.ChannelRoomCode, x.ChannelRateCode }).IsUnique();
        b.Entity<ChannelRoomMapping>().HasOne<ChannelConnection>().WithMany().HasForeignKey(x => x.ConnectionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ChannelRoomMapping>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ChannelRoomMapping>().HasOne<RoomType>().WithMany().HasForeignKey(x => x.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ChannelRoomMapping>().HasOne<RatePlan>().WithMany().HasForeignKey(x => x.RatePlanId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ChannelBooking>().ToTable("channel_bookings").HasIndex(x => new { x.Provider, x.ChannelBookingId, x.PayloadHash }).IsUnique();
        b.Entity<ChannelBooking>().HasIndex(x => new { x.PropertyId, x.Status });
        b.Entity<ChannelBooking>().HasIndex(x => new { x.PropertyId, x.Fingerprint });
        b.Entity<ChannelBooking>().HasOne<ChannelConnection>().WithMany().HasForeignKey(x => x.ConnectionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ChannelBooking>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ChannelBooking>().HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ChannelSyncLog>().ToTable("channel_sync_logs").HasIndex(x => new { x.PropertyId, x.CreatedAtUtc });
        b.Entity<ChannelSyncLog>().HasOne<ChannelConnection>().WithMany().HasForeignKey(x => x.ConnectionId).OnDelete(DeleteBehavior.Restrict);
        if (isNpgsql) {
            b.Entity<Reservation>().Property(x => x.RatePlanSnapshot).HasColumnType("jsonb");
            b.Entity<EInvoice>().Property(x => x.RequestPayload).HasColumnType("jsonb");
            b.Entity<EInvoice>().Property(x => x.ResponsePayload).HasColumnType("jsonb");
        }
        // Word V1.0 naming convention: Database = snake_case, C# = PascalCase, JSON = camelCase.
        foreach (var type in b.Model.GetEntityTypes())
            foreach (var column in type.GetProperties())
                column.SetColumnName(Regex.Replace(column.Name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant());
    }
}
