using System.Runtime.InteropServices;
using System.Text;
using DesktopIconUpgrader.Interop;
using DesktopIconUpgrader.Models;

namespace DesktopIconUpgrader.Services;

public sealed class ShellLinkService
{
    public ShortcutData Read(string path)
    {
        var shellLink = (IShellLinkW)new ShellLinkComObject();
        try
        {
            ((IPersistFile)shellLink).Load(path, 0);
            var target = new StringBuilder(32768);
            var arguments = new StringBuilder(32768);
            var workingDirectory = new StringBuilder(32768);
            var description = new StringBuilder(1024);
            var icon = new StringBuilder(32768);
            shellLink.GetPath(target, target.Capacity, 0, 0);
            shellLink.GetArguments(arguments, arguments.Capacity);
            shellLink.GetWorkingDirectory(workingDirectory, workingDirectory.Capacity);
            shellLink.GetDescription(description, description.Capacity);
            shellLink.GetIconLocation(icon, icon.Capacity, out var iconIndex);
            return new ShortcutData(target.ToString(), arguments.ToString(), workingDirectory.ToString(), description.ToString(), icon.ToString(), iconIndex);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellLink);
        }
    }

    public void SetIcon(string path, string iconPath, int iconIndex = 0)
    {
        var shellLink = (IShellLinkW)new ShellLinkComObject();
        try
        {
            var persist = (IPersistFile)shellLink;
            persist.Load(path, 0);
            shellLink.SetIconLocation(iconPath, iconIndex);
            persist.Save(path, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellLink);
        }
    }

    public void Create(string path, string targetPath, string iconPath, int iconIndex = 0)
    {
        var shellLink = (IShellLinkW)new ShellLinkComObject();
        try
        {
            shellLink.SetPath(targetPath);
            shellLink.SetWorkingDirectory(Directory.Exists(targetPath) ? targetPath : Path.GetDirectoryName(targetPath) ?? string.Empty);
            shellLink.SetIconLocation(iconPath, iconIndex);
            ((IPersistFile)shellLink).Save(path, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellLink);
        }
    }
}
