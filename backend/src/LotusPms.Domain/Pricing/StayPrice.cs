namespace HotelPms.Api;

public static class StayPrice
{
    public static string? Guard(RoomType type, string mode, decimal cardUnit, decimal? asked)
    {
        if (asked is not decimal price) return null;
        if (price < 0) return "Giá phòng không hợp lệ";
        var floor = Floor(type, mode, cardUnit);
        if (price + 0.01m < floor)
            return $"Giá {price:N0} thấp hơn sàn {floor:N0}. Trần giảm {type.MaxDiscountPercent:0.##}% so với giá niêm yết {cardUnit:N0}.";
        return null;
    }

    public static decimal Floor(RoomType type, string mode, decimal cardUnit)
    {
        var percent = type.MaxDiscountPercent < 0 ? 0 : type.MaxDiscountPercent > 100 ? 100 : type.MaxDiscountPercent;
        var discounted = cardUnit * (1 - percent / 100m);
        var block = mode == "WEEK" ? 7 : mode == "MONTH" ? 30 : 1;
        var minPackage = type.MinNightlyRate * block;
        if ((mode == "OVERNIGHT" || mode == "WEEK" || mode == "MONTH") && minPackage > discounted) return minPackage;
        return discounted < 0 ? 0 : discounted;
    }
}
