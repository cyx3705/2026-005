using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using DesktopIconUpgrader.Models;
using DesktopIconUpgrader.Services;
using System.Diagnostics;

namespace DesktopIconUpgrader.SmokeTests;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        var keepArtifacts = args.Contains("--keep-artifacts", StringComparer.OrdinalIgnoreCase);
        var root = keepArtifacts
            ? Path.Combine(Environment.CurrentDirectory, "smoke-artifacts")
            : Path.Combine(Path.GetTempPath(), "DesktopIconUpgrader-SmokeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            TestClassificationAndIco(root, keepArtifacts);
            await TestInventory();
            await TestShortcutTransaction(root);
            Console.WriteLine("PASS: classification, ten-frame ICO, shortcut upgrade and baseline restore");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            if (!keepArtifacts)
                try { Directory.Delete(root, true); } catch { }
        }
    }

    private static async Task TestInventory()
    {
        var stopwatch = Stopwatch.StartNew();
        var items = await new DesktopInventoryService(new ShellLinkService()).ScanAsync(CancellationToken.None);
        Require(items.Count > 0, "桌面扫描没有返回任何项目");
        Console.WriteLine($"Inventory: {items.Count} items in {stopwatch.ElapsedMilliseconds} ms");
    }

    private static void TestClassificationAndIco(string root, bool keepArtifacts)
    {
        var renderer = new IconRenderer();
        var transparent = CreateImage(256, false);
        var colored = CreateImage(256, true);
        var transparentAnalysis = renderer.Analyze(transparent);
        var coloredAnalysis = renderer.Analyze(colored);
        Require(transparentAnalysis.Kind == BackgroundKind.Transparent, "透明底分类失败");
        Require(coloredAnalysis.Kind == BackgroundKind.Colored, "有色底分类失败");

        var master = renderer.RenderMaster(transparent, transparentAnalysis);
        var coloredMaster = renderer.RenderMaster(colored, coloredAnalysis);
        var icoPath = Path.Combine(root, "test.ico");
        new IcoWriter().Write(icoPath, master);
        IcoWriter.Validate(icoPath);
        Require(new FileInfo(icoPath).Length > 10_000, "ICO 文件异常小");
        if (keepArtifacts)
        {
            SavePng(master, Path.Combine(root, "transparent-result.png"));
            SavePng(coloredMaster, Path.Combine(root, "colored-result.png"));
            Console.WriteLine($"Artifacts: {root}");
        }
    }

    private static async Task TestShortcutTransaction(string root)
    {
        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        Require(File.Exists(explorer), "找不到 explorer.exe");
        var shortcutPath = Path.Combine(root, "transaction.lnk");
        var links = new ShellLinkService();
        links.Create(shortcutPath, explorer, explorer, 0);
        var item = new DesktopIconItem
        {
            Id = "SMOKE-TRANSACTION",
            Name = "transaction",
            Kind = DesktopItemKind.Shortcut,
            Path = shortcutPath,
            TargetPath = explorer,
            IconLocation = explorer,
            IconIndex = 0,
            IsSupported = true
        };
        var renderer = new IconRenderer();
        var backups = new BackupStore(links, Path.Combine(root, "data"));
        var applier = new DesktopIconApplier(links);
        var coordinator = new DesktopIconCoordinator(
            new IconSourceResolver(), renderer, new IcoWriter(), backups, applier, new ShellRefreshService());

        var upgraded = await coordinator.UpgradeAsync([item], null, CancellationToken.None);
        Require(upgraded.Single().Success, "快捷方式升级失败：" + upgraded.Single().Message);
        var generated = links.Read(shortcutPath).IconLocation;
        Require(generated.EndsWith(".ico", StringComparison.OrdinalIgnoreCase), "快捷方式没有指向生成 ICO");
        Require(File.Exists(generated), "生成 ICO 不存在");

        var restored = await coordinator.RestoreAsync([item], null, CancellationToken.None);
        Require(restored.Single().Success, "快捷方式还原失败：" + restored.Single().Message);
        var original = links.Read(shortcutPath);
        Require(string.Equals(original.IconLocation, explorer, StringComparison.OrdinalIgnoreCase) && original.IconIndex == 0,
            "还原后的 IconLocation 与基线不一致");
    }

    private static BitmapSource CreateImage(int size, bool coloredBackground)
    {
        var stride = size * 4;
        var pixels = new byte[stride * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var offset = y * stride + x * 4;
            var inside = Math.Pow(x - size / 2d, 2) + Math.Pow(y - size / 2d, 2) < Math.Pow(size * 0.31, 2);
            if (coloredBackground || inside)
            {
                pixels[offset] = coloredBackground ? (byte)54 : (byte)205;
                pixels[offset + 1] = coloredBackground ? (byte)112 : (byte)105;
                pixels[offset + 2] = coloredBackground ? (byte)225 : (byte)38;
                pixels[offset + 3] = 255;
            }
        }
        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void SavePng(BitmapSource source, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
