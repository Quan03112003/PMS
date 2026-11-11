using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace HotelPms.Api;

public static class StaffAccess
{
    public sealed record Item(string Key, string Group, string Label);

    public static readonly Item[] Catalog =
    [
        new("property.operate", "Quản lý", "Vận hành toàn bộ cơ sở, trừ việc riêng của quản trị chuỗi"),
        new("user.manage", "Nhân sự", "Tạo và sửa tài khoản, cấp quyền"),
        new("chain.property", "Chuỗi", "Thêm cơ sở khách sạn"),
        new("price.guard", "Giá", "Sửa giá sàn và trần giảm"),
        new("rate.confidential", "Giá", "Gắn hoặc gỡ bảo mật giá"),
        new("debt.approve", "Tiền", "Duyệt hạn mức nợ"),
        new("stay.front", "Lễ tân", "Sơ đồ phòng, đặt phòng, khách, nhận và trả phòng"),
        new("stay.checkin", "Lễ tân", "Nhận phòng"),
        new("stay.checkout", "Lễ tân", "Trả phòng"),
        new("reservation.edit", "Lễ tân", "Tạo, sửa và hủy đặt phòng"),
        new("folio.service", "Dịch vụ", "Ghi mọi dịch vụ vào hóa đơn"),
        new("folio.minibar", "Buồng phòng", "Chỉ ghi minibar và giặt ủi vào hóa đơn"),
        new("cashier", "Thu ngân", "Ca, thu tiền, cọc, phiếu thu chi"),
        new("housekeeping.update", "Buồng phòng", "Cập nhật vệ sinh và phiếu dọn"),
        new("room.maintain", "Bảo trì", "Khóa và mở phòng bảo trì"),
        new("room.configure", "Phòng", "Thêm hạng phòng và phòng"),
        new("report.finance", "Báo cáo", "Doanh thu, dòng tiền, công suất"),
        new("stock.manage", "Kho", "Nhập xuất tồn"),
        new("invoice.draft", "Hóa đơn", "Xem và lập hóa đơn nháp")
    ];

    static readonly Dictionary<string, string[]> Templates = new()
    {
        ["CHAIN_ADMIN"] = Catalog.Select(x => x.Key).ToArray(),
        ["PROPERTY_MANAGER"] = Catalog.Select(x => x.Key).Where(k => k is not ("price.guard" or "chain.property")).ToArray(),
        ["FRONT_DESK_AGENT"] = ["stay.front", "stay.checkin", "stay.checkout", "reservation.edit", "folio.service", "cashier", "housekeeping.update"],
        ["SALES_AGENT"] = ["reservation.edit", "report.finance"],
        ["HOUSEKEEPING_AGENT"] = ["housekeeping.update", "folio.minibar"],
        ["MAINTENANCE_AGENT"] = ["room.maintain", "housekeeping.update"],
        ["ACCOUNTING_AGENT"] = ["report.finance", "cashier", "invoice.draft", "stock.manage"]
    };

    public static string[] TemplateOf(string role) => Templates.TryGetValue(role, out var keys) ? keys : [];

    public sealed record Access(HashSet<string> Keys, bool Customized);

    static readonly ConcurrentDictionary<int, (DateTime At, Access Access)> Cache = new();

    public static void Forget(int userId) => Cache.TryRemove(userId, out _);

    public static async Task<Access> Load(PmsDbContext db, ClaimsPrincipal user)
    {
        var id = Ops.UserId(user);
        if (Cache.TryGetValue(id, out var hit) && DateTime.UtcNow - hit.At < TimeSpan.FromSeconds(45)) return hit.Access;
        var row = await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Role, x.PermissionsCustomized }).FirstOrDefaultAsync();
        Access access;
        if (row == null) access = new Access([], true);
        else if (!row.PermissionsCustomized) access = new Access(TemplateOf(row.Role).ToHashSet(), false);
        else
        {
            var granted = await db.UserPermissions.AsNoTracking().Where(x => x.UserId == id).Select(x => x.PermissionKey).ToListAsync();
            access = new Access(granted.ToHashSet(), true);
        }
        Cache[id] = (DateTime.UtcNow, access);
        return access;
    }

    public static bool Allows(string method, string path, ClaimsPrincipal user, Access access)
    {
        var keys = access.Keys;
        if (path.StartsWith("/api/ai", StringComparison.Ordinal)) return true;
        if (method == "POST" && Regex.IsMatch(path, @"^/api/stays/\d+/services$"))
            return keys.Contains("folio.service") || keys.Contains("stay.front") || keys.Contains("property.operate") || keys.Contains("folio.minibar");
        var needed = RequiredKey(method, path);
        if (needed != null) return keys.Contains(needed);
        if (keys.Contains("property.operate")) return true;
        if (keys.Contains("stay.front") && !IsUserAdmin(path) && !IsPriceGuard(path) && !IsChainProperty(method, path)) return true;
        if (Keyed(method, path, keys)) return true;
        if (access.Customized) return false;
        return RolePath(method, path, user.FindFirstValue(ClaimTypes.Role) ?? "", keys);
    }

    public static string? RequiredKey(string method, string path)
    {
        if (path.StartsWith("/api/reports/", StringComparison.Ordinal) && path != "/api/reports/registration-csv")
            return path.Contains("/stock", StringComparison.Ordinal) ? "stock.manage" : "report.finance";
        if (path is "/api/audit" or "/api/daily-closes" || path.StartsWith("/api/adjustment", StringComparison.Ordinal)) return "report.finance";
        if (path.StartsWith("/api/e-invoices", StringComparison.Ordinal)) return "invoice.draft";
        if (path.StartsWith("/api/users", StringComparison.Ordinal)) return "user.manage";
        if (IsPriceGuard(path)) return "price.guard";
        if (IsChainProperty(method, path)) return "chain.property";
        if (Regex.IsMatch(path, @"^/api/reservations/\d+/check-in$") && method == "POST") return "stay.checkin";
        if (Regex.IsMatch(path, @"^/api/stays/\d+/checkout$") && method == "POST") return "stay.checkout";
        if (method == "POST" && path is "/api/rooms" or "/api/room-types") return "room.configure";
        if (Regex.IsMatch(path, @"^/api/rooms/\d+/maintenance$")) return "room.maintain";
        if (method == "POST" && (path == "/api/services" || Regex.IsMatch(path, @"^/api/services/\d+/restock$"))) return "stock.manage";
        if (path.StartsWith("/api/cash-accounts", StringComparison.Ordinal)) return "report.finance";
        return null;
    }

    static bool Keyed(string method, string path, HashSet<string> keys)
    {
        var read = method == "GET";
        if (keys.Contains("cashier") && (path is "/api/shifts" or "/api/vouchers" or "/api/shift-handovers" or "/api/shift-handover-receivers"
            || Regex.IsMatch(path, @"^/api/(shifts/\d+(/reconcile|/handover-preview)?|shift-handovers/\d+)$"))
            && (read || method == "POST")) return true;
        if (keys.Contains("cashier") && method == "POST" && (path is "/api/shifts/open" or "/api/vouchers" || Regex.IsMatch(path, @"^/api/(shifts/\d+/(close|handover)|shift-handovers/\d+/(acknowledge|notes)|reservations/\d+/deposit|stays/\d+/payments)$"))) return true;
        if (keys.Contains("reservation.edit") && read && path is "/api/dashboard" or "/api/availability" or "/api/guests" or "/api/reservations" or "/api/agencies" or "/api/rate-plans" or "/api/booking-note-types" or "/api/rooms" or "/api/room-types" or "/api/properties") return true;
        if (keys.Contains("reservation.edit") && method == "POST" && (path is "/api/reservations" or "/api/group-bookings" || Regex.IsMatch(path, @"^/api/reservations/\d+/cancel$"))) return true;
        if (keys.Contains("report.finance") && read && (path is "/api/dashboard" or "/api/reservations" or "/api/stays" or "/api/services" or "/api/guests" or "/api/agencies" or "/api/debts" or "/api/daily-closes" or "/api/shifts/exposure" || Regex.IsMatch(path, @"^/api/(stays/\d+/folio|guests/\d+/debts)$"))) return true;
        if (keys.Contains("housekeeping.update") && (
            (read && path is "/api/dashboard" or "/api/rooms" or "/api/room-types" or "/api/properties" or "/api/housekeeping-tasks" or "/api/housekeeping/folio-board" or "/api/housekeeping/productivity" or "/api/housekeeping/stock-alerts")
            || (method == "PATCH" && Regex.IsMatch(path, @"^/api/(rooms/\d+/housekeeping|housekeeping-tasks/\d+|housekeeping-task-items/\d+)$"))
            || (method == "POST" && Regex.IsMatch(path, @"^/api/housekeeping-tasks/\d+/(submit|media)$")))) return true;
        if (keys.Contains("folio.minibar") && read && (path is "/api/dashboard" or "/api/stays" or "/api/services" or "/api/rooms" || Regex.IsMatch(path, @"^/api/stays/\d+/folio$"))) return true;
        if (keys.Contains("stock.manage") && read && path is "/api/services" or "/api/dashboard") return true;
        if (keys.Contains("invoice.draft") && path.StartsWith("/api/e-invoices", StringComparison.Ordinal)) return true;
        return false;
    }

    static bool IsUserAdmin(string path) => path.StartsWith("/api/users", StringComparison.Ordinal);
    static bool IsPriceGuard(string path) => path.Contains("/price-guard", StringComparison.Ordinal);
    static bool IsChainProperty(string method, string path) => method == "POST" && path == "/api/properties";

    static bool RolePath(string method, string path, string role, HashSet<string> keys)
    {
        var common = method == "GET" && path is "/api/properties" or "/api/dashboard" or "/api/rooms" or "/api/room-types";
        return role switch
        {
            "SALES_AGENT" => common || (method == "GET" && path is "/api/availability" or "/api/guests" or "/api/reservations" or "/api/agencies" or "/api/rate-plans" or "/api/booking-note-types" or "/api/reports/summary" or "/api/reports/agencies" or "/api/reports/nationality")
                || (method == "POST" && (path is "/api/reservations" or "/api/group-bookings" || Regex.IsMatch(path, @"^/api/reservations/\d+/(cancel|deposit)$"))),
            "HOUSEKEEPING_AGENT" => common || (method == "GET" && path is "/api/housekeeping-tasks" or "/api/housekeeping/folio-board" or "/api/housekeeping/productivity" or "/api/housekeeping/stock-alerts")
                || (method == "PATCH" && Regex.IsMatch(path, @"^/api/(rooms/\d+/housekeeping|housekeeping-tasks/\d+|housekeeping-task-items/\d+)$"))
                || (method == "POST" && (Regex.IsMatch(path, @"^/api/housekeeping-tasks/\d+/(submit|media)$") || (keys.Contains("folio.minibar") && Regex.IsMatch(path, @"^/api/stays/\d+/services$")))),
            "MAINTENANCE_AGENT" => common || (method == "PATCH" && Regex.IsMatch(path, @"^/api/rooms/\d+/maintenance$")),
            "ACCOUNTING_AGENT" => common || (method == "GET" && (path is "/api/reservations" or "/api/stays" or "/api/services" or "/api/shifts" or "/api/shifts/exposure" or "/api/agencies" or "/api/reports/summary" or "/api/reports/chain" or "/api/reports/nationality" or "/api/reports/stock" or "/api/reports/stock-card" or "/api/reports/cash-ledger" or "/api/reports/receipts" or "/api/vouchers" or "/api/debts" or "/api/daily-closes" or "/api/shift-handovers" or "/api/shift-handover-receivers" || Regex.IsMatch(path, @"^/api/(stays/\d+/folio|guests/\d+/debts|shifts/\d+/reconcile|shifts/\d+/handover-preview|shift-handovers/\d+)$")))
                || (method == "POST" && (path is "/api/shifts/open" or "/api/vouchers" or "/api/daily-closes" || Regex.IsMatch(path, @"^/api/(shifts/\d+/close|shifts/\d+/handover|shift-handovers/\d+/(acknowledge|notes)|reservations/\d+/deposit|stays/\d+/payments)$"))),
            _ => false
        };
    }

    public static bool MinibarOnly(Access access) => access.Keys.Contains("folio.minibar") && !access.Keys.Contains("folio.service") && !access.Keys.Contains("stay.front") && !access.Keys.Contains("property.operate");

    public static bool IsMinibarOrLaundry(string code) =>
        code.StartsWith("MIN-", StringComparison.OrdinalIgnoreCase) || code.Equals("LAUNDRY", StringComparison.OrdinalIgnoreCase);
}
