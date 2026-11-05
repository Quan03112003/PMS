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
    public int ReservationId { get; set; }
    [Precision(18,2)] public decimal OriginalAmount { get; set; }
    [Precision(18,2)] public decimal OutstandingAmount { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "OPEN";
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
