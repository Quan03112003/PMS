using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HotelPms.Api;
public sealed class PropertyDaySnapshot
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public decimal RoomRevenue { get; set; }
    public decimal ServiceRevenue { get; set; }
    public decimal NoShowRevenue { get; set; }
    public decimal Payments { get; set; }
    public decimal Expenses { get; set; }
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class GroupMasterFolio
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(30)] public string Code { get; set; } = "";
    [System.ComponentModel.DataAnnotations.MaxLength(160)] public string GuestName { get; set; } = "";
    [System.ComponentModel.DataAnnotations.MaxLength(20)] public string Status { get; set; } = "OPEN";
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }
}

public sealed class GroupMasterLine
{
    public int Id { get; set; }
    public int FolioId { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(20)] public string Kind { get; set; } = "SERVICE";
    [System.ComponentModel.DataAnnotations.MaxLength(200)] public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class GroupMasterPayment
{
    public int Id { get; set; }
    public int FolioId { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(20)] public string Method { get; set; } = "CASH";
    public decimal Amount { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(80)] public string? Reference { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(80)] public string? IdempotencyKey { get; set; }
    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;
}
