using System.Text.RegularExpressions;

namespace LotusPms.UnitTests;

public class ColumnNameTests
{
    static string Column(string name) => Regex.Replace(name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();

    [Theory]
    [InlineData("WeeklyRate", "weekly_rate")]
    [InlineData("MonthlyRate", "monthly_rate")]
    [InlineData("ShiftCode", "shift_code")]
    [InlineData("MinNightlyRate", "min_nightly_rate")]
    [InlineData("IdempotencyKey", "idempotency_key")]
    public void Regex_matches_the_sql_scripts(string property, string column) =>
        Assert.Equal(column, Column(property));
}
