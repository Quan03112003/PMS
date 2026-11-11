using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HotelPms.Api;

// PMS V2 extension: simple entities, all financially relevant actions additionally record an AuditEvent.
public sealed class HotelBuilding
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(160)] public string Name { get; set; } = "";
}
public sealed class RatePlan
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(20)] public string Mode { get; set; } = "OVERNIGHT"; // OVERNIGHT | DAY_USE | HOURLY
    [Precision(18,2)] public decimal BasePrice { get; set; }
    public int IncludedHours { get; set; } = 2;
    [Precision(18,2)] public decimal ExtraHourPrice { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class WaitlistEntry
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int? RoomTypeId { get; set; }
    [MaxLength(150)] public string GuestName { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "WAITING";
    [MaxLength(500)] public string? Note { get; set; }
    public int? ReservationId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class Supplier
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(160)] public string Name { get; set; } = "";
    [MaxLength(50)] public string Phone { get; set; } = "";
    [MaxLength(250)] public string? Address { get; set; }
}
public sealed class StockMovement
{
    public long Id { get; set; }
    public int PropertyId { get; set; }
    public int ServiceItemId { get; set; }
    [MaxLength(24)] public string Type { get; set; } = "IN"; // IN, OUT, SALE, CONSUMPTION, RETURN, ADJUST
    public int QuantityChange { get; set; } // signed: positive additions, negative withdrawals
    public int BalanceAfter { get; set; }
    [Precision(18,2)] public decimal UnitCost { get; set; }
    [MaxLength(150)] public string? Reference { get; set; }
    public int? SupplierId { get; set; }
    public int ActorUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class RetailSale
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int? ShiftId { get; set; }
    public int ServiceItemId { get; set; }
    public int Quantity { get; set; }
    [Precision(18,2)] public decimal UnitPrice { get; set; }
    [Precision(18,2)] public decimal TotalAmount { get; set; }
    [MaxLength(20)] public string PaymentMethod { get; set; } = "CASH";
    public int ActorUserId { get; set; }
    public DateTime SoldAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class CashVoucher
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(16)] public string Type { get; set; } = "EXPENSE";
    [MaxLength(20)] public string Method { get; set; } = "CASH";
    [MaxLength(160)] public string Category { get; set; } = "OTHER";
    [MaxLength(500)] public string Description { get; set; } = "";
    [Precision(18,2)] public decimal Amount { get; set; }
    public int ActorUserId { get; set; }
    public int? ShiftId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class DebtReceivable
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int? ReservationId { get; set; }
    public int? VirtualFolioId { get; set; }
    [Precision(18,2)] public decimal OriginalAmount { get; set; }
    [Precision(18,2)] public decimal OutstandingAmount { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "OPEN";
    [MaxLength(20)] public string Origin { get; set; } = "CHECKOUT";
    [MaxLength(400)] public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SettledAtUtc { get; set; }
}
public sealed class GuestAsset
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int StayId { get; set; }
    [MaxLength(200)] public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
    [MaxLength(500)] public string? Note { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReturnedAtUtc { get; set; }
}
public sealed class DailyClose
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public DateOnly BusinessDate { get; set; }
    [Precision(18,2)] public decimal RoomCharges { get; set; }
    [Precision(18,2)] public decimal ServiceCharges { get; set; }
    [Precision(18,2)] public decimal ReceivedAmount { get; set; }
    public int ActorUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

// PMS V5 foundation. Posted folio charges stay immutable; corrections are new charges linked to a voucher.
public sealed class AdjustmentVoucher
{
    public int Id { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    public int PropertyId { get; set; }
    public int ReservationId { get; set; }
    public int? OriginalChargeId { get; set; }
    [MaxLength(20)] public string Kind { get; set; } = "CORRECTION"; // CORRECTION | REVERSAL | WAIVER | MANUAL_ADJUSTMENT
    [Precision(18,2)] public decimal AmountDelta { get; set; } // signed: negative reduces the folio
    [MaxLength(500)] public string Reason { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "PENDING"; // PENDING | APPROVED | REJECTED | POSTED
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public int? RejectedByUserId { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    [MaxLength(500)] public string? RejectionReason { get; set; }
    public int? PostedByUserId { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public int? PostedChargeId { get; set; }
    [MaxLength(80)] public string IdempotencyKey { get; set; } = "";
}
public sealed class TaxRule
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(20)] public string TaxType { get; set; } = "VAT"; // VAT | SERVICE_CHARGE | LOCAL_TAX
    [Precision(7,4)] public decimal RatePercent { get; set; }
    public bool IsInclusive { get; set; }
    [MaxLength(20)] public string AppliesTo { get; set; } = "ALL"; // ROOM | SERVICE | ALL
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class HolidayRate
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(120)] public string Name { get; set; } = "";
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    [Precision(7,4)] public decimal AdjustmentPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
// Reserved for the V5.2 Night Audit run/lock/reopen workflow; no endpoint writes it yet.
public sealed class NightAuditLog
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public DateOnly BusinessDate { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "PENDING";
    [Precision(18,2)] public decimal RoomCharges { get; set; }
    [Precision(18,2)] public decimal ServiceCharges { get; set; }
    [Precision(18,2)] public decimal TotalPayments { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public int? LockedByUserId { get; set; }
    public DateTime? ReopenedAtUtc { get; set; }
    public int? ReopenedByUserId { get; set; }
    [MaxLength(500)] public string? ReopenReason { get; set; }
    public int RoomsSold { get; set; }
    public int RoomsAvailable { get; set; }
    [Precision(6, 2)] public decimal OccupancyPercent { get; set; }
    [Precision(18, 2)] public decimal Adr { get; set; }
    [Precision(18, 2)] public decimal Revpar { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
// Draft only: issuing requires a contracted e-invoice provider adapter (V6).
public sealed class EInvoice
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int? ReservationId { get; set; }
    [MaxLength(200)] public string CustomerName { get; set; } = "";
    [MaxLength(20)] public string? CustomerTaxId { get; set; }
    [Precision(18,2)] public decimal TotalAmount { get; set; }
    [Precision(18,2)] public decimal TaxAmount { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "DRAFT";
    [MaxLength(40)] public string? InvoiceNumber { get; set; }
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? IssuedAtUtc { get; set; }
}
// Reserved for V5.4 action-level RBAC; authorization still uses role checks in Ops.
public sealed class ActionPermission
{
    public int Id { get; set; }
    [MaxLength(30)] public string Role { get; set; } = "";
    [MaxLength(80)] public string PermissionKey { get; set; } = "";
    public int? PropertyId { get; set; }
    public bool IsAllowed { get; set; } = true;
}
