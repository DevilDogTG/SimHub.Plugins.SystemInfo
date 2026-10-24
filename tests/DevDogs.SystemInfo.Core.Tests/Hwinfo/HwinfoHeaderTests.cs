using DevDogs.SystemInfo.Hwinfo;

namespace DevDogs.SystemInfo.Core.Tests.Hwinfo;

public class HwinfoHeaderTests
{
    private static HwinfoHeader Parse(byte[] bytes) => HwinfoHeader.Parse(bytes, bytes.Length);

    [Fact]
    public void Parse_reads_every_field_in_layout_order()
    {
        var header = Parse(HeaderBytes.Build());

        Assert.True(header.IsActive);
        Assert.Equal(2u, header.Version);
        Assert.Equal(1u, header.Revision);
        Assert.Equal(1_792_832_145L, header.PollTime);
        Assert.Equal(48u, header.SensorSectionOffset);
        Assert.Equal(392u, header.SensorElementSize);
        Assert.Equal(23u, header.SensorCount);
        Assert.Equal(9064u, header.ReadingSectionOffset);
        Assert.Equal(460u, header.ReadingElementSize);
        Assert.Equal(490u, header.ReadingCount);
        Assert.Equal(2000u, header.PollingPeriodMs);
    }

    [Fact]
    public void TotalSize_matches_the_live_mapping()
    {
        Assert.Equal(234_464, Parse(HeaderBytes.Build()).TotalSize);
    }

    [Fact]
    public void Classic_header_has_no_polling_period()
    {
        var header = Parse(HeaderBytes.BuildClassic());

        Assert.Equal(44u, header.SensorSectionOffset);
        Assert.Null(header.PollingPeriodMs);
    }

    [Fact]
    public void Classic_header_ignores_sensor_bytes_at_offset_44()
    {
        var bytes = HeaderBytes.BuildClassic().Concat(BitConverter.GetBytes(0xF0000301u)).ToArray();

        Assert.Null(Parse(bytes).PollingPeriodMs);
    }

    [Fact]
    public void Dead_signature_is_not_active()
    {
        Assert.False(Parse(HeaderBytes.Build(HwinfoHeader.DeadSignature)).IsActive);
    }

    [Fact]
    public void Parse_rejects_a_short_buffer()
    {
        Assert.Throws<ArgumentException>(() => HwinfoHeader.Parse(new byte[HwinfoHeader.MaxSize], HwinfoHeader.MinSize - 1));
    }
}
