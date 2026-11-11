namespace HotelPms.Api;

public static class SoftwareGuide
{
    public static bool WantsLiveData(string question)
    {
        var text = Normalize(question);
        if (IsHowTo(text)) return false;
        return WantsMoney(question) || text.Contains("phong") || text.Contains("trang thai") || text.Contains("bao nhieu")
            || text.Contains("so luong") || text.Contains("cong suat") || text.Contains("dang o")
            || text.Contains("hom nay") || text.Contains("so do") || HasRoomNumber(question);
    }

    public static string? For(string question, string module)
    {
        var text = Normalize(question);
        var picked = Sections.Where(section => section.Modules.Contains(module) || section.Keys.Any(text.Contains)).Take(2).ToList();
        if (picked.Count == 0 && IsHowTo(text)) picked = Sections.Where(section => section.Modules.Contains(module)).Take(1).ToList();
        if (picked.Count == 0) return null;
        return string.Join("\n\n", picked.Select(section => section.Title + "\n" + section.Body));
    }

    public static bool WantsMoney(string question)
    {
        var text = Normalize(question);
        if (IsHowTo(text)) return false;
        return text.Contains("bao cao") || text.Contains("doanh thu") || text.Contains("tien phong")
            || text.Contains("loi nhuan") || text.Contains("phieu chi") || text.Contains("thang nay")
            || text.Contains("thang truoc") || text.Contains("nam nay") || text.Contains("nam ngoai")
            || text.Contains("nam truoc");
    }

    public static bool WantsCounts(string question)
    {
        var text = Normalize(question);
        if (IsHowTo(text) || WantsMoney(question)) return false;
        return text.Contains("bao nhieu") || text.Contains("so luong") || text.Contains("cong suat")
            || text.Contains("dang o") || text.Contains("hom nay") || text.Contains("phong") || text.Contains("trang thai");
    }

    public static bool WantsRoomList(string question)
    {
        var text = Normalize(question);
        if (IsHowTo(text)) return false;
        return text.Contains("phong nao") || text.Contains("nhung phong") || text.Contains("danh sach phong")
            || (text.Contains("phong") && HasRoomNumber(question));
    }

    public static IReadOnlyList<(string Label, DateOnly From, DateOnly To)> Periods(string question, DateOnly today)
    {
        if (!WantsMoney(question)) return [];
        var text = Normalize(question);
        var periods = new List<(string Label, DateOnly From, DateOnly To)>();
        if (text.Contains("hom nay")) periods.Add(("Hôm nay", today, today));
        if (text.Contains("thang nay")) periods.Add(("Tháng này", new DateOnly(today.Year, today.Month, 1), today));
        if (text.Contains("thang truoc"))
        {
            var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
            periods.Add(("Tháng trước", first, first.AddMonths(1).AddDays(-1)));
        }
        if (text.Contains("nam nay")) periods.Add(("Năm nay", new DateOnly(today.Year, 1, 1), today));
        if (text.Contains("nam ngoai") || text.Contains("nam truoc"))
            periods.Add(("Năm trước", new DateOnly(today.Year - 1, 1, 1), new DateOnly(today.Year - 1, 12, 31)));
        if (periods.Count == 0) periods.Add(("Tháng này", new DateOnly(today.Year, today.Month, 1), today));
        return periods;
    }

    public static IReadOnlyList<string> Parts(string question)
    {
        var parts = (question ?? "").Split(['.', '?', '!', '\n', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim()).Where(part => part.Length >= 2).ToList();
        return parts.Count == 0 ? [(question ?? "").Trim()] : parts;
    }

    public static bool HasRoomNumber(string question) =>
        System.Text.RegularExpressions.Regex.IsMatch(question ?? "", @"\d{2,4}");

    static bool IsHowTo(string text) =>
        text.Contains("lam ") || text.Contains("cach ") || text.Contains("the nao") || text.Contains("buoc")
        || text.Contains("o dau") || text.Contains("chuc nang") || text.Contains("loi thuong") || text.Contains("bi loi");

    static string Normalize(string? value)
    {
        var text = (value ?? "").ToLowerInvariant();
        text = text.Replace('à', 'a').Replace('á', 'a').Replace('ạ', 'a').Replace('ả', 'a').Replace('ã', 'a')
            .Replace('â', 'a').Replace('ầ', 'a').Replace('ấ', 'a').Replace('ậ', 'a').Replace('ẩ', 'a').Replace('ẫ', 'a')
            .Replace('ă', 'a').Replace('ằ', 'a').Replace('ắ', 'a').Replace('ặ', 'a').Replace('ẳ', 'a').Replace('ẵ', 'a')
            .Replace('è', 'e').Replace('é', 'e').Replace('ẹ', 'e').Replace('ẻ', 'e').Replace('ẽ', 'e')
            .Replace('ê', 'e').Replace('ề', 'e').Replace('ế', 'e').Replace('ệ', 'e').Replace('ể', 'e').Replace('ễ', 'e')
            .Replace('ì', 'i').Replace('í', 'i').Replace('ị', 'i').Replace('ỉ', 'i').Replace('ĩ', 'i')
            .Replace('ò', 'o').Replace('ó', 'o').Replace('ọ', 'o').Replace('ỏ', 'o').Replace('õ', 'o')
            .Replace('ô', 'o').Replace('ồ', 'o').Replace('ố', 'o').Replace('ộ', 'o').Replace('ổ', 'o').Replace('ỗ', 'o')
            .Replace('ơ', 'o').Replace('ờ', 'o').Replace('ớ', 'o').Replace('ợ', 'o').Replace('ở', 'o').Replace('ỡ', 'o')
            .Replace('ù', 'u').Replace('ú', 'u').Replace('ụ', 'u').Replace('ủ', 'u').Replace('ũ', 'u')
            .Replace('ư', 'u').Replace('ừ', 'u').Replace('ứ', 'u').Replace('ự', 'u').Replace('ử', 'u').Replace('ữ', 'u')
            .Replace('ỳ', 'y').Replace('ý', 'y').Replace('ỵ', 'y').Replace('ỷ', 'y').Replace('ỹ', 'y')
            .Replace('đ', 'd');
        return text;
    }

    static readonly Section[] Sections =
    [
        new(["BOOKING"], ["dat phong", "booking", "giu phong", "tao dat"], "Đặt phòng",
            "Mở Đặt phòng & lễ tân → Đặt phòng → Danh sách đặt phòng, rồi tạo đặt phòng. Chọn phòng, ngày nhận, ngày trả và xác nhận. Nhận từ hôm nay trở về trước chỉ giữ phòng khi buồng phòng đang INSPECTED. Gói tuần cần số đêm chia hết cho 7. Gói tháng cần số đêm chia hết cho 30."),
        new(["CHECK_IN", "ROOM_MANAGEMENT"], ["nhan phong", "check-in", "checkin", "check in"], "Nhận phòng",
            "Phiếu phải đang CONFIRMED. Trên Danh sách đặt phòng bấm Check-in, hoặc trên Sơ đồ phòng bấm Nhận phòng khi phòng đang đến. Phần mềm từ chối nếu chưa đến ngày nhận, phòng không INSPECTED, hoặc phòng đã có khách đang ở."),
        new(["CHECK_OUT", "FOLIO", "PAYMENT"], ["tra phong", "check-out", "checkout", "check out", "folio"], "Trả phòng",
            "Mở folio của phiếu đang CHECKED_IN. Còn phải thanh toán khác 0 thì nút Check-out không bấm được. Thu hết rồi mới trả phòng."),
        new(["ROOM_MANAGEMENT", "HOUSEKEEPING"], ["so do", "trang thai phong", "buong", "ve sinh", "inspected", "dirty"], "Trạng thái phòng",
            "Sơ đồ phòng nằm ở Đặt phòng & lễ tân → Lưu trú → Sơ đồ phòng. Buồng phòng INSPECTED mới bán cho ngày nhận hôm nay. DIRTY là phòng bẩn, CLEAN là đã dọn, phòng hỏng hiện MAINTENANCE. Khách đang ở hiện OCCUPIED."),
        new(["CASHIER", "PAYMENT", "FOLIO"], ["thu tien", "ca thu", "tien mat", "city ledger", "cong no"], "Thu tiền",
            "Thu tiền trong folio. Tiền mặt cần ca thu ngân đang mở. City Ledger ghi nợ và đóng phiếu khách. Công nợ xem ở Thanh toán & công nợ → Công nợ / khôi phục."),
        new(["HOUSEKEEPING"], ["don phong", "buong phong"], "Buồng phòng",
            "Mở Buồng phòng & dịch vụ → Buồng phòng. Trạng thái INSPECTED là đã kiểm, dùng để bán. Đổi trạng thái vệ sinh không tự nhận phòng và không tự thu tiền."),
        new(["REPORT"], ["bao cao", "doanh thu"], "Báo cáo",
            "Mở Báo cáo → Báo cáo doanh thu. Số liệu lấy từ phiếu và folio đang có, không phải số model tự tính."),
        new(["OTHER", "DASHBOARD", "SYSTEM_ERROR"], ["chuc nang", "o dau", "menu"], "Vị trí chức năng",
            "Đặt phòng và sơ đồ nằm trong Đặt phòng & lễ tân. Buồng phòng nằm trong Buồng phòng & dịch vụ. Ca thu ngân và công nợ nằm trong Thanh toán & công nợ. Kho tri thức AI là chỗ duyệt tài liệu, không phải khung hỏi đáp. Khung hỏi đáp là nút Hỏi góc phải.")
    ];

    sealed record Section(string[] Modules, string[] Keys, string Title, string Body);
}
