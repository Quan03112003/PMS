using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace HotelPms.Api;

public static class Loyalty
{
    public const int StayMark = 3;
    public sealed record Tier(string Code, string Label, decimal Percent);
    public static readonly Tier[] Tiers = LoyaltyRules.Tiers.Select(t => new Tier(t.Code, t.Label, t.Percent)).ToArray();

    public static Tier Find(string? code)
    {
        var found = LoyaltyRules.Find(code);
        return new Tier(found.Code, found.Label, found.Percent);
    }

    public static string LabelOf(string? code) => Find(code).Label;

    public static decimal ClampPercent(decimal? asked, string? tierCode) => LoyaltyRules.ClampPercent(asked, tierCode);

    public static string LabelFor(decimal percent) => LoyaltyRules.LabelFor(percent);

    public static string? CardFor(decimal percent, string? tierCode) => LoyaltyRules.CardFor(percent, tierCode);

    public static decimal Payable(decimal card, decimal percent, decimal floor) => LoyaltyRules.Payable(card, percent, floor);

    public sealed record Evidence(int? GuestId, string? FullName, string Tier, string? GiftNote, int StayCount, bool Qualified, string Reason, string? Preferences = null);

    public static async Task<Evidence> LookUp(PmsDbContext db, int propertyId, string? phone, string? giftDraft)
    {
        var number = (phone ?? "").Trim();
        var draft = string.IsNullOrWhiteSpace(giftDraft) ? null : giftDraft.Trim();
        if (number.Length < 8)
            return new Evidence(null, null, "", draft, 0, false, "Chưa đối chiếu được lần ở. Vẫn chọn bậc hoặc nhập phần trăm giảm.");
        var known = await db.Guests.AsNoTracking().Where(g => g.Phone == number)
            .Select(g => new { g.Id, g.FullName, g.LoyaltyCard, g.GiftNote, g.Preferences }).ToListAsync();
        var ids = known.Select(g => g.Id).ToArray();
        var atProperty = ids.Length == 0 ? [] : await db.Reservations.AsNoTracking()
            .Where(r => r.PropertyId == propertyId && ids.Contains(r.GuestId))
            .Select(r => new { r.GuestId, r.Status }).ToListAsync();
        var here = atProperty.Select(r => r.GuestId).ToHashSet();
        var local = known.Where(g => here.Contains(g.Id)).ToList();
        var profile = local.OrderByDescending(g => g.Id).FirstOrDefault();
        var stays = atProperty.Count(r => r.Status is "CHECKED_IN" or "CHECKED_OUT");
        var gift = draft ?? local.Select(g => g.GiftNote).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        var tier = Find(local.OrderByDescending(g => g.Id).Select(g => g.LoyaltyCard).FirstOrDefault(c => Find(c).Percent > 0) ?? profile?.LoyaltyCard).Code;
        var qualified = stays >= StayMark || !string.IsNullOrWhiteSpace(gift);
        var stayed = stays > 0 ? $"Đã ở {stays} lần" : "Chưa có lần ở";
        var present = string.IsNullOrWhiteSpace(gift) ? "chưa ghi quà" : $"quà: {gift}";
        var reason = $"{stayed}, {present}. Chọn bậc hoặc nhập % , giá tự tính.";
        var preferences = local.Select(g => g.Preferences).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        return new Evidence(profile?.Id, profile?.FullName, tier, gift, stays, qualified, reason, preferences);
    }
}
