using System;
using HotelPms.Api;

Console.WriteLine("=== LOTUS PMS V4 - 13 CA KIỂM TRA CÔNG THỨC GIÁ VÀ PHỤ THU C# ===");

var stdIn = new TimeOnly(14, 0);
var stdOut = new TimeOnly(12, 0);
decimal baseOvernight = 650000m;
decimal extraHourRate = 100000m;

int passed = 0;
int total = 13;

void AssertEqual<T>(string testId, string desc, T actual, T expected)
{
    if (!Equals(actual, expected))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {testId}: {desc} | Expected: {expected}, Actual: {actual}");
        Console.ResetColor();
        Environment.Exit(1);
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[PASS] {testId}: {desc} -> {actual}");
        Console.ResetColor();
        passed++;
    }
}

// TC-01: Overnight 1 night standard, no early, no late
var r1 = PricingV4.CalculateOvernight(stdIn, stdOut, baseOvernight, extraHourRate,
    new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 6), null, null);
AssertEqual("TC-01", "Qua đêm tiêu chuẩn 1 đêm", r1.total, 650000m);

// TC-02: Overnight 3 nights standard
var r2 = PricingV4.CalculateOvernight(stdIn, stdOut, baseOvernight, extraHourRate,
    new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 8), null, null);
AssertEqual("TC-02", "Qua đêm tiêu chuẩn 3 đêm", r2.total, 1950000m);

// TC-03: Overnight 1 night, early check-in at 12:45 (1h15m early -> ceil 2h)
var r3 = PricingV4.CalculateOvernight(stdIn, stdOut, baseOvernight, extraHourRate,
    new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 6),
    new DateTime(2026, 11, 5, 12, 45, 0), null);
AssertEqual("TC-03", "Nhận sớm 1h15m làm tròn 2h", r3.earlyFee, 200000m);

// TC-04: Overnight 2 nights, late check-out at 12:50 (50m late -> ceil 1h)
var r4 = PricingV4.CalculateOvernight(stdIn, stdOut, baseOvernight, extraHourRate,
    new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 7),
    null, new DateTime(2026, 11, 7, 12, 50, 0));
AssertEqual("TC-04", "Trả muộn 50m làm tròn 1h", r4.lateFee, 100000m);

// TC-05: Overnight 1 night, both early (2h) and late (3h10m -> ceil 4h)
var r5 = PricingV4.CalculateOvernight(stdIn, stdOut, baseOvernight, extraHourRate,
    new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 6),
    new DateTime(2026, 11, 5, 12, 0, 0), new DateTime(2026, 11, 6, 15, 10, 0));
AssertEqual("TC-05", "Vừa nhận sớm 2h vừa trả muộn 4h", r5.total, 650000m + 200000m + 400000m);

// TC-06: Overnight check-in exact at standard 14:00
var r6 = PricingV4.CalculateOvernight(stdIn, stdOut, baseOvernight, extraHourRate,
    new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 6),
    new DateTime(2026, 11, 5, 14, 0, 0), null);
AssertEqual("TC-06", "Nhận phòng đúng giờ chuẩn 14:00 không tính sớm", r6.earlyHours, 0);

// TC-07: Overnight check-out exact at standard 12:00
var r7 = PricingV4.CalculateOvernight(stdIn, stdOut, baseOvernight, extraHourRate,
    new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 6),
    null, new DateTime(2026, 11, 6, 12, 0, 0));
AssertEqual("TC-07", "Trả phòng đúng giờ chuẩn 12:00 không tính muộn", r7.lateHours, 0);

// TC-08: Hourly package 2h included, duration 1h45m (within package)
var r8 = PricingV4.CalculateHourlyDayUse(250000m, 2, 75000m,
    new DateTime(2026, 11, 5, 9, 0, 0), new DateTime(2026, 11, 5, 10, 45, 0));
AssertEqual("TC-08", "Nghỉ giờ trong gói 2h", r8.total, 250000m);

// TC-09: Hourly package 2h included, duration 3h15m (1h15m overtime -> ceil 2h)
var r9 = PricingV4.CalculateHourlyDayUse(250000m, 2, 75000m,
    new DateTime(2026, 11, 5, 9, 0, 0), new DateTime(2026, 11, 5, 12, 15, 0));
AssertEqual("TC-09", "Nghỉ giờ vượt gói 1h15m làm tròn 2h phụ thu", r9.total, 250000m + 150000m);

// TC-10: Day-use package 8h included, duration 8h exactly
var r10 = PricingV4.CalculateHourlyDayUse(450000m, 8, 80000m,
    new DateTime(2026, 11, 5, 8, 0, 0), new DateTime(2026, 11, 5, 16, 0, 0));
AssertEqual("TC-10", "Nghỉ ngày trọn gói 8h", r10.total, 450000m);

// TC-11: Day-use package 8h included, duration 9h05m (1h05m overtime -> ceil 2h)
var r11 = PricingV4.CalculateHourlyDayUse(450000m, 8, 80000m,
    new DateTime(2026, 11, 5, 8, 0, 0), new DateTime(2026, 11, 5, 17, 5, 0));
AssertEqual("TC-11", "Nghỉ ngày vượt gói 1h05m làm tròn 2h phụ thu", r11.total, 450000m + 160000m);

// TC-12: Overnight checkOutDate <= checkInDate throws ArgumentException
bool tc12Caught = false;
try
{
    PricingV4.CalculateOvernight(stdIn, stdOut, baseOvernight, extraHourRate,
        new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 5), null, null);
}
catch (ArgumentException)
{
    tc12Caught = true;
}
AssertEqual("TC-12", "Từ chối ngày trả trước hoặc bằng ngày nhận", tc12Caught, true);

// TC-13: Early or late exceeding 24 hours throws ArgumentOutOfRangeException
bool tc13Caught = false;
try
{
    PricingV4.CalculateOvernight(stdIn, stdOut, baseOvernight, extraHourRate,
        new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 6),
        new DateTime(2026, 11, 3, 10, 0, 0), null); // 52 hours early
}
catch (ArgumentOutOfRangeException)
{
    tc13Caught = true;
}
AssertEqual("TC-13", "Từ chối nhận sớm vượt quá 24h", tc13Caught, true);

Console.WriteLine($"\nKẾT QUẢ: {passed}/{total} ca kiểm tra công thức PASS.");
if (passed == total)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("PASS: Bộ kiểm tra công thức giá và phụ thu V4 hoàn tất.");
    Console.ResetColor();
    Environment.Exit(0);
}
else
{
    Environment.Exit(1);
}
