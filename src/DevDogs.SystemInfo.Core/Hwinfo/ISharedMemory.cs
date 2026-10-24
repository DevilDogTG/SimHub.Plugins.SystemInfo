using System;

namespace DevDogs.SystemInfo.Hwinfo;

/// <summary>Read-only view of a shared memory block. Lets tests replay recorded dumps instead of a live HWiNFO.</summary>
public interface ISharedMemory : IDisposable
{
    /// <summary>Copies <paramref name="count"/> bytes from <paramref name="offset"/>; returns how many were copied.</summary>
    int Read(long offset, byte[] buffer, int count);
}

/// <summary>Opens HWiNFO's shared memory, or returns <c>null</c> when it isn't published (HWiNFO closed or sharing off).</summary>
public interface ISharedMemoryOpener
{
    ISharedMemory? TryOpen();
}
