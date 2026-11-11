using System.Text.Json;
using System.Text.Json.Nodes;

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
        else if (mode is "WEEK" or "MONTH")
        {
            var block = mode == "WEEK" ? 7 : 30;
            var blocks = Math.Max(1, nights / block);
            var unit = roomOverride ?? (mode == "WEEK"
                ? type.WeeklyRate ?? ((type.WeekdayRate ?? type.BaseRate) * 7)
                : type.MonthlyRate ?? ((type.WeekdayRate ?? type.BaseRate) * 30));
            var label = mode == "WEEK" ? $"{blocks} tuần" : $"{blocks} tháng (30 đêm)";
            lines.Add(new Line("ROOM", label, blocks, unit, blocks * unit));
            nights = blocks;
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

    static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);

    public static string Serialize(string source, string mode, IReadOnlyList<Line> lines, RoomType type, RatePlan? plan, decimal? roomOverride, IReadOnlyList<NightAmount>? nights = null)
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
            capacity = Math.Max(1, type.Capacity),
            nights = nights?.OrderBy(x => x.Date).Select(x => new { date = x.Date, amount = x.Amount })
        }, SnapshotJson);
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

    public static string RetargetCard(string? json, RoomType type, string mode, DateOnly checkIn, DateOnly checkOut, int adults, int children)
    {
        var weekday = type.WeekdayRate ?? type.BaseRate;
        var weekend = type.WeekendRate ?? weekday;
        var hourly = type.HourlyRate ?? type.BaseRate;
        var extraHour = type.ExtraHourRate ?? 0m;
        var extraPerson = type.ExtraPersonRate ?? 0m;
        var capacity = Math.Max(1, type.Capacity);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var node = JsonNode.Parse(json)!.AsObject();
                node["weekdayUnit"] = weekday;
                node["weekendUnit"] = weekend;
                node["hourlyUnit"] = hourly;
                node["extraHourUnit"] = extraHour;
                node["extraPersonUnit"] = extraPerson;
                node["capacity"] = capacity;
                node["source"] = "ROOM_MOVE";
                return node.ToJsonString();
            }
            catch (JsonException) { }
        }
        var start = checkOut > checkIn ? checkIn : checkOut.AddDays(-1);
        var (lines, _) = Build(type, null, mode, start, checkOut > start ? checkOut : start.AddDays(1), Math.Max(1, adults), Math.Max(0, children), null, null);
        return Serialize("ROOM_CARD", mode, lines, type, null, null);
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
                if (!Field(line, "amount", out var amountNode)) continue;
                var amount = amountNode.GetDecimal();
                if (amount <= 0) continue;
                result.Add(new Line(
                    Field(line, "category", out var category) ? category.GetString() ?? "ROOM" : "ROOM",
                    Field(line, "description", out var description) ? description.GetString() ?? "Tiền phòng" : "Tiền phòng",
                    Field(line, "quantity", out var quantity) ? quantity.GetInt32() : 1,
                    Field(line, "unitPrice", out var unitPrice) ? unitPrice.GetDecimal() : amount,
                    amount));
            }
            return result;
        }
        catch (JsonException) { return []; }
    }

    static bool Field(JsonElement line, string camel, out JsonElement value)
    {
        if (line.TryGetProperty(camel, out value)) return true;
        var pascal = char.ToUpperInvariant(camel[0]) + camel[1..];
        return line.TryGetProperty(pascal, out value);
    }

    public sealed record NightAmount(DateOnly Date, decimal Amount);
    public sealed record NightCard(DateOnly Date, decimal Card, decimal Payable, decimal Floor);

    public static List<NightCard> Cards(DateOnly checkIn, DateOnly checkOut, decimal weekday, decimal weekend, RoomType type, decimal percent)
    {
        var list = new List<NightCard>();
        if (checkIn >= checkOut) return list;
        for (var day = checkIn; day < checkOut; day = day.AddDays(1))
        {
            var card = IsWeekend(day) ? weekend : weekday;
            var floor = StayPrice.Floor(type, "OVERNIGHT", card);
            list.Add(new NightCard(day, card, LoyaltyRules.Payable(card, percent, floor), floor));
        }
        return list;
    }

    public static string? RejectNights(IReadOnlyList<NightCard> cards, IReadOnlyList<NightAmount> asked)
    {
        if (asked.Count != cards.Count) return "Cần nhập giá cho đúng từng đêm của phiếu.";
        foreach (var card in cards)
        {
            var row = asked.FirstOrDefault(x => x.Date == card.Date);
            if (row == null) return $"Thiếu giá đêm {card.Date:dd/MM/yyyy}.";
            if (row.Amount + 0.01m < card.Floor)
                return $"Đêm {card.Date:dd/MM/yyyy}: giá {row.Amount:N0} thấp hơn sàn {card.Floor:N0}.";
        }
        return null;
    }

    public static List<Line> WithNights(List<Line> lines, IReadOnlyList<NightAmount> nights)
    {
        var room = nights.OrderBy(x => x.Date).Select(n => new Line("ROOM", $"Đêm {n.Date:dd/MM/yyyy}", 1, n.Amount, n.Amount)).ToList();
        room.AddRange(lines.Where(x => x.Category != "ROOM"));
        return room;
    }

    public static List<NightAmount> ReadNights(string? json, DateOnly checkIn, DateOnly checkOut)
    {
        var list = new List<NightAmount>();
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("nights", out var nights) && nights.ValueKind == JsonValueKind.Array)
                {
                    foreach (var row in nights.EnumerateArray())
                        list.Add(new NightAmount(DateOnly.Parse(row.GetProperty("date").GetString()!), row.GetProperty("amount").GetDecimal()));
                    if (list.Count > 0) return list.OrderBy(x => x.Date).ToList();
                }
                if (doc.RootElement.TryGetProperty("weekdayUnit", out var weekdayNode))
                {
                    var weekday = weekdayNode.GetDecimal();
                    var weekend = doc.RootElement.GetProperty("weekendUnit").GetDecimal();
                    for (var day = checkIn; day < checkOut; day = day.AddDays(1))
                        list.Add(new NightAmount(day, IsWeekend(day) ? weekend : weekday));
                    return list;
                }
            }
            catch (JsonException) { }
            catch (FormatException) { }
        }
        return list;
    }
}
