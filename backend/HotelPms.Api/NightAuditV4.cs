using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace HotelPms.Api;

public static class NightAuditV4
{
    public static void MapNightAuditV4(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.AddEndpointFilter(Ops.DepartmentAccess);

        // GET /api/night-audit/precheck - Pre-check conditions before creating daily close snapshot
        api.MapGet("/night-audit/precheck", async (int propertyId, DateOnly businessDate, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.HasScope(actor, propertyId)) return Results.Forbid();

            var prop = await db.Properties.FindAsync(propertyId);
            if (prop == null) return Results.NotFound("Không tìm thấy cơ sở");

            var openShifts = await db.Shifts
                .Where(s => s.PropertyId == propertyId && s.Status == "OPEN")
                .Select(s => new { s.Id, s.UserId, s.OpenedAtUtc, s.OpeningAmount, s.Status })
                .ToListAsync();

            var pendingArrivals = await db.Reservations
                .Where(r => r.PropertyId == propertyId && r.CheckInDate <= businessDate && r.Status == "CONFIRMED")
                .Select(r => new { r.Id, r.Code, r.RoomId, r.CheckInDate, r.CheckOutDate, r.Status })
                .ToListAsync();

            var pendingDepartures = await (from s in db.Stays
                                           join r in db.Reservations on s.ReservationId equals r.Id
                                           where r.PropertyId == propertyId && s.Status == "IN_HOUSE" && r.CheckOutDate <= businessDate
                                           select new { s.Id, r.Code, s.RoomId, r.CheckInDate, r.CheckOutDate, s.Status })
                                          .ToListAsync();

            var snapshotExists = await db.DailyCloses
                .AnyAsync(d => d.PropertyId == propertyId && d.BusinessDate == businessDate);

            var warnings = new List<string>();
            if (openShifts.Count > 0)
                warnings.Add($"Còn {openShifts.Count} ca thu ngân đang mở chưa chốt đóng.");
            if (pendingArrivals.Count > 0)
                warnings.Add($"Còn {pendingArrivals.Count} booking đến hạn trước hoặc trong ngày {businessDate:yyyy-MM-dd} chưa nhận phòng.");
            if (pendingDepartures.Count > 0)
                warnings.Add($"Còn {pendingDepartures.Count} khách đến hạn trả phòng trước hoặc trong ngày {businessDate:yyyy-MM-dd} đang lưu trú.");
            if (snapshotExists)
                warnings.Add($"Đã có snapshot chốt ngày cho ngày {businessDate:yyyy-MM-dd}. Nếu tạo tiếp sẽ trùng ngày kinh doanh.");

            bool canClose = openShifts.Count == 0 && pendingArrivals.Count == 0 && pendingDepartures.Count == 0 && !snapshotExists;

            return Results.Ok(new
            {
                propertyId,
                businessDate = businessDate.ToString("yyyy-MM-dd"),
                canClose,
                openShiftsCount = openShifts.Count,
                pendingArrivalsCount = pendingArrivals.Count,
                pendingDeparturesCount = pendingDepartures.Count,
                snapshotExists,
                warnings,
                openShifts,
                pendingArrivals,
                pendingDepartures,
                note = "Kiểm tra trước chốt snapshot ngày. Lưu ý: Đây là tiền kiểm tra dữ liệu, không phải khóa sổ kế toán hai bên (night audit)."
            });
        });
    }
}
