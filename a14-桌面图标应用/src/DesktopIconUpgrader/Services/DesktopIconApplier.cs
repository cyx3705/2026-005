using System.Text;
using DesktopIconUpgrader.Models;
using Microsoft.Win32;

namespace DesktopIconUpgrader.Services;

public sealed class DesktopIconApplier(ShellLinkService shellLinks)
{
    public void Apply(DesktopIconItem item, string iconPath)
    {
        switch (item.Kind)
        {
            case DesktopItemKind.Shortcut:
                shellLinks.SetIcon(item.Path, iconPath, 0);
                break;
            case DesktopItemKind.InternetShortcut:
                SetUrlIcon(item.Path, iconPath);
                break;
            case DesktopItemKind.Folder:
                SetFolderIcon(item.Path, iconPath);
                break;
            case DesktopItemKind.SystemIcon:
                SetSystemIcon(item.SystemClsid!, iconPath);
                break;
            default:
                throw new NotSupportedException("此项目不支持独立替换图标");
        }
    }

    public void Restore(BackupRecord record)
    {
        switch (record.Kind)
        {
            case DesktopItemKind.Shortcut:
                if (!File.Exists(record.Path)) throw new FileNotFoundException("快捷方式已不存在", record.Path);
                shellLinks.SetIcon(record.Path, record.OriginalIconLocation ?? string.Empty, record.OriginalIconIndex);
                break;
            case DesktopItemKind.InternetShortcut:
                if (record.OriginalFileBase64 is null) throw new InvalidDataException("URL 原始备份缺失");
                File.WriteAllBytes(record.Path, Convert.FromBase64String(record.OriginalFileBase64));
                File.SetAttributes(record.Path, (FileAttributes)record.OriginalAttributes);
                break;
            case DesktopItemKind.Folder:
                RestoreFolder(record);
                break;
            case DesktopItemKind.SystemIcon:
                RestoreRegistry(record);
                break;
        }
    }

    public bool Verify(DesktopIconItem item, string iconPath)
    {
        var normalized = Path.GetFullPath(iconPath);
        return item.Kind switch
        {
            DesktopItemKind.Shortcut => string.Equals(Path.GetFullPath(shellLinks.Read(item.Path).IconLocation), normalized, StringComparison.OrdinalIgnoreCase),
            DesktopItemKind.InternetShortcut => string.Equals(
                Path.GetFullPath(DesktopInventoryService.ReadInternetShortcut(item.Path).GetValueOrDefault("IconFile") ?? string.Empty),
                normalized, StringComparison.OrdinalIgnoreCase),
            DesktopItemKind.Folder => File.ReadAllText(Path.Combine(item.Path, "desktop.ini")).Contains(iconPath, StringComparison.OrdinalIgnoreCase),
            DesktopItemKind.SystemIcon => VerifySystemIcon(item.SystemClsid!, iconPath),
            _ => false
        };
    }

    private static void SetUrlIcon(string path, string iconPath)
    {
        var lines = File.ReadAllLines(path).ToList();
        SetIniValue(lines, "IconFile", iconPath);
        SetIniValue(lines, "IconIndex", "0");
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }

    private static void SetIniValue(List<string> lines, string key, string value)
    {
        var index = lines.FindIndex(line => line.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase));
        if (index >= 0) lines[index] = $"{key}={value}";
        else
        {
            if (!lines.Any(line => line.Equals("[InternetShortcut]", StringComparison.OrdinalIgnoreCase))) lines.Insert(0, "[InternetShortcut]");
            lines.Add($"{key}={value}");
        }
    }

    private static void SetFolderIcon(string folderPath, string iconPath)
    {
        var iniPath = Path.Combine(folderPath, "desktop.ini");
        if (File.Exists(iniPath)) File.SetAttributes(iniPath, FileAttributes.Normal);
        var content = $"[.ShellClassInfo]\r\nIconResource={iconPath},0\r\n";
        File.WriteAllText(iniPath, content, Encoding.Unicode);
        File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);
        File.SetAttributes(folderPath, File.GetAttributes(folderPath) | FileAttributes.ReadOnly);
    }

    private static void RestoreFolder(BackupRecord record)
    {
        if (!Directory.Exists(record.Path)) throw new DirectoryNotFoundException(record.Path);
        var iniPath = Path.Combine(record.Path, "desktop.ini");
        if (File.Exists(iniPath)) File.SetAttributes(iniPath, FileAttributes.Normal);
        if (record.AuxiliaryFileExisted)
        {
            if (record.AuxiliaryFileBase64 is null) throw new InvalidDataException("desktop.ini 备份缺失");
            File.WriteAllBytes(iniPath, Convert.FromBase64String(record.AuxiliaryFileBase64));
            File.SetAttributes(iniPath, (FileAttributes)record.AuxiliaryAttributes);
        }
        else if (File.Exists(iniPath))
        {
            File.Delete(iniPath);
        }
        File.SetAttributes(record.Path, (FileAttributes)record.OriginalAttributes);
    }

    private static void SetSystemIcon(string clsid, string iconPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\CLSID\{clsid}\DefaultIcon", true);
        var value = $"{iconPath},0";
        key.SetValue(null, value, RegistryValueKind.String);
        if (clsid.Equals("{645FF040-5081-101B-9F08-00AA002F954E}", StringComparison.OrdinalIgnoreCase))
        {
            key.SetValue("empty", value, RegistryValueKind.String);
            key.SetValue("full", value, RegistryValueKind.String);
        }
    }

    private static bool VerifySystemIcon(string clsid, string iconPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\CLSID\{clsid}\DefaultIcon");
        var value = key?.GetValue(null) as string;
        return value?.StartsWith(iconPath, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static void RestoreRegistry(BackupRecord record)
    {
        var relative = $@"Software\Classes\CLSID\{record.SystemClsid}\DefaultIcon";
        if (record.RegistryValues.GetValueOrDefault("__keyExists") != "true")
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(relative, false); } catch { }
            return;
        }
        using var key = Registry.CurrentUser.CreateSubKey(relative, true);
        foreach (var name in new[] { "", "empty", "full" })
        {
            var value = record.RegistryValues.GetValueOrDefault(name);
            if (value is null) key.DeleteValue(name, false);
            else key.SetValue(name, value, RegistryValueKind.String);
        }
    }
}
