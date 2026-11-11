namespace HotelPms.Api;

public class AiAssistantTests
{
    [Fact]
    public void Chunker_keeps_a_short_procedure_together()
    {
        var text = "# Nhận phòng\n1. Mở sơ đồ\n2. Chọn phòng đã kiểm\n3. Xác nhận";
        var chunks = KnowledgeChunker.Split(text, 800, 80);
        Assert.Single(chunks);
        Assert.Equal("Nhận phòng", chunks[0].Heading);
        Assert.Contains("3. Xác nhận", chunks[0].Content);
    }

    [Fact]
    public void Guide_explains_check_in_from_the_real_screen()
    {
        var text = SoftwareGuide.For("Làm thế nào để nhận phòng?", "CHECK_IN");
        Assert.Contains("Check-in", text);
        Assert.Contains("INSPECTED", text);
        Assert.False(SoftwareGuide.WantsLiveData("Làm thế nào để nhận phòng?"));
        Assert.True(SoftwareGuide.WantsLiveData("Phòng 201 đang trạng thái gì?"));
        var parts = SoftwareGuide.Parts("Làm thế nào để nhận phòng? Báo cáo tháng này bao nhiêu?");
        Assert.Equal(2, parts.Count);
        Assert.False(SoftwareGuide.WantsLiveData(parts[0]));
        Assert.True(SoftwareGuide.WantsMoney(parts[1]));
        Assert.NotNull(SoftwareGuide.For(parts[0], "CHECK_IN"));
        var periods = SoftwareGuide.Periods("Báo cáo tháng này bao nhiêu, năm nay thì sao?", new DateOnly(2026, 11, 11));
        Assert.Equal(2, periods.Count);
        Assert.Equal(new DateOnly(2026, 11, 1), periods[0].From);
        Assert.Equal(new DateOnly(2026, 1, 1), periods[1].From);
    }

    [Fact]
    public void Chunker_drops_duplicate_text()
    {
        var chunks = KnowledgeChunker.Split("# A\nCùng một đoạn\n# B\nCùng một đoạn", 800, 0);
        Assert.Single(chunks);
    }

}
