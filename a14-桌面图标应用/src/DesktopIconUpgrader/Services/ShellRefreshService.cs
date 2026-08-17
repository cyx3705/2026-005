using DesktopIconUpgrader.Interop;

namespace DesktopIconUpgrader.Services;

public sealed class ShellRefreshService
{
    public void Refresh()
    {
        NativeMethods.SHChangeNotify(NativeMethods.SHCNE_ASSOCCHANGED, NativeMethods.SHCNF_IDLIST, 0, 0);
    }
}
