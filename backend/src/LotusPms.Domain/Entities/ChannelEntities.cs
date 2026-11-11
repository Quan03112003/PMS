using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

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
