namespace HotelPms.Api;

public static class StayRules
{
    public static string? PackageLength(string mode, DateOnly checkIn, DateOnly checkOut)
    {
        var nights = checkOut.DayNumber - checkIn.DayNumber;
        if (mode == "WEEK" && (nights < 7 || nights % 7 != 0)) return "Gói tuần cần số đêm chia hết cho 7.";
        if (mode == "MONTH" && (nights < 30 || nights % 30 != 0)) return "Gói tháng cần số đêm chia hết cho 30.";
        return null;
    }
}
