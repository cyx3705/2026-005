using DesktopIconUpgrader.Models;
using DesktopIconUpgrader.Utilities;

namespace DesktopIconUpgrader.Services;

public sealed class DesktopInventoryService(ShellLinkService shellLinks)
{
    public Task<IReadOnlyList<DesktopIconItem>> ScanAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        var items = new List<DesktopIconItem>();
        var userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        ScanDirectory(userDesktop, false, items, cancellationToken);
        if (!string.Equals(userDesktop, publicDesktop, StringComparison.OrdinalIgnoreCase))
            ScanDirectory(publicDesktop, true, items, cancellationToken);

        foreach (var definition in SystemIconCatalog.Definitions.Where(SystemIconCatalog.IsVisible))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var location = SystemIconCatalog.GetEffectiveIconLocation(definition.Clsid);
            ParseIconLocation(location, out var iconPath, out var iconIndex);
            items.Add(new DesktopIconItem
            {
                Id = StableId.For("system", definition.Clsid),
                Name = definition.Name,
                Kind = DesktopItemKind.SystemIcon,
                Path = definition.Clsid,
                SystemClsid = definition.Clsid,
                IconLocation = iconPath,
                IconIndex = iconIndex,
                IsSupported = true
            });
        }

        return (IReadOnlyList<DesktopIconItem>)items
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }, cancellationToken);

    private void ScanDirectory(string directory, bool isPublic, List<DesktopIconItem> items, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.Hidden) && Path.GetFileName(path).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                items.Add(CreateItem(path, isPublic));
            }
            catch (Exception exception)
            {
                items.Add(new DesktopIconItem
                {
                    Id = StableId.For("error", path),
                    Name = Path.GetFileNameWithoutExtension(path),
                    Kind = DesktopItemKind.UnsupportedFile,
                    Path = path,
                    IsSupported = false,
                    IsPublicDesktop = isPublic,
                    Status = $"读取失败：{exception.Message}"
                });
            }
        }
    }

    private DesktopIconItem CreateItem(string path, bool isPublic)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".lnk")
        {
            var data = shellLinks.Read(path);
            return new DesktopIconItem
            {
                Id = StableId.For("lnk", path),
                Name = Path.GetFileNameWithoutExtension(path),
                Kind = DesktopItemKind.Shortcut,
                Path = path,
                TargetPath = data.TargetPath,
                IconLocation = data.IconLocation,
                IconIndex = data.IconIndex,
                IsSupported = true,
                IsPublicDesktop = isPublic
            };
        }

        if (extension == ".url")
        {
            var values = ReadInternetShortcut(path);
            return new DesktopIconItem
            {
                Id = StableId.For("url", path),
                Name = Path.GetFileNameWithoutExtension(path),
                Kind = DesktopItemKind.InternetShortcut,
                Path = path,
                TargetPath = values.GetValueOrDefault("URL"),
                IconLocation = values.GetValueOrDefault("IconFile"),
                IconIndex = int.TryParse(values.GetValueOrDefault("IconIndex"), out var index) ? index : 0,
                IsSupported = true,
                IsPublicDesktop = isPublic
            };
        }

        if (Directory.Exists(path))
        {
            return new DesktopIconItem
            {
                Id = StableId.For("folder", path),
                Name = Path.GetFileName(path),
                Kind = DesktopItemKind.Folder,
                Path = path,
                TargetPath = path,
                IsSupported = true,
                IsPublicDesktop = isPublic
            };
        }

        return new DesktopIconItem
        {
            Id = StableId.For("file", path),
            Name = Path.GetFileName(path),
            Kind = DesktopItemKind.UnsupportedFile,
            Path = path,
            TargetPath = path,
            IsSupported = false,
            IsPublicDesktop = isPublic,
            Status = "Windows 不支持单文件独立图标"
        };
    }

    public static Dictionary<string, string> ReadInternetShortcut(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(path))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }
        return values;
    }

    public static void ParseIconLocation(string? location, out string? path, out int index)
    {
        path = null;
        index = 0;
        if (string.IsNullOrWhiteSpace(location)) return;
        var expanded = Environment.ExpandEnvironmentVariables(location.Trim());
        var separator = expanded.LastIndexOf(',');
        if (separator > 0 && int.TryParse(expanded[(separator + 1)..].Trim(), out var parsed))
        {
            index = parsed;
            expanded = expanded[..separator];
        }
        path = expanded.Trim().Trim('"');
    }
}
