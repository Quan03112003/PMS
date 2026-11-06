using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace HotelPms.Api;

public record PricingQuoteRequest(
    int PropertyId,
    int RatePlanId,
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    DateTime? ActualCheckInLocal = null,
    DateTime? ActualCheckOutLocal = null
);

public record PricingAdjustmentRequest(
    string Kind,
    int RatePlanId,
    DateTime ActualAtLocal
);

public static class PricingV4
{
    public static void MapPricingV4(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.AddEndpointFilter(Ops.DepartmentAccess);

        // POST /api/pricing/quote - Calculates price quote server-side without writing to DB
        api.MapPost("/pricing/quote", async (PricingQuoteRequest req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            if (!Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            var prop = await db.Properties.FindAsync(req.PropertyId);
            if (prop == null) return Results.NotFound("Không tìm thấy cơ sở");

            var plan = await db.RatePlans.FindAsync(req.RatePlanId);
            if (plan == null) return Results.NotFound("Không tìm thấy bảng giá");
            if (plan.PropertyId != req.PropertyId) return Ops.Fail("Bảng giá không thuộc khách sạn");

            if (plan.Mode == "OVERNIGHT")
            {
                if (req.CheckOutDate <= req.CheckInDate)
                    return Ops.Fail("Ngày trả phòng phải sau ngày nhận phòng.");

                var nights = req.CheckOutDate.DayNumber - req.CheckInDate.DayNumber;
                var baseAmount = nights * plan.BasePrice;

                var standardCheckIn = req.CheckInDate.ToDateTime(prop.CheckInTime);
                var standardCheckOut = req.CheckOutDate.ToDateTime(prop.CheckOutTime);

                int earlyHours = 0;
                decimal earlyFee = 0;
                if (req.ActualCheckInLocal.HasValue && req.ActualCheckInLocal.Value < standardCheckIn)
                {
                    var diff = (standardCheckIn - req.ActualCheckInLocal.Value).TotalHours;
                    if (diff > 24) return Ops.Fail("Thời gian nhận sớm vượt quá 24 giờ. Cần đặt thêm đêm.");
                    earlyHours = (int)Math.Ceiling(diff);
                    earlyFee = earlyHours * plan.ExtraHourPrice;
                }

                int lateHours = 0;
                decimal lateFee = 0;
                if (req.ActualCheckOutLocal.HasValue && req.ActualCheckOutLocal.Value > standardCheckOut)
                {
                    var diff = (req.ActualCheckOutLocal.Value - standardCheckOut).TotalHours;
                    if (diff > 24) return Ops.Fail("Thời gian trả muộn vượt quá 24 giờ. Cần gia hạn đêm.");
                    lateHours = (int)Math.Ceiling(diff);
                    lateFee = lateHours * plan.ExtraHourPrice;
                }

                var totalAmount = baseAmount + earlyFee + lateFee;
                return Results.Ok(new
                {
                    propertyId = req.PropertyId,
                    ratePlanId = plan.Id,
                    ratePlanCode = plan.Code,
                    ratePlanName = plan.Name,
                    bookingMode = plan.Mode,
                    nights,
                    roomRate = plan.BasePrice,
                    baseAmount,
                    earlyHours,
                    earlyFee,
                    lateHours,
                    lateFee,
                    overtimeHours = 0,
                    overtimeFee = 0m,
                    totalAmount,
                    note = "Báo giá qua đêm tính theo máy chủ. Nhận sớm/trả muộn tính theo đơn giá giờ vượt chuẩn."
                });
            }
            else // HOURLY or DAY_USE
            {
                var checkIn = req.ActualCheckInLocal ?? req.CheckInDate.ToDateTime(prop.CheckInTime);
                var checkOut = req.ActualCheckOutLocal ?? req.CheckOutDate.ToDateTime(prop.CheckOutTime);

                if (checkOut <= checkIn)
                    return Ops.Fail("Giờ trả phòng phải sau giờ nhận phòng.");

                var duration = (checkOut - checkIn).TotalHours;
                int overtimeHours = 0;
                decimal overtimeFee = 0;

                if (duration > plan.IncludedHours)
                {
                    var diff = duration - plan.IncludedHours;
                    if (diff > 72) return Ops.Fail("Thời gian quá giờ vượt quá 72 giờ.");
                    overtimeHours = (int)Math.Ceiling(diff);
                    overtimeFee = overtimeHours * plan.ExtraHourPrice;
                }

                var totalAmount = plan.BasePrice + overtimeFee;
                return Results.Ok(new
                {
                    propertyId = req.PropertyId,
                    ratePlanId = plan.Id,
                    ratePlanCode = plan.Code,
                    ratePlanName = plan.Name,
                    bookingMode = plan.Mode,
                    nights = 0,
                    roomRate = plan.BasePrice,
                    baseAmount = plan.BasePrice,
                    earlyHours = 0,
                    earlyFee = 0m,
                    lateHours = 0,
                    lateFee = 0m,
                    overtimeHours,
                    overtimeFee,
                    totalAmount,
                    note = $"Báo giá {plan.Mode} trọn gói ({plan.IncludedHours}h đầu). Quá giờ tính thêm theo đơn giá vượt giờ."
                });
            }
        });

        // POST /api/stays/{stayId:int}/pricing-adjustments - Manager only posts surcharge with lock and conflict check
        api.MapPost("/stays/{stayId:int}/pricing-adjustments", async (int stayId, PricingAdjustmentRequest req, ClaimsPrincipal actor, PmsDbContext db) =>
        {
            var stay = await db.Stays.FindAsync(stayId);
            if (stay == null) return Results.NotFound("Không tìm thấy lượt lưu trú");
            if (stay.Status != "IN_HOUSE") return Ops.Conflict("Lượt lưu trú không ở trạng thái đang ở (IN_HOUSE)");

            var res = await db.Reservations.FindAsync(stay.ReservationId);
            if (res == null) return Results.NotFound("Không tìm thấy đơn đặt phòng liên kết");

            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, res.PropertyId))
                return Results.Forbid();

            var prop = await db.Properties.FindAsync(res.PropertyId);
            if (prop == null) return Results.NotFound("Không tìm thấy cơ sở");

            var plan = await db.RatePlans.FindAsync(req.RatePlanId);
            if (plan == null) return Results.NotFound("Không tìm thấy bảng giá");
            if (plan.PropertyId != res.PropertyId) return Ops.Conflict("Bảng giá không thuộc cơ sở của đơn đặt phòng");
            if (plan.Mode != res.BookingMode) return Ops.Conflict($"Bảng giá {plan.Mode} không khớp hình thức đặt phòng {res.BookingMode}");

            var kind = req.Kind?.Trim().ToUpperInvariant();
            if (kind is not ("EARLY_CHECKIN" or "LATE_CHECKOUT" or "OVERTIME"))
                return Ops.Fail("Loại phụ thu không hợp lệ (EARLY_CHECKIN, LATE_CHECKOUT, OVERTIME)");

            // Check future time (at most 15 minutes ahead of current VN local time UTC+7)
            var nowLocal = DateTime.UtcNow.AddHours(7);
            if (req.ActualAtLocal > nowLocal.AddMinutes(15))
                return Ops.Fail("Thời gian thực tế không được ở tương lai quá 15 phút.");

            using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await Ops.LockReservation(db, res.Id);

            // Prevent duplicate posting of the same kind for this reservation
            if (await db.Charges.AnyAsync(c => c.ReservationId == res.Id && c.Category == kind))
                return Ops.Conflict($"Đã ghi nhận phụ thu {kind} cho đơn đặt phòng này rồi.");

            int hours = 0;
            decimal amount = 0;
            string desc = "";

            if (kind == "EARLY_CHECKIN")
            {
                if (res.BookingMode != "OVERNIGHT")
                    return Ops.Fail("Phụ thu nhận sớm chỉ áp dụng cho đặt phòng qua đêm.");

                var stayCheckInLocal = stay.CheckedInAtUtc.AddHours(7);
                if (Math.Abs((req.ActualAtLocal - stayCheckInLocal).TotalMinutes) > 15)
                    return Ops.Fail("Thời gian nhận sớm sai lệch quá 15 phút so với giờ check-in thực tế đã lưu.");

                var standardCheckIn = res.CheckInDate.ToDateTime(prop.CheckInTime);
                if (req.ActualAtLocal >= standardCheckIn)
                    return Ops.Fail("Thời gian nhận phòng không sớm hơn giờ quy định.");

                var diff = (standardCheckIn - req.ActualAtLocal).TotalHours;
                if (diff > 24) return Ops.Fail("Thời gian nhận sớm vượt quá 24 giờ.");
                hours = (int)Math.Ceiling(diff);
                amount = hours * plan.ExtraHourPrice;
                desc = $"Phụ thu nhận sớm {hours} giờ ({plan.Code})";
            }
            else if (kind == "LATE_CHECKOUT")
            {
                if (res.BookingMode != "OVERNIGHT")
                    return Ops.Fail("Phụ thu trả muộn chỉ áp dụng cho đặt phòng qua đêm.");

                var standardCheckOut = res.CheckOutDate.ToDateTime(prop.CheckOutTime);
                if (req.ActualAtLocal <= standardCheckOut)
                    return Ops.Fail("Thời gian trả phòng không muộn hơn giờ quy định.");

                var diff = (req.ActualAtLocal - standardCheckOut).TotalHours;
                if (diff > 24) return Ops.Fail("Thời gian trả muộn vượt quá 24 giờ.");
                hours = (int)Math.Ceiling(diff);
                amount = hours * plan.ExtraHourPrice;
                desc = $"Phụ thu trả muộn {hours} giờ ({plan.Code})";
            }
            else if (kind == "OVERTIME")
            {
                if (res.BookingMode is not ("HOURLY" or "DAY_USE"))
                    return Ops.Fail("Phụ thu quá giờ chỉ áp dụng cho đặt phòng nghỉ giờ hoặc ngày.");

                var checkInLocal = stay.CheckedInAtUtc.AddHours(7);
                var duration = (req.ActualAtLocal - checkInLocal).TotalHours;
                if (duration <= plan.IncludedHours)
                    return Ops.Fail($"Thời gian sử dụng ({duration:F1}h) chưa vượt số giờ gói ({plan.IncludedHours}h).");

                var diff = duration - plan.IncludedHours;
                if (diff > 72) return Ops.Fail("Thời gian quá giờ vượt quá 72 giờ.");
                hours = (int)Math.Ceiling(diff);
                amount = hours * plan.ExtraHourPrice;
                desc = $"Phụ thu quá giờ {hours} giờ ({plan.Code})";
            }

            if (amount <= 0)
                return Ops.Fail("Số tiền phụ thu tính ra bằng 0 (đơn giá phụ thu bảng giá = 0).");

            var charge = new FolioCharge
            {
                ReservationId = res.Id,
                Category = kind,
                Description = desc,
                Quantity = hours,
                UnitPrice = plan.ExtraHourPrice,
                Amount = amount,
                PostedAtUtc = DateTime.UtcNow
            };
            db.Charges.Add(charge);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await Ops.Log(db, actor, res.PropertyId, "PRICING_ADJUSTMENT", "FolioCharge", charge.Id);
            return Results.Ok(charge);
        });
    }

    // Static calculation helper exposed for pure unit testing
    public static (int nights, decimal baseAmount, int earlyHours, decimal earlyFee, int lateHours, decimal lateFee, decimal total)
        CalculateOvernight(TimeOnly checkInTime, TimeOnly checkOutTime, decimal basePrice, decimal extraHourPrice,
            DateOnly checkInDate, DateOnly checkOutDate, DateTime? actualCheckInLocal, DateTime? actualCheckOutLocal)
    {
        if (checkOutDate <= checkInDate) throw new ArgumentException("Check-out must be after check-in date.");
        int nights = checkOutDate.DayNumber - checkInDate.DayNumber;
        decimal baseAmount = nights * basePrice;

        var stdIn = checkInDate.ToDateTime(checkInTime);
        var stdOut = checkOutDate.ToDateTime(checkOutTime);

        int earlyHours = 0;
        decimal earlyFee = 0;
        if (actualCheckInLocal.HasValue && actualCheckInLocal.Value < stdIn)
        {
            var diff = (stdIn - actualCheckInLocal.Value).TotalHours;
            if (diff > 24) throw new ArgumentOutOfRangeException(nameof(actualCheckInLocal), "Early check-in exceeds 24h.");
            earlyHours = (int)Math.Ceiling(diff);
            earlyFee = earlyHours * extraHourPrice;
        }

        int lateHours = 0;
        decimal lateFee = 0;
        if (actualCheckOutLocal.HasValue && actualCheckOutLocal.Value > stdOut)
        {
            var diff = (actualCheckOutLocal.Value - stdOut).TotalHours;
            if (diff > 24) throw new ArgumentOutOfRangeException(nameof(actualCheckOutLocal), "Late check-out exceeds 24h.");
            lateHours = (int)Math.Ceiling(diff);
            lateFee = lateHours * extraHourPrice;
        }

        return (nights, baseAmount, earlyHours, earlyFee, lateHours, lateFee, baseAmount + earlyFee + lateFee);
    }

    public static (int overtimeHours, decimal overtimeFee, decimal total)
        CalculateHourlyDayUse(decimal basePrice, int includedHours, decimal extraHourPrice,
            DateTime checkInLocal, DateTime checkOutLocal)
    {
        if (checkOutLocal <= checkInLocal) throw new ArgumentException("Check-out must be after check-in.");
        var duration = (checkOutLocal - checkInLocal).TotalHours;
        int overtimeHours = 0;
        decimal overtimeFee = 0;
        if (duration > includedHours)
        {
            var diff = duration - includedHours;
            if (diff > 72) throw new ArgumentOutOfRangeException(nameof(checkOutLocal), "Overtime exceeds 72h.");
            overtimeHours = (int)Math.Ceiling(diff);
            overtimeFee = overtimeHours * extraHourPrice;
        }
        return (overtimeHours, overtimeFee, basePrice + overtimeFee);
    }
}
