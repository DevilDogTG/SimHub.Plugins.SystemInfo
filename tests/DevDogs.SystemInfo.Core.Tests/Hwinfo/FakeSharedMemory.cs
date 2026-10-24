using DevDogs.SystemInfo.Hwinfo;

namespace DevDogs.SystemInfo.Core.Tests.Hwinfo;

/// <summary>Shared memory backed by a byte array; <see cref="Bytes"/> can be swapped to simulate HWiNFO changing it.</summary>
internal sealed class FakeSharedMemory(byte[] bytes) : ISharedMemory
{
    public byte[] Bytes { get; set; } = bytes;
    public bool Disposed { get; private set; }

    public int Read(long offset, byte[] buffer, int count)
    {
        var copied = (int)Math.Max(0, Math.Min(count, Bytes.Length - offset));
        Array.Copy(Bytes, offset, buffer, 0, copied);
        return copied;
    }

    public void Dispose() => Disposed = true;
}

/// <summary>Hands out <see cref="Next"/> on each open; <c>null</c> means "HWiNFO not running".</summary>
internal sealed class FakeOpener : ISharedMemoryOpener
{
    public FakeSharedMemory? Next { get; set; }
    public int OpenAttempts { get; private set; }

    public ISharedMemory? TryOpen()
    {
        OpenAttempts++;
        return Next;
    }
}

internal static class HeaderBytes
{
    /// <summary>
    /// Header as HWiNFO v2 rev 1 wrote it on the owner's PC: 48 bytes (polling period 2000 ms at 44),
    /// 23 sensors of 392 bytes from 48, 490 readings of 460 bytes from 9064.
    /// </summary>
    public static byte[] Build(uint signature = HwinfoHeader.ActiveSignature) => Build(signature, extended: true);

    /// <summary>Classic 44-byte header with no polling period; the sensors start right after it.</summary>
    public static byte[] BuildClassic() => Build(HwinfoHeader.ActiveSignature, extended: false);

    private static byte[] Build(uint signature, bool extended)
    {
        var size = extended ? HwinfoHeader.MaxSize : HwinfoHeader.MinSize;
        var b = new byte[size];
        void Put(int at, uint v) => BitConverter.GetBytes(v).CopyTo(b, at);
        Put(0, signature);
        Put(4, 2);
        Put(8, 1);
        BitConverter.GetBytes(1_792_832_145L).CopyTo(b, 12);
        Put(20, (uint)size);
        Put(24, 392);
        Put(28, 23);
        Put(32, (uint)(size + 392 * 23));
        Put(36, 460);
        Put(40, 490);
        if (extended) Put(44, 2000);
        return b;
    }
}
