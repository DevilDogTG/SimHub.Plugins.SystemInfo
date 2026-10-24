using DevDogs.SystemInfo.Hardware;

namespace DevDogs.SystemInfo.Core.Tests.Hardware;

public class StaticHardwareTests
{
    private const ulong GiB = 1UL << 30;

    // As Windows reports the owner's PC: RX 9070 XT 17,095,983,104 bytes in the registry, iGPU without a size,
    // 4 x 16 GB DDR4 (SMBIOS 26) at 3200 MT/s.
    private static readonly StaticHardware OwnerPc = new(
        [new DisplayAdapter("AMD Radeon RX 9070 XT", 17_095_983_104)],
        Enumerable.Repeat(new MemoryModule(16 * GiB, 26, 3200), 4).ToList());

    [Fact]
    public void Vram_total_comes_from_the_adapter_matching_the_gpu()
    {
        Assert.Equal(16_304, OwnerPc.VramTotalMb("AMD Radeon RX 9070 XT")!.Value, precision: 0);
    }

    [Fact]
    public void Vram_total_uses_the_largest_adapter_when_no_name_matches()
    {
        var hardware = new StaticHardware(
            [new DisplayAdapter("Intel(R) UHD Graphics 770", 128 * 1048576UL), new DisplayAdapter("NVIDIA GeForce RTX 4080", 16 * GiB)],
            []);

        Assert.Equal(16_384, hardware.VramTotalMb("GeForce RTX 4080 SUPER"));
        Assert.Equal(16_384, hardware.VramTotalMb(null));
    }

    [Fact]
    public void Vram_total_is_null_without_adapters()
    {
        Assert.Null(StaticHardware.None.VramTotalMb("AMD Radeon RX 9070 XT"));
    }

    [Fact]
    public void Ram_is_described_by_size_type_and_speed()
    {
        Assert.Equal(65_536, OwnerPc.RamInstalledMb);
        Assert.Equal("DDR4", OwnerPc.RamType);
        Assert.Equal("64 GB DDR4-3200", OwnerPc.RamDescription);
        Assert.Equal(3200, OwnerPc.RamRatedSpeedMts);
        Assert.Equal("DDR4-3200", OwnerPc.RamSpec);
    }

    [Fact]
    public void Rated_speed_is_the_slowest_module()
    {
        var hardware = new StaticHardware([], [new MemoryModule(8 * GiB, 26, 3600), new MemoryModule(8 * GiB, 26, 3200)]);

        Assert.Equal("DDR4-3200", hardware.RamSpec);
    }

    [Fact]
    public void Ddr5_modules_are_named()
    {
        var hardware = new StaticHardware([], [new MemoryModule(32 * GiB, 34, 6000), new MemoryModule(32 * GiB, 34, 6000)]);

        Assert.Equal("64 GB DDR5-6000", hardware.RamDescription);
    }

    [Fact]
    public void Unknown_type_keeps_only_the_size()
    {
        var hardware = new StaticHardware([], [new MemoryModule(8 * GiB, 0, 2400)]);

        Assert.Null(hardware.RamType);
        Assert.Null(hardware.RamSpec);
        Assert.Equal("8 GB", hardware.RamDescription);
    }

    [Fact]
    public void Mixed_module_types_report_no_single_type()
    {
        var hardware = new StaticHardware([], [new MemoryModule(8 * GiB, 26, 3200), new MemoryModule(8 * GiB, 24, 1600)]);

        Assert.Null(hardware.RamType);
    }

    [Fact]
    public void No_modules_means_no_ram_info()
    {
        Assert.Null(StaticHardware.None.RamInstalledMb);
        Assert.Null(StaticHardware.None.RamDescription);
    }
}
