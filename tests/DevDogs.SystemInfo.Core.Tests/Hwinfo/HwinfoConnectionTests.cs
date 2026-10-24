using DevDogs.SystemInfo.Hwinfo;

namespace DevDogs.SystemInfo.Core.Tests.Hwinfo;

public class HwinfoConnectionTests
{
    private readonly FakeOpener _opener = new();
    private TimeSpan _now = TimeSpan.Zero;

    private HwinfoConnection Connect() => new(_opener, () => _now);

    [Fact]
    public void Connects_when_HWiNFO_publishes()
    {
        _opener.Next = new FakeSharedMemory(HeaderBytes.Build());
        using var connection = Connect();

        Assert.True(connection.Poll());
        Assert.True(connection.IsConnected);
        Assert.Equal(490u, connection.Header!.Value.ReadingCount);
    }

    [Fact]
    public void Missing_mapping_is_retried_only_after_the_interval()
    {
        using var connection = Connect();

        Assert.False(connection.Poll());
        Assert.False(connection.Poll());
        Assert.Equal(1, _opener.OpenAttempts);

        _now += HwinfoConnection.RetryInterval;
        _opener.Next = new FakeSharedMemory(HeaderBytes.Build());

        Assert.True(connection.Poll());
        Assert.Equal(2, _opener.OpenAttempts);
    }

    [Fact]
    public void Dead_signature_disconnects_and_releases_the_mapping()
    {
        var memory = new FakeSharedMemory(HeaderBytes.Build());
        _opener.Next = memory;
        using var connection = Connect();
        Assert.True(connection.Poll());

        memory.Bytes = HeaderBytes.Build(HwinfoHeader.DeadSignature);

        Assert.False(connection.Poll());
        Assert.False(connection.IsConnected);
        Assert.Null(connection.Header);
        Assert.True(memory.Disposed);
    }

    [Fact]
    public void Reconnects_to_a_new_mapping_after_HWiNFO_restarts()
    {
        var first = new FakeSharedMemory(HeaderBytes.Build());
        _opener.Next = first;
        using var connection = Connect();
        connection.Poll();
        first.Bytes = HeaderBytes.Build(HwinfoHeader.DeadSignature);
        connection.Poll();

        _opener.Next = new FakeSharedMemory(HeaderBytes.Build());
        Assert.False(connection.Poll());

        _now += HwinfoConnection.RetryInterval;
        Assert.True(connection.Poll());
    }

    [Fact]
    public void Truncated_block_counts_as_disconnected()
    {
        _opener.Next = new FakeSharedMemory(new byte[HwinfoHeader.MinSize - 4]);
        using var connection = Connect();

        Assert.False(connection.Poll());
    }
}
