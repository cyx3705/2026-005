using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DesktopIconUpgrader.Interop;
using DesktopIconUpgrader.Models;

namespace DesktopIconUpgrader.Services;

public sealed class IconSourceResolver
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ico", ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff"
    };

    public BitmapSource Resolve(DesktopIconItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.IconLocation))
        {
            var iconPath = Environment.ExpandEnvironmentVariables(item.IconLocation);
            if (File.Exists(iconPath))
            {
                var direct = ResolvePath(iconPath, item.IconIndex);
                if (direct is not null) return direct;
            }
        }

        var targetPath = item.TargetPath;
        if (!string.IsNullOrWhiteSpace(targetPath) && (File.Exists(targetPath) || Directory.Exists(targetPath)))
        {
            var target = ResolvePath(targetPath, 0);
            if (target is not null) return target;
        }

        if (item.Kind == DesktopItemKind.SystemIcon && !string.IsNullOrWhiteSpace(item.SystemClsid))
        {
            var location = SystemIconCatalog.GetEffectiveIconLocation(item.SystemClsid);
            DesktopInventoryService.ParseIconLocation(location, out var path, out var index);
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                var system = ResolvePath(path, index);
                if (system is not null) return system;
            }
        }

        var fallback = ResolveShellItem(Environment.GetFolderPath(Environment.SpecialFolder.Windows), 256);
        return fallback ?? throw new InvalidOperationException("无法提取原始图标");
    }

    private BitmapSource? ResolvePath(string path, int index)
    {
        var extension = Path.GetExtension(path);
        if (ImageExtensions.Contains(extension))
        {
            try { return LoadBestFrame(path); }
            catch { }
        }

        if (File.Exists(path) && extension is ".exe" or ".dll" or ".cpl" or ".ocx" or ".scr")
        {
            var resource = ExtractResourceIcon(path, index, 256);
            if (resource is not null) return resource;
        }

        return ResolveShellItem(path, 256);
    }

    public static BitmapSource LoadBestFrame(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.OrderByDescending(value => value.PixelWidth * value.PixelHeight).First();
        var copy = BitmapFrame.Create(frame);
        copy.Freeze();
        return copy;
    }

    private static BitmapSource? ExtractResourceIcon(string path, int index, int size)
    {
        var packedSize = (uint)(size | (48 << 16));
        var result = NativeMethods.SHDefExtractIconW(path, index, 0, out var large, out var small, packedSize);
        var handle = large != 0 ? large : small;
        if (result != 0 || handle == 0) return null;
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            if (large != 0) NativeMethods.DestroyIcon(large);
            if (small != 0 && small != large) NativeMethods.DestroyIcon(small);
        }
    }

    private static BitmapSource? ResolveShellItem(string path, int size)
    {
        IShellItemImageFactory? factory = null;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            NativeMethods.SHCreateItemFromParsingName(path, 0, ref iid, out factory);
            factory.GetImage(new NativeSize(size, size), ShellItemImageFlags.IconOnly | ShellItemImageFlags.BiggerSizeOk | ShellItemImageFlags.ScaleUp, out var bitmap);
            if (bitmap == 0) return null;
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                NativeMethods.DeleteObject(bitmap);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (factory is not null && Marshal.IsComObject(factory)) Marshal.FinalReleaseComObject(factory);
        }
    }
}
