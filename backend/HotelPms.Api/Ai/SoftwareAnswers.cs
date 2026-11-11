using Microsoft.EntityFrameworkCore;

namespace HotelPms.Api;

public static class SoftwareAnswers
{
    public static readonly Guid GuideId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DataId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static async Task<IReadOnlyList<AiCitation>> CollectAsync(PmsDbContext db, string question, int? propertyId, string module, bool canSeeFinance, CancellationToken ct)
    {
        var parts = SoftwareGuide.Parts(question);
        var blocks = new List<string>();
        for (var i = 0; i < parts.Count; i++)
        {
            var block = await AnswerPartAsync(db, parts[i], propertyId, module, canSeeFinance, ct);
            if (block is null) block = AiAssistantPolicy.Abstain;
            blocks.Add(parts.Count == 1 ? block : $"Câu {i + 1}. {parts[i]}\n{block}");
        }
        if (blocks.All(x => x.Contains(AiAssistantPolicy.Abstain) && !x.Contains('\n'))) return [];
        return [new AiCitation(DataId, "Lotus PMS — dữ liệu và chức năng đang có", "Từng câu", string.Join("\n\n", blocks))];
    }

    public static async Task<AiFactBundle> CollectFactsAsync(PmsDbContext db, int? propertyId, bool canSeeFinance, CancellationToken ct)
    {
        if (propertyId is null or 0) return new AiFactBundle(false, null, null, [], []);
        var zone = await db.Properties.AsNoTracking().Where(p => p.Id == propertyId).Select(p => p.TimeZoneId).FirstOrDefaultAsync(ct);
        var today = V5Closeout.PropertyToday(zone);
        var counts = await LoadCounts(db, propertyId.Value, today, ct);
        var rooms = await LoadRooms(db, propertyId.Value, today, ct);
        var reports = new List<AiReportFact>();
        if (canSeeFinance)
        {
            foreach (var period in FactPeriods(today))
            {
                var numbers = await LoadReport(db, propertyId.Value, period.From, period.To, ct);
                reports.Add(new AiReportFact(period.Label, period.From.ToString("yyyy-MM-dd"), period.To.ToString("yyyy-MM-dd"), numbers.Room, numbers.Service, numbers.NoShow, numbers.Late, numbers.Retail, numbers.Outside, numbers.Expenses, numbers.Folio, numbers.Collected, numbers.Profit));
            }
        }
        return new AiFactBundle(true, today, new AiCountFacts(counts.InHouse, counts.Available, counts.Arrivals, counts.Departures, counts.Selling, counts.Broken, counts.Dirty, counts.Occupancy), rooms, reports);
    }

    static IEnumerable<(string Label, DateOnly From, DateOnly To)> FactPeriods(DateOnly today)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var previous = monthStart.AddMonths(-1);
        yield return ("Hôm nay", today, today);
        yield return ("Tháng này", monthStart, today);
        yield return ("Tháng trước", previous, previous.AddMonths(1).AddDays(-1));
        yield return ("Năm nay", new DateOnly(today.Year, 1, 1), today);
        yield return ("Năm trước", new DateOnly(today.Year - 1, 1, 1), new DateOnly(today.Year - 1, 12, 31));
    }
    static async Task<string?> AnswerPartAsync(PmsDbContext db, string part, int? propertyId, string module, bool canSeeFinance, CancellationToken ct)
    {
        if (!SoftwareGuide.WantsLiveData(part)) return SoftwareGuide.For(part, module);
        return await SnapshotAsync(db, part, propertyId, canSeeFinance, ct);
    }

    static async Task<string?> SnapshotAsync(PmsDbContext db, string question, int? propertyId, bool canSeeFinance, CancellationToken ct)
    {
        if (propertyId is null or 0) return "Hãy chọn một cơ sở trên thanh trên cùng rồi hỏi lại.";
        var zone = await db.Properties.AsNoTracking().Where(p => p.Id == propertyId).Select(p => p.TimeZoneId).FirstOrDefaultAsync(ct);
        var today = V5Closeout.PropertyToday(zone);
        var lines = new List<string>();
        if (SoftwareGuide.WantsMoney(question) && !canSeeFinance)
            lines.Add("Báo cáo doanh thu chỉ hiện với tài khoản có quyền báo cáo.");
        if (SoftwareGuide.WantsMoney(question) && canSeeFinance)
        {
            foreach (var period in SoftwareGuide.Periods(question, today))
                lines.Add(await ReportAsync(db, propertyId.Value, period.Label, period.From, period.To, ct));
        }
        if (SoftwareGuide.WantsCounts(question) || SoftwareGuide.WantsRoomList(question))
            await AddCounts(db, propertyId.Value, today, lines, ct);
        if (SoftwareGuide.WantsRoomList(question)) await AddRooms(db, question, propertyId.Value, today, lines, ct);
        var text = string.Join("\n", lines);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    static async Task AddCounts(PmsDbContext db, int propertyId, DateOnly today, List<string> lines, CancellationToken ct)
    {
        var counts = await LoadCounts(db, propertyId, today, ct);
        lines.Add("Cùng số trên Bảng điều khiển:");
        lines.Add($"• Đang ở: {counts.InHouse}");
        lines.Add($"• Sẵn sàng bán hôm nay: {counts.Available}");
        lines.Add($"• Đến hôm nay: {counts.Arrivals}");
        lines.Add($"• Trả hôm nay: {counts.Departures}");
        lines.Add($"• Phòng kinh doanh: {counts.Selling}. Phòng hỏng: {counts.Broken}. Phòng bẩn: {counts.Dirty}. Công suất: {counts.Occupancy}%.");
    }

    static async Task<CountSnapshot> LoadCounts(PmsDbContext db, int propertyId, DateOnly today, CancellationToken ct)
    {
        var nightEnd = today.AddDays(1);
        var operating = await db.Rooms.AsNoTracking().Where(r => r.PropertyId == propertyId && !r.IsOutOfOrder)
            .Select(r => new { r.Id, r.HousekeepingStatus }).ToListAsync(ct);
        var readyIds = operating.Where(r => r.HousekeepingStatus == "INSPECTED").Select(r => r.Id).ToList();
        var stayed = await db.Reservations.AsNoTracking().Where(r => r.PropertyId == propertyId && readyIds.Contains(r.RoomId)
            && (r.Status == "CONFIRMED" || r.Status == "CHECKED_IN") && r.CheckInDate < nightEnd && r.CheckOutDate > today)
            .Select(r => r.RoomId).Distinct().ToListAsync(ct);
        var held = await db.ReservationHolds.AsNoTracking().Where(h => h.PropertyId == propertyId && h.Status == "ACTIVE" && h.ExpiresAtUtc > DateTime.UtcNow
            && readyIds.Contains(h.RoomId) && h.CheckInDate < nightEnd && h.CheckOutDate > today)
            .Select(h => h.RoomId).Distinct().ToListAsync(ct);
        var available = readyIds.Count(id => !stayed.Contains(id) && !held.Contains(id));
        var selling = operating.Count;
        var broken = await db.Rooms.CountAsync(r => r.PropertyId == propertyId && r.IsOutOfOrder, ct);
        var dirty = await db.Rooms.CountAsync(r => r.PropertyId == propertyId && r.HousekeepingStatus == "DIRTY", ct);
        var inHouse = await db.Reservations.CountAsync(r => r.PropertyId == propertyId && r.Status == "CHECKED_IN", ct);
        var arrivals = await db.Reservations.CountAsync(r => r.PropertyId == propertyId && r.Status == "CONFIRMED" && r.CheckInDate == today, ct);
        var departures = await db.Reservations.CountAsync(r => r.PropertyId == propertyId && r.Status == "CHECKED_IN" && r.CheckOutDate == today, ct);
        var occupancy = selling == 0 ? 0 : Math.Round(inHouse * 100m / selling, 1);
        return new CountSnapshot(inHouse, available, arrivals, departures, selling, broken, dirty, occupancy);
    }

    static async Task AddRooms(PmsDbContext db, string question, int propertyId, DateOnly today, List<string> lines, CancellationToken ct)
    {
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.PropertyId == propertyId)
            .OrderBy(r => r.Floor).ThenBy(r => r.Number)
            .Select(r => new RoomRow(r.Id, r.Number, r.Floor, r.HousekeepingStatus, r.IsOutOfOrder))
            .ToListAsync(ct);
        var stays = await db.Reservations.AsNoTracking()
            .Where(r => r.PropertyId == propertyId && (r.Status == "CHECKED_IN" || r.Status == "CONFIRMED") && r.CheckOutDate > today && r.CheckInDate <= today)
            .Select(r => new StayRow(r.RoomId, r.Code, r.Status, r.CheckInDate, r.CheckOutDate))
            .ToListAsync(ct);
        var wanted = RoomNumbers(question);
        if (wanted.Count > 0) rooms = rooms.Where(r => wanted.Contains(r.Number)).ToList();
        else rooms = rooms.Where(r => stays.Any(s => s.RoomId == r.Id)).ToList();
        if (rooms.Count == 0) lines.Add(wanted.Count > 0 ? "Không thấy số phòng này ở cơ sở đang chọn." : "Hôm nay không có phiếu CHECKED_IN hoặc CONFIRMED còn hiệu lực.");
        foreach (var room in rooms.Take(30))
        {
            var stay = stays.FirstOrDefault(s => s.RoomId == room.Id);
            var stayText = stay is null ? "không có phiếu đang hiệu lực" : $"phiếu {stay.Code} {stay.Status}, nhận {stay.CheckInDate:dd/MM}, trả {stay.CheckOutDate:dd/MM}";
            var housekeeping = room.OutOfOrder ? "hỏng, không bán" : room.Housekeeping;
            lines.Add($"Phòng {room.Number}, tầng {room.Floor}: buồng {housekeeping}; {stayText}.");
        }
    }

    static async Task<IReadOnlyList<AiRoomFact>> LoadRooms(PmsDbContext db, int propertyId, DateOnly today, CancellationToken ct)
    {
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.PropertyId == propertyId)
            .OrderBy(r => r.Floor).ThenBy(r => r.Number)
            .Select(r => new RoomRow(r.Id, r.Number, r.Floor, r.HousekeepingStatus, r.IsOutOfOrder))
            .ToListAsync(ct);
        var stays = await db.Reservations.AsNoTracking()
            .Where(r => r.PropertyId == propertyId && (r.Status == "CHECKED_IN" || r.Status == "CONFIRMED") && r.CheckOutDate > today && r.CheckInDate <= today)
            .Select(r => new StayRow(r.RoomId, r.Code, r.Status, r.CheckInDate, r.CheckOutDate))
            .ToListAsync(ct);
        return rooms.Where(room => stays.Any(stay => stay.RoomId == room.Id)).Take(30).Select(room =>
        {
            var stay = stays.First(s => s.RoomId == room.Id);
            return new AiRoomFact(room.Number, room.Floor, room.Housekeeping, room.OutOfOrder, stay.Code, stay.Status, stay.CheckInDate.ToString("yyyy-MM-dd"), stay.CheckOutDate.ToString("yyyy-MM-dd"));
        }).ToList();
    }

    static async Task<string> ReportAsync(PmsDbContext db, int propertyId, string label, DateOnly startDate, DateOnly endDate, CancellationToken ct)
    {
        var numbers = await LoadReport(db, propertyId, startDate, endDate, ct);
        string Money(decimal amount) => amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " ₫";
        return string.Join("\n",
        [
            $"{label} ({startDate:dd/MM/yyyy} – {endDate:dd/MM/yyyy}), cùng Báo cáo doanh thu:",
            $"• Tiền phòng: {Money(numbers.Room)}",
            $"• Dịch vụ: {Money(numbers.Service)}",
            $"• No-show: {Money(numbers.NoShow)}",
            $"• Trả muộn: {Money(numbers.Late)}",
            $"• Bán lẻ: {Money(numbers.Retail)}",
            $"• Ngoài phòng: {Money(numbers.Outside)}",
            $"• Phiếu chi: {Money(numbers.Expenses)}",
            $"• Tổng phát sinh folio: {Money(numbers.Folio)}",
            $"• Đã thu trên folio: {Money(numbers.Collected)}",
            $"• Lãi lỗ vận hành: {Money(numbers.Profit)}",
            "Tiền phòng chỉ gồm hạng ROOM. No-show và trả muộn không cộng vào tiền phòng."
        ]);
    }

    static async Task<ReportNumbers> LoadReport(PmsDbContext db, int propertyId, DateOnly startDate, DateOnly endDate, CancellationToken ct)
    {
        var startAt = startDate.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var endAt = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var reservationIds = db.Reservations.Where(r => r.PropertyId == propertyId).Select(r => r.Id);
        var charges = await db.Charges.AsNoTracking()
            .Where(x => reservationIds.Contains(x.ReservationId) && x.PostedAtUtc >= startAt && x.PostedAtUtc < endAt)
            .GroupBy(x => x.Category)
            .Select(g => new { g.Key, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(ct);
        decimal Cat(string name) => charges.Where(x => x.Key == name).Sum(x => x.Amount);
        var roomRevenue = Cat("ROOM");
        var serviceRevenue = Cat("SERVICE");
        var noShowRevenue = Cat("NO_SHOW");
        var lateRevenue = Cat("LATE_CHECKOUT");
        var retailRevenue = await db.RetailSales.AsNoTracking().Where(x => x.PropertyId == propertyId && x.SoldAtUtc >= startAt && x.SoldAtUtc < endAt).SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0;
        var outsideRevenue = await (from line in db.VirtualFolioLines.AsNoTracking()
            join folio in db.VirtualFolios.AsNoTracking() on line.FolioId equals folio.Id
            where folio.PropertyId == propertyId && line.PostedAtUtc >= startAt && line.PostedAtUtc < endAt
            select (decimal?)line.Amount).SumAsync(ct) ?? 0;
        var expenses = await db.Vouchers.AsNoTracking().Where(x => x.PropertyId == propertyId && x.Type == "EXPENSE" && x.CreatedAtUtc >= startAt && x.CreatedAtUtc < endAt).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var collected = await db.Payments.AsNoTracking().Where(x => reservationIds.Contains(x.ReservationId) && x.PaidAtUtc >= startAt && x.PaidAtUtc < endAt).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var folioTotal = charges.Sum(x => x.Amount);
        var profit = roomRevenue + serviceRevenue + noShowRevenue + lateRevenue + retailRevenue + outsideRevenue - expenses;
        return new ReportNumbers(roomRevenue, serviceRevenue, noShowRevenue, lateRevenue, retailRevenue, outsideRevenue, expenses, folioTotal, collected, profit);
    }

    static List<string> RoomNumbers(string question) =>
        System.Text.RegularExpressions.Regex.Matches(question ?? "", @"\d{2,4}").Select(m => m.Value).Distinct().ToList();

    sealed record RoomRow(int Id, string Number, int Floor, string Housekeeping, bool OutOfOrder);
    sealed record StayRow(int RoomId, string Code, string Status, DateOnly CheckInDate, DateOnly CheckOutDate);
    sealed record CountSnapshot(int InHouse, int Available, int Arrivals, int Departures, int Selling, int Broken, int Dirty, decimal Occupancy);
    sealed record ReportNumbers(decimal Room, decimal Service, decimal NoShow, decimal Late, decimal Retail, decimal Outside, decimal Expenses, decimal Folio, decimal Collected, decimal Profit);
}

public sealed record AiFactBundle(bool PropertySelected, DateOnly? Today, AiCountFacts? Counts, IReadOnlyList<AiRoomFact> Rooms, IReadOnlyList<AiReportFact> Reports);
public sealed record AiCountFacts(int InHouse, int Available, int Arrivals, int Departures, int Selling, int Broken, int Dirty, decimal Occupancy);
public sealed record AiRoomFact(string Number, int Floor, string Housekeeping, bool OutOfOrder, string Code, string Status, string CheckIn, string CheckOut);
public sealed record AiReportFact(string Label, string Start, string End, decimal Room, decimal Service, decimal NoShow, decimal Late, decimal Retail, decimal Outside, decimal Expenses, decimal Folio, decimal Collected, decimal Profit);
