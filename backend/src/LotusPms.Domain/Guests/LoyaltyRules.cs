using System.Globalization;

namespace HotelPms.Api;

public static class LoyaltyRules
{
    public sealed record Tier(string Code, string Label, decimal Percent);
    public static readonly Tier[] Tiers =
    [
        new("", "Không giảm", 0),
        new("BAC", "Bạc", 5),
        new("VANG", "Vàng", 10)
    ];

    public static Tier Find(string? code)
    {
        var key = (code ?? "").Trim().ToUpperInvariant();
        var known = Tiers.FirstOrDefault(t => t.Code == key);
        if (known != null) return known;
        if (decimal.TryParse(key, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent) && percent > 0 && percent <= 100)
            return new Tier(percent.ToString("0.##", CultureInfo.InvariantCulture), "Tự nhập", percent);
        return Tiers[0];
    }

    public static decimal ClampPercent(decimal? asked, string? tierCode)
    {
        if (asked is not decimal percent) return Find(tierCode).Percent;
        if (percent < 0) return 0;
        return percent > 100 ? 100 : percent;
    }

    public static string LabelFor(decimal percent) => percent switch
    {
        5 => "Bạc",
        10 => "Vàng",
        > 0 => "Tự nhập",
        _ => "Không giảm"
    };

    public static string? CardFor(decimal percent, string? tierCode)
    {
        if (percent <= 0) return null;
        if (tierCode is "BAC" or "VANG" && Find(tierCode).Percent == percent) return tierCode;
        return percent.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public static decimal Payable(decimal card, decimal percent, decimal floor)
    {
        if (percent <= 0 || card <= 0) return card;
        var raw = decimal.Round(card * (1m - percent / 100m), 0, MidpointRounding.AwayFromZero);
        if (raw < 0) raw = 0;
        return raw < floor ? floor : raw;
    }
}
