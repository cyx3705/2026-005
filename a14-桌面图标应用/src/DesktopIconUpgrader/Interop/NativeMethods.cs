using System.Runtime.InteropServices;

namespace DesktopIconUpgrader.Interop;

internal static class NativeMethods
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHDefExtractIconW(
        string pszIconFile,
        int iIndex,
        uint uFlags,
        out nint phiconLarge,
        out nint phiconSmall,
        uint nIconSize);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(nint hIcon);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(nint hObject);

    [DllImport("shell32.dll")]
    internal static extern void SHChangeNotify(uint wEventId, uint uFlags, nint dwItem1, nint dwItem2);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    internal static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        nint pbc,
        [In] ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    internal const uint SHCNE_ASSOCCHANGED = 0x08000000;
    internal const uint SHCNF_IDLIST = 0x0000;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeSize
{
    public int Width;
    public int Height;

    public NativeSize(int width, int height)
    {
        Width = width;
        Height = height;
    }
}

[Flags]
internal enum ShellItemImageFlags
{
    ResizeToFit = 0x00,
    BiggerSizeOk = 0x01,
    IconOnly = 0x04,
    ScaleUp = 0x100
}

[ComImport]
[Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemImageFactory
{
    void GetImage(NativeSize size, ShellItemImageFlags flags, out nint phbm);
}
