using Microsoft.Win32;

namespace DesktopIconUpgrader.Services;

public sealed record SystemIconDefinition(string Name, string Clsid, bool VisibleByDefault);

public static class SystemIconCatalog
{
    public static readonly IReadOnlyList<SystemIconDefinition> Definitions =
    [
        new("回收站", "{645FF040-5081-101B-9F08-00AA002F954E}", true),
        new("此电脑", "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", false),
        new("网络", "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", false),
        new("用户文件", "{59031A47-3F72-44A7-89C5-5595FE6B30EE}", false),
        new("控制面板", "{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}", false)
    ];

    public static bool IsVisible(SystemIconDefinition definition)
    {
        const string relative = @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
        using var key = Registry.CurrentUser.OpenSubKey(relative);
        var value = key?.GetValue(definition.Clsid);
        return value is int integer ? integer == 0 : definition.VisibleByDefault;
    }

    public static string? GetEffectiveIconLocation(string clsid)
    {
        using var userKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\CLSID\{clsid}\DefaultIcon");
        var userValue = userKey?.GetValue(null) as string;
        if (!string.IsNullOrWhiteSpace(userValue)) return userValue;
        using var classesKey = Registry.ClassesRoot.OpenSubKey($@"CLSID\{clsid}\DefaultIcon");
        return classesKey?.GetValue(null) as string;
    }
}
