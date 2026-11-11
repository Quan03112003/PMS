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
    [Precision(18,2)] public decimal MinNightlyRate { get; set; }
    [Precision(5,2)] public decimal MaxDiscountPercent { get; set; } = 15;
    [Precision(18,2)] public decimal? WeeklyRate { get; set; }
    [Precision(18,2)] public decimal? MonthlyRate { get; set; }
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
    [MaxLength(200)] public string? GiftNote { get; set; }
    [MaxLength(300)] public string? Preferences { get; set; }
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
    public bool RateConfidential { get; set; }
    [MaxLength(10)] public string? LoyaltyTier { get; set; }
    [Precision(5, 2)] public decimal LoyaltyPercent { get; set; }
    [Precision(18, 2)] public decimal ApprovedExposure { get; set; }
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
public sealed class CashAccount
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [MaxLength(80)] public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class CashMovement
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int AccountId { get; set; }
    public int? CounterAccountId { get; set; }
    [MaxLength(20)] public string MovementType { get; set; } = "DEPOSIT";
    [MaxLength(20)] public string Method { get; set; } = "CASH";
    [Precision(18, 2)] public decimal Amount { get; set; }
    [MaxLength(200)] public string? Note { get; set; }
    public int ActorUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class GroupCutoff
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    public DateOnly CutoffDate { get; set; }
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
    [MaxLength(10)] public string Priority { get; set; } = "NORMAL";
    [MaxLength(10)] public string ShiftCode { get; set; } = "MORNING";
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
    public bool PermissionsCustomized { get; set; }
}
public sealed class VirtualFolio
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(160)] public string GuestName { get; set; } = "";
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "OPEN";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class VirtualFolioLine
{
    public int Id { get; set; }
    public int FolioId { get; set; }
    public int? ServiceItemId { get; set; }
    [MaxLength(20)] public string Kind { get; set; } = "SERVICE";
    [MaxLength(160)] public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
    [Precision(18, 2)] public decimal UnitPrice { get; set; }
    [Precision(18, 2)] public decimal Amount { get; set; }
    public DateTime PostedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class VirtualFolioPayment
{
    public int Id { get; set; }
    public int FolioId { get; set; }
    [Precision(18, 2)] public decimal Amount { get; set; }
    [MaxLength(20)] public string Method { get; set; } = "CASH";
    public int? ShiftId { get; set; }
    [MaxLength(80)] public string? Reference { get; set; }
    [MaxLength(80)] public string? IdempotencyKey { get; set; }
    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class UserPermission
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [MaxLength(80)] public string PermissionKey { get; set; } = "";
}
