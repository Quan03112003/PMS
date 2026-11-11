namespace HotelPms.Api;

public static class TaxMath
{
    public static bool Matches(string appliesTo, string category) => appliesTo == "ALL" || appliesTo == category;

    public static decimal Amount(decimal baseAmount, decimal ratePercent, bool inclusive) =>
        inclusive ? baseAmount - baseAmount / (1 + ratePercent / 100m) : baseAmount * ratePercent / 100m;
}
