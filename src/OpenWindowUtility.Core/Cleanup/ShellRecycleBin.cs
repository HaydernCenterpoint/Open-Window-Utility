using System.Runtime.InteropServices;

namespace OpenWindowUtility.Core.Cleanup;

public interface IRecycleBinQuery
{
    (long Bytes, int Count) Query();
    void Empty();
}

public sealed class ShellRecycleBin : IRecycleBinQuery
{
    private const uint NoConfirmation = 0x00000001;
    private const uint NoProgressUi = 0x00000002;
    private const uint NoSound = 0x00000004;

    public (long Bytes, int Count) Query()
    {
        var info = new SHQUERYRBINFO
        {
            cbSize = Marshal.SizeOf<SHQUERYRBINFO>()
        };
        var hr = SHQueryRecycleBin(null, ref info);
        if (hr is not 0 and not 1)
        {
            return (0, 0);
        }

        var count = info.i64NumItems > int.MaxValue ? int.MaxValue : (int)info.i64NumItems;
        return (info.i64Size, count);
    }

    public void Empty()
    {
        var hr = SHEmptyRecycleBin(0, null, NoConfirmation | NoProgressUi | NoSound);
        if (hr is not 0 and not 1)
        {
            throw new InvalidOperationException($"Recycle Bin empty failed (HRESULT 0x{hr:X8}).");
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(nint hwnd, string? pszRootPath, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }
}
