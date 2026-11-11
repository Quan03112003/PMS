using HotelPms.Api;

namespace LotusPms.UnitTests;

public class MoneyRulesTests
{
    static RoomType Standard() => new()
    {
        BaseRate = 650000, WeekdayRate = 650000, WeekendRate = 750000,
        WeeklyRate = 4550000, Capacity = 2, MaxDiscountPercent = 15, MinNightlyRate = 0
    };

    [Fact]
    public void Overnight_floor_is_fifteen_percent_off_the_card()
    {
        Assert.Equal(552500m, StayPrice.Floor(Standard(), "OVERNIGHT", 650000m));
    }

    [Fact]
    public void Week_floor_is_fifteen_percent_off_the_package()
    {
        Assert.Equal(3867500m, StayPrice.Floor(Standard(), "WEEK", 4550000m));
    }

    [Fact]
    public void Nobody_books_below_the_floor()
    {
        var message = StayPrice.Guard(Standard(), "WEEK", 4550000m, 1000m);
        Assert.Equal($"Giá {1000m:N0} thấp hơn sàn {3867500m:N0}. Trần giảm 15% so với giá niêm yết {4550000m:N0}.", message);
    }

    [Theory]
    [InlineData(6, "Gói tuần cần số đêm chia hết cho 7.")]
    [InlineData(7, null)]
    public void Week_package_length(int nights, string? expected)
    {
        var start = new DateOnly(2026, 12, 7);
        Assert.Equal(expected, StayRules.PackageLength("WEEK", start, start.AddDays(nights)));
    }

    [Fact]
    public void Month_package_rejects_ten_nights()
    {
        var start = new DateOnly(2026, 12, 1);
        Assert.Equal("Gói tháng cần số đêm chia hết cho 30.", StayRules.PackageLength("MONTH", start, start.AddDays(10)));
    }

    [Fact]
    public void Loyalty_percent_stops_at_the_floor()
    {
        Assert.Equal(585000m, LoyaltyRules.Payable(650000m, 10m, 552500m));
        Assert.Equal(600000m, LoyaltyRules.Payable(650000m, 10m, 600000m));
    }

    [Fact]
    public void Inclusive_room_tax_matches_the_worksheet()
    {
        var tax = decimal.Round(TaxMath.Amount(14200000m, 8m, true), 0, MidpointRounding.AwayFromZero);
        Assert.Equal(1051852m, tax);
    }

    [Fact]
    public void Room_rule_does_not_tax_late_checkout_or_no_show()
    {
        Assert.True(TaxMath.Matches("ROOM", "ROOM"));
        Assert.False(TaxMath.Matches("ROOM", "LATE_CHECKOUT"));
        Assert.False(TaxMath.Matches("ROOM", "NO_SHOW"));
        Assert.True(TaxMath.Matches("ALL", "LATE_CHECKOUT"));
    }

    [Fact]
    public void Week_package_is_one_room_line()
    {
        var (lines, unit) = RoomCardPricing.Build(Standard(), null, "WEEK", new DateOnly(2026, 12, 7), new DateOnly(2026, 12, 14), 1, 0, null, null);
        Assert.Equal(4550000m, unit);
        var room = Assert.Single(lines, line => line.Category == "ROOM");
        Assert.Equal("1 tuần", room.Description);
        Assert.DoesNotContain(lines, line => line.Category is "NO_SHOW" or "LATE_CHECKOUT");
    }
}
