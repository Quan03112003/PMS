using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HotelPms.Api;
public sealed class ShiftHandover
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int CashShiftId { get; set; }
    public int FromUserId { get; set; }
    public int ToUserId { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "SUBMITTED";
    [Precision(18, 2)] public decimal OpeningAmount { get; set; }
    [Precision(18, 2)] public decimal ExpectedCash { get; set; }
    [Precision(18, 2)] public decimal DeclaredCash { get; set; }
    [Precision(18, 2)] public decimal Variance { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAtUtc { get; set; }
    public int? AcknowledgedByUserId { get; set; }
}
public sealed class ShiftHandoverItem
{
    public int Id { get; set; }
    public int HandoverId { get; set; }
    [MaxLength(24)] public string Kind { get; set; } = "";
    [MaxLength(40)] public string Reference { get; set; } = "";
    [MaxLength(240)] public string Summary { get; set; } = "";
}
public sealed class ShiftHandoverNote
{
    public int Id { get; set; }
    public int HandoverId { get; set; }
    public int AuthorUserId { get; set; }
    [MaxLength(500)] public string Text { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
