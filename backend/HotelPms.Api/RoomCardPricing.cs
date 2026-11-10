using System.Text.Json;

namespace HotelPms.Api;

public static class RoomCardPricing
{
    public sealed record Line(string Category, string Description, int Quantity, decimal UnitPrice, decimal Amount);

    public static bool IsWeekend(DateOnly day) => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static (List<Line> Lines, decimal RoomUnit) Build(RoomType type, RatePlan? plan, string mode, DateOnly checkIn, DateOnly checkOut, int adults, int children, int? plannedHours, decimal? roomOverride)
    {
        var lines = new List<Line>();
        var nights = Math.Max(1, checkOut.DayNumber - checkIn.DayNumber);
        if (mode == "OVERNIGHT")
        {
            var weekday = 0;
            var weekend = 0;
            for (var day = checkIn; day < checkOut; day = day.AddDays(1))
            {
                if (IsWeekend(day)) weekend++; else weekday++;
            }
            var weekdayPrice = roomOverride ?? plan?.BasePrice ?? type.WeekdayRate ?? type.BaseRate;
            var weekendPrice = roomOverride ?? (plan != null ? plan.BasePrice : type.WeekendRate ?? type.WeekdayRate ?? type.BaseRate);
            if (weekday > 0) lines.Add(new Line("ROOM", $"{weekday} đêm ngày thường", weekday, weekdayPrice, weekday * weekdayPrice));
            if (weekend > 0) lines.Add(new Line("ROOM", $"{weekend} đêm cuối tuần", weekend, weekendPrice, weekend * weekendPrice));
        }
        else if (mode == "HOURLY")
        {
            var block = roomOverride ?? plan?.BasePrice ?? type.HourlyRate ?? type.BaseRate;
            var included = plan?.IncludedHours > 0 ? plan.IncludedHours : 2;
            lines.Add(new Line("ROOM", $"Nghỉ giờ {included} giờ đầu", 1, block, block));
            var extraRate = plan?.ExtraHourPrice ?? type.ExtraHourRate ?? 0;
            var extra = Math.Max(0, (plannedHours ?? included) - included);
            if (extra > 0 && extraRate > 0) lines.Add(new Line("OVERTIME", $"Thêm {extra} giờ", extra, extraRate, extra * extraRate));
            nights = 1;
        }
        else
        {
            var dayPrice = roomOverride ?? plan?.BasePrice ?? (IsWeekend(checkIn) ? type.WeekendRate ?? type.WeekdayRate ?? type.BaseRate : type.WeekdayRate ?? type.BaseRate);
            lines.Add(new Line("ROOM", IsWeekend(checkIn) ? "Nghỉ ngày cuối tuần" : "Nghỉ ngày thường", 1, dayPrice, dayPrice));
            nights = 1;
        }

        var extraGuests = Math.Max(0, adults + children - Math.Max(1, type.Capacity));
        var extraPerson = type.ExtraPersonRate ?? 0;
        if (extraGuests > 0 && extraPerson > 0)
        {
            var qty = extraGuests * nights;
            lines.Add(new Line("EXTRA_PERSON", $"Phụ thu {extraGuests} khách", qty, extraPerson, qty * extraPerson));
        }

        var roomLines = lines.Where(x => x.Category == "ROOM").ToList();
        var roomUnit = roomLines.Count == 1 ? roomLines[0].UnitPrice : roomLines.Sum(x => x.Amount) / Math.Max(1, roomLines.Sum(x => x.Quantity));
        return (lines, roomUnit);
    }

    public static string Serialize(string source, string mode, IReadOnlyList<Line> lines, RoomType type, RatePlan? plan, decimal? roomOverride)
    {
        var weekday = roomOverride ?? plan?.BasePrice ?? type.WeekdayRate ?? type.BaseRate;
        var weekend = roomOverride ?? (plan != null ? plan.BasePrice : type.WeekendRate ?? type.WeekdayRate ?? type.BaseRate);
        return JsonSerializer.Serialize(new
        {
            source, mode, lines, total = lines.Sum(x => x.Amount), lockedAtUtc = DateTime.UtcNow,
            weekdayUnit = weekday, weekendUnit = weekend,
            hourlyUnit = roomOverride ?? plan?.BasePrice ?? type.HourlyRate ?? type.BaseRate,
            extraHourUnit = plan?.ExtraHourPrice ?? type.ExtraHourRate ?? 0,
            extraPersonUnit = type.ExtraPersonRate ?? 0,
            capacity = Math.Max(1, type.Capacity)
        });
    }

    public static List<Line> ExtendNights(string? json, DateOnly from, DateOnly to, int adults, int children)
    {
        if (string.IsNullOrWhiteSpace(json) || from >= to) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("weekdayUnit", out var weekdayNode)) return [];
            var weekday = weekdayNode.GetDecimal();
            var weekend = root.GetProperty("weekendUnit").GetDecimal();
            var extraPerson = root.GetProperty("extraPersonUnit").GetDecimal();
            var capacity = root.GetProperty("capacity").GetInt32();
            var lines = new List<Line>();
            var weekdayNights = 0;
            var weekendNights = 0;
            for (var day = from; day < to; day = day.AddDays(1))
            {
                if (IsWeekend(day)) weekendNights++; else weekdayNights++;
            }
            if (weekdayNights > 0) lines.Add(new Line("ROOM", $"Gia hạn {weekdayNights} đêm ngày thường", weekdayNights, weekday, weekdayNights * weekday));
            if (weekendNights > 0) lines.Add(new Line("ROOM", $"Gia hạn {weekendNights} đêm cuối tuần", weekendNights, weekend, weekendNights * weekend));
            var extraGuests = Math.Max(0, adults + children - capacity);
            var nights = weekdayNights + weekendNights;
            if (extraGuests > 0 && extraPerson > 0)
                lines.Add(new Line("EXTRA_PERSON", $"Phụ thu {extraGuests} khách gia hạn", extraGuests * nights, extraPerson, extraGuests * nights * extraPerson));
            return lines;
        }
        catch (JsonException) { return []; }
    }

    public static decimal LockedTotal(string? json) => Read(json).Sum(x => x.Amount);

    public static List<Line> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array) return [];
            var result = new List<Line>();
            foreach (var line in lines.EnumerateArray())
            {
                var amount = line.GetProperty("amount").GetDecimal();
                if (amount <= 0) continue;
                result.Add(new Line(
                    line.GetProperty("category").GetString() ?? "ROOM",
                    line.GetProperty("description").GetString() ?? "Tiền phòng",
                    line.GetProperty("quantity").GetInt32(),
                    line.GetProperty("unitPrice").GetDecimal(),
                    amount));
            }
            return result;
        }
        catch (JsonException) { return []; }
    }
}
