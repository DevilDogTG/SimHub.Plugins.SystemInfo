using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace DevDogs.SystemInfo.Hwinfo;

/// <summary>Opens the live <c>Global\HWiNFO_SENS_SM2</c> mapping read-only.</summary>
public sealed class MemoryMappedHwinfoOpener : ISharedMemoryOpener
{
    public const string MappingName = @"Global\HWiNFO_SENS_SM2";

    public ISharedMemory? TryOpen()
    {
        MemoryMappedFile? file = null;
        try
        {
            file = MemoryMappedFile.OpenExisting(MappingName, MemoryMappedFileRights.Read);
            // Size 0 maps the whole block; HWiNFO grows it when sensors appear, so a reopen picks up the new size.
            var view = file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            return new MappedView(file, view);
        }
        catch (Exception ex) when (ex is FileNotFoundException or IOException or UnauthorizedAccessException)
        {
            file?.Dispose();
            return null;
        }
    }

    private sealed class MappedView(MemoryMappedFile file, MemoryMappedViewAccessor view) : ISharedMemory
    {
        public int Read(long offset, byte[] buffer, int count)
        {
            var available = view.Capacity - offset;
            if (available <= 0) return 0;
            return view.ReadArray(offset, buffer, 0, (int)Math.Min(count, available));
        }

        public void Dispose()
        {
            view.Dispose();
            file.Dispose();
        }
    }
}
