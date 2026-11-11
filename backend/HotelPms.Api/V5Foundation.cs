using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HotelPms.Api;

public record AdjustmentVoucherInput(int PropertyId, int ReservationId, int? OriginalChargeId, string Kind,
    decimal AmountDelta, string Reason, string IdempotencyKey);
public record VoucherRejectInput(string Reason);
public record TaxRuleInput(int PropertyId, string Code, string Name, string TaxType, decimal RatePercent, bool IsInclusive,
    string AppliesTo, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public record HolidayRateInput(int PropertyId, string Code, string Name, DateOnly DateFrom, DateOnly DateTo, decimal AdjustmentPercent);
public record EInvoiceInput(int PropertyId, int? ReservationId, string CustomerName, string? CustomerTaxId, decimal TotalAmount, decimal TaxAmount);
public record PropertyTimeZoneInput(string TimeZoneId);
public record ReservationRatePlanInput(int RatePlanId, string Reason);

public static class V5Foundation
{
    private static readonly string[] VoucherKinds = ["CORRECTION", "REVERSAL", "WAIVER", "MANUAL_ADJUSTMENT"];
    private static readonly string[] TaxTypes = ["VAT", "SERVICE_CHARGE", "LOCAL_TAX"];
    private static readonly string[] TaxTargets = ["ROOM", "SERVICE", "ALL"];

    public static void MapV5Foundation(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.AddEndpointFilter(Ops.DepartmentAccess);

        api.MapGet("/adjustment-vouchers", async (int propertyId, string? status, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.CanManage(user) || !Ops.HasScope(user, propertyId)) return Results.Forbid();
            var query = db.AdjustmentVouchers.AsNoTracking().Where(x => x.PropertyId == propertyId);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.ToUpper());
            return Results.Ok(await query.OrderByDescending(x => x.Id).Take(200).ToListAsync());
        });

        api.MapPost("/adjustment-vouchers", async (AdjustmentVoucherInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            if (!Ops.ValidIdempotency(req.IdempotencyKey)) return Ops.Fail("idempotencyKey bắt buộc là UUID để chống ghi trùng phiếu");
            var existing = await db.AdjustmentVouchers.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == req.IdempotencyKey);
            if (existing != null) return existing.ReservationId == req.ReservationId && existing.AmountDelta == req.AmountDelta
                ? Results.Ok(existing) : Ops.Conflict("idempotencyKey đã dùng cho phiếu khác");
            var kind = req.Kind?.Trim().ToUpperInvariant() ?? "";
            if (!VoucherKinds.Contains(kind)) return Ops.Fail("Loại phiếu không hợp lệ (CORRECTION, REVERSAL, WAIVER, MANUAL_ADJUSTMENT)");
            if (req.AmountDelta == 0 || Math.Abs(req.AmountDelta) > 10000000000m) return Ops.Fail("Số tiền điều chỉnh phải khác 0 và trong giới hạn");
            if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Trim().Length < 5) return Ops.Fail("Cần nhập lý do điều chỉnh (tối thiểu 5 ký tự)");
            var reservation = await db.Reservations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.ReservationId && x.PropertyId == req.PropertyId);
            if (reservation == null) return Results.NotFound(new { message = "Không tìm thấy đặt phòng thuộc cơ sở" });
            if (req.OriginalChargeId.HasValue) {
                var original = await db.Charges.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.OriginalChargeId && x.ReservationId == reservation.Id);
                if (original == null) return Ops.Fail("Dòng folio gốc không thuộc đặt phòng này");
                if (req.AmountDelta < 0 && -req.AmountDelta > original.Amount) return Ops.Fail("Số tiền giảm vượt giá trị dòng folio gốc");
                if (kind == "REVERSAL" && req.AmountDelta != -original.Amount) return Ops.Fail("Phiếu đảo phải bằng âm toàn bộ dòng folio gốc");
                if (kind == "REVERSAL" && await db.AdjustmentVouchers.AnyAsync(x => x.OriginalChargeId == original.Id && x.Kind == "REVERSAL" && x.Status != "REJECTED"))
                    return Ops.Conflict("Dòng folio này đã có phiếu đảo");
            } else if (kind == "REVERSAL") return Ops.Fail("Phiếu đảo cần chọn dòng folio gốc");
            var voucher = new AdjustmentVoucher {
                Code = "ADJ-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), PropertyId = req.PropertyId,
                ReservationId = reservation.Id, OriginalChargeId = req.OriginalChargeId, Kind = kind, AmountDelta = req.AmountDelta,
                Reason = req.Reason.Trim(), CreatedByUserId = Ops.UserId(actor), IdempotencyKey = req.IdempotencyKey
            };
            db.AdjustmentVouchers.Add(voucher);
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, req.PropertyId, "ADJUSTMENT_VOUCHER_CREATE", "AdjustmentVoucher", voucher.Id, $"{kind} {req.AmountDelta}");
            return Results.Ok(voucher);
        });

        api.MapPost("/adjustment-vouchers/{id:int}/approve", async (int id, ClaimsPrincipal actor, PmsDbContext db) => {
            var voucher = await db.AdjustmentVouchers.FirstOrDefaultAsync(x => x.Id == id);
            if (voucher == null) return Results.NotFound();
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, voucher.PropertyId)) return Results.Forbid();
            if (voucher.Status != "PENDING") return Ops.Conflict("Chỉ duyệt được phiếu đang chờ duyệt");
            if (voucher.CreatedByUserId == Ops.UserId(actor)) return Ops.Conflict("Người lập phiếu không được tự duyệt phiếu của mình");
            voucher.Status = "APPROVED"; voucher.ApprovedByUserId = Ops.UserId(actor); voucher.ApprovedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, voucher.PropertyId, "ADJUSTMENT_VOUCHER_APPROVE", "AdjustmentVoucher", voucher.Id);
            return Results.Ok(voucher);
        });

        api.MapPost("/adjustment-vouchers/{id:int}/reject", async (int id, VoucherRejectInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            var voucher = await db.AdjustmentVouchers.FirstOrDefaultAsync(x => x.Id == id);
            if (voucher == null) return Results.NotFound();
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, voucher.PropertyId)) return Results.Forbid();
            if (voucher.Status is not ("PENDING" or "APPROVED")) return Ops.Conflict("Phiếu đã ghi sổ hoặc đã bị từ chối");
            if (string.IsNullOrWhiteSpace(req.Reason)) return Ops.Fail("Cần nhập lý do từ chối");
            voucher.Status = "REJECTED"; voucher.RejectedByUserId = Ops.UserId(actor); voucher.RejectedAtUtc = DateTime.UtcNow;
            voucher.RejectionReason = req.Reason.Trim();
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, voucher.PropertyId, "ADJUSTMENT_VOUCHER_REJECT", "AdjustmentVoucher", voucher.Id);
            return Results.Ok(voucher);
        });

        api.MapPost("/adjustment-vouchers/{id:int}/post", async (int id, ClaimsPrincipal actor, PmsDbContext db) => {
            var header = await db.AdjustmentVouchers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (header == null) return Results.NotFound();
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, header.PropertyId)) return Results.Forbid();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await Ops.LockReservation(db, header.ReservationId);
            var voucher = await db.AdjustmentVouchers.FirstAsync(x => x.Id == id);
            if (voucher.Status != "APPROVED") return Ops.Conflict("Phiếu chưa được duyệt hoặc đã ghi sổ");
            var reservation = await db.Reservations.AsNoTracking().FirstAsync(x => x.Id == voucher.ReservationId);
            if (reservation.Status != "CHECKED_IN") return Ops.Conflict("Chỉ ghi điều chỉnh vào folio của khách đang lưu trú");
            var total = await db.Charges.Where(x => x.ReservationId == reservation.Id).SumAsync(x => (decimal?)x.Amount) ?? 0;
            if (total + voucher.AmountDelta < 0) return Ops.Conflict("Điều chỉnh làm tổng folio âm");
            var description = $"{voucher.Code} {voucher.Kind}: {voucher.Reason}";
            var charge = new FolioCharge {
                ReservationId = reservation.Id, Category = "ADJUSTMENT", Quantity = 1, UnitPrice = voucher.AmountDelta, Amount = voucher.AmountDelta,
                Description = description.Length > 160 ? description[..160] : description
            };
            db.Charges.Add(charge);
            await db.SaveChangesAsync();
            voucher.Status = "POSTED"; voucher.PostedByUserId = Ops.UserId(actor); voucher.PostedAtUtc = DateTime.UtcNow; voucher.PostedChargeId = charge.Id;
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, voucher.PropertyId, "ADJUSTMENT_VOUCHER_POST", "FolioCharge", charge.Id, voucher.Code);
            await tx.CommitAsync();
            return Results.Ok(new { voucher, charge });
        });

        api.MapGet("/tax-rules", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.CanManage(user) || !Ops.HasScope(user, propertyId)) return Results.Forbid();
            return Results.Ok(await CatalogCache.TaxRulesFor(db, propertyId));
        });

        api.MapPost("/tax-rules", async (TaxRuleInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            var code = req.Code?.Trim().ToUpperInvariant() ?? "";
            var taxType = req.TaxType?.Trim().ToUpperInvariant() ?? "";
            var appliesTo = req.AppliesTo?.Trim().ToUpperInvariant() ?? "";
            if (code.Length == 0 || string.IsNullOrWhiteSpace(req.Name)) return Ops.Fail("Cần nhập mã và tên quy tắc thuế");
            if (!TaxTypes.Contains(taxType) || !TaxTargets.Contains(appliesTo)) return Ops.Fail("Loại thuế hoặc phạm vi áp dụng không hợp lệ");
            if (req.RatePercent < 0 || req.RatePercent > 100) return Ops.Fail("Thuế suất phải trong khoảng 0–100%");
            if (req.EffectiveTo.HasValue && req.EffectiveTo < req.EffectiveFrom) return Ops.Fail("Ngày hết hiệu lực phải sau ngày hiệu lực");
            if (await db.TaxRules.AnyAsync(x => x.PropertyId == req.PropertyId && x.Code == code && x.IsActive &&
                    x.EffectiveFrom <= (req.EffectiveTo ?? DateOnly.MaxValue) && (x.EffectiveTo ?? DateOnly.MaxValue) >= req.EffectiveFrom))
                return Ops.Conflict("Đã có quy tắc thuế cùng mã chồng lấn thời gian hiệu lực");
            var rule = new TaxRule {
                PropertyId = req.PropertyId, Code = code, Name = req.Name.Trim(), TaxType = taxType, RatePercent = req.RatePercent,
                IsInclusive = req.IsInclusive, AppliesTo = appliesTo, EffectiveFrom = req.EffectiveFrom, EffectiveTo = req.EffectiveTo,
                CreatedByUserId = Ops.UserId(actor)
            };
            db.TaxRules.Add(rule);
            await db.SaveChangesAsync();
            CatalogCache.ForgetTaxRules(req.PropertyId);
            await Ops.Log(db, actor, req.PropertyId, "TAX_RULE_CREATE", "TaxRule", rule.Id, $"{code} {req.RatePercent}%");
            return Results.Ok(rule);
        });

        api.MapGet("/holiday-rates", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.HasScope(user, propertyId)) return Results.Forbid();
            return Results.Ok(await db.HolidayRates.AsNoTracking().Where(x => x.PropertyId == propertyId).OrderBy(x => x.DateFrom).ToListAsync());
        });

        api.MapPost("/holiday-rates", async (HolidayRateInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            var code = req.Code?.Trim().ToUpperInvariant() ?? "";
            if (code.Length == 0 || string.IsNullOrWhiteSpace(req.Name)) return Ops.Fail("Cần nhập mã và tên kỳ lễ");
            if (req.DateTo < req.DateFrom) return Ops.Fail("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu");
            if (req.AdjustmentPercent < -100 || req.AdjustmentPercent > 500) return Ops.Fail("Tỷ lệ điều chỉnh phải trong khoảng -100% đến 500%");
            if (await db.HolidayRates.AnyAsync(x => x.PropertyId == req.PropertyId && x.Code == code)) return Ops.Conflict("Mã kỳ lễ đã tồn tại");
            var rate = new HolidayRate {
                PropertyId = req.PropertyId, Code = code, Name = req.Name.Trim(), DateFrom = req.DateFrom, DateTo = req.DateTo,
                AdjustmentPercent = req.AdjustmentPercent, CreatedByUserId = Ops.UserId(actor)
            };
            db.HolidayRates.Add(rate);
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, req.PropertyId, "HOLIDAY_RATE_CREATE", "HolidayRate", rate.Id, $"{code} {req.AdjustmentPercent}%");
            return Results.Ok(rate);
        });

        api.MapGet("/e-invoices", async (int propertyId, ClaimsPrincipal user, PmsDbContext db) => {
            if (!Ops.CanManage(user) || !Ops.HasScope(user, propertyId)) return Results.Forbid();
            return Results.Ok(await db.EInvoices.AsNoTracking().Where(x => x.PropertyId == propertyId).OrderByDescending(x => x.Id).Take(200).ToListAsync());
        });

        api.MapPost("/e-invoices", async (EInvoiceInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, req.PropertyId)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(req.CustomerName)) return Ops.Fail("Cần nhập tên người mua");
            if (req.TotalAmount <= 0 || req.TaxAmount < 0 || req.TaxAmount > req.TotalAmount) return Ops.Fail("Tổng tiền hoặc tiền thuế không hợp lệ");
            if (req.ReservationId.HasValue && !await db.Reservations.AnyAsync(x => x.Id == req.ReservationId && x.PropertyId == req.PropertyId))
                return Ops.Fail("Đặt phòng không thuộc cơ sở");
            var invoice = new EInvoice {
                PropertyId = req.PropertyId, ReservationId = req.ReservationId, CustomerName = req.CustomerName.Trim(),
                CustomerTaxId = req.CustomerTaxId?.Trim(), TotalAmount = req.TotalAmount, TaxAmount = req.TaxAmount,
                CreatedByUserId = Ops.UserId(actor)
            };
            db.EInvoices.Add(invoice);
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, req.PropertyId, "EINVOICE_DRAFT_CREATE", "EInvoice", invoice.Id);
            return Results.Ok(invoice);
        });

        api.MapPost("/e-invoices/{id:int}/send", async (int id, ClaimsPrincipal actor, PmsDbContext db) => {
            var invoice = await db.EInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (invoice == null) return Results.NotFound();
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, invoice.PropertyId)) return Results.Forbid();
            return Ops.Conflict("Chưa cấu hình nhà cung cấp hóa đơn điện tử; hóa đơn vẫn ở trạng thái nháp");
        });

        api.MapPatch("/properties/{id:int}/time-zone", async (int id, PropertyTimeZoneInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, id)) return Results.Forbid();
            var property = await db.Properties.FirstOrDefaultAsync(x => x.Id == id);
            if (property == null) return Results.NotFound();
            var zone = req.TimeZoneId?.Trim() ?? "";
            if ((zone != "UTC" && !zone.Contains('/')) || !TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _))
                return Ops.Fail("Múi giờ phải là mã IANA hợp lệ, ví dụ Asia/Ho_Chi_Minh");
            var before = property.TimeZoneId;
            property.TimeZoneId = zone;
            await db.SaveChangesAsync();
            await Ops.Log(db, actor, property.Id, "PROPERTY_TIMEZONE_UPDATE", "Property", property.Id, $"{before} -> {zone}");
            return Results.Ok(property);
        });

        api.MapPatch("/reservations/{id:int}/rate-plan", async (int id, ReservationRatePlanInput req, ClaimsPrincipal actor, PmsDbContext db) => {
            var header = await db.Reservations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (header == null) return Results.NotFound();
            if (!Ops.CanManage(actor) || !Ops.HasScope(actor, header.PropertyId)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(req.Reason)) return Ops.Fail("Cần nhập lý do đổi bảng giá");
            var plan = await db.RatePlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.RatePlanId && x.PropertyId == header.PropertyId && x.IsActive);
            if (plan == null) return Results.NotFound(new { message = "Bảng giá không thuộc cơ sở hoặc đã ngừng" });
            if (plan.Mode != header.BookingMode) return Ops.Conflict($"Bảng giá {plan.Mode} không khớp hình thức đặt phòng {header.BookingMode}");
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await Ops.LockReservation(db, id);
            var reservation = await db.Reservations.FirstAsync(x => x.Id == id);
            if (reservation.Status != "CONFIRMED") return Ops.Conflict("Chỉ đổi bảng giá trước khi nhận phòng");
            if (await Ops.OtaPendingConfirmation(db, id)) return Ops.Conflict("Booking OTA chưa hoàn tất xác nhận 2 lớp");
            var before = $"{reservation.RatePlanId?.ToString() ?? "none"}@{reservation.NightlyRate}";
            reservation.RatePlanId = plan.Id;
            reservation.NightlyRate = plan.BasePrice;
            reservation.RatePlanSnapshot = JsonSerializer.Serialize(new {
                ratePlanId = plan.Id, code = plan.Code, name = plan.Name, mode = plan.Mode, currency = "VND",
                basePrice = plan.BasePrice, includedHours = plan.IncludedHours, extraHourPrice = plan.ExtraHourPrice,
                version = 1, capturedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            var details = $"{before} -> {plan.Id}@{plan.BasePrice}: {req.Reason.Trim()}";
            await Ops.Log(db, actor, reservation.PropertyId, "RESERVATION_RATE_PLAN_CHANGE", "Reservation", reservation.Id,
                details.Length > 500 ? details[..500] : details);
            await tx.CommitAsync();
            return Results.Ok(reservation);
        });
    }
}
