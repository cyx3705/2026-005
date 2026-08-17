using System.Windows.Media.Imaging;
using DesktopIconUpgrader.Models;
using DesktopIconUpgrader.Utilities;

namespace DesktopIconUpgrader.Services;

public sealed class DesktopIconCoordinator(
    IconSourceResolver sourceResolver,
    IconRenderer renderer,
    IcoWriter icoWriter,
    BackupStore backupStore,
    DesktopIconApplier applier,
    ShellRefreshService shellRefresh)
{
    public Task<IReadOnlyList<OperationResult>> UpgradeAsync(
        IReadOnlyList<DesktopIconItem> items,
        IProgress<(int Current, int Total, string Name)>? progress,
        CancellationToken cancellationToken) => StaTask.Run<IReadOnlyList<OperationResult>>(() =>
    {
        var results = new List<OperationResult>();
        for (var index = 0; index < items.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[index];
            progress?.Report((index + 1, items.Count, item.Name));
            if (!item.IsSupported)
            {
                results.Add(new OperationResult(item, false, "此项目不支持独立替换"));
                continue;
            }
            results.Add(UpgradeOne(item));
        }
        shellRefresh.Refresh();
        return results;
    }, cancellationToken);

    public Task<IReadOnlyList<OperationResult>> RestoreAsync(
        IReadOnlyList<DesktopIconItem> items,
        IProgress<(int Current, int Total, string Name)>? progress,
        CancellationToken cancellationToken) => StaTask.Run<IReadOnlyList<OperationResult>>(() =>
    {
        var results = new List<OperationResult>();
        for (var index = 0; index < items.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[index];
            progress?.Report((index + 1, items.Count, item.Name));
            var record = backupStore.Find(item.Id);
            if (record is null)
            {
                results.Add(new OperationResult(item, false, "没有原始基线备份"));
                continue;
            }
            try
            {
                applier.Restore(record);
                item.Status = "已还原";
                results.Add(new OperationResult(item, true, "已恢复首次修改前状态"));
            }
            catch (Exception exception)
            {
                item.Status = "还原失败";
                results.Add(new OperationResult(item, false, exception.Message));
            }
        }
        shellRefresh.Refresh();
        return results;
    }, cancellationToken);

    public Task PreparePreviewsAsync(IReadOnlyList<DesktopIconItem> items, CancellationToken cancellationToken) => StaTask.Run(() =>
    {
        foreach (var item in items.Where(value => value.IsSupported))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var source = sourceResolver.Resolve(item);
                item.OriginalPreview = IconRenderer.Scale(source, 48, 48);
            }
            catch
            {
                item.Status = "无法读取原图";
            }
        }
    }, cancellationToken);

    private OperationResult UpgradeOne(DesktopIconItem item)
    {
        BackupRecord? baseline = null;
        try
        {
            baseline = backupStore.EnsureBaseline(item);
            var source = sourceResolver.Resolve(item);
            var analysis = renderer.Analyze(source);
            var master = renderer.RenderMaster(source, analysis);
            var iconPath = Path.Combine(backupStore.IconDirectory, $"{item.Id}.ico");
            icoWriter.Write(iconPath, master);
            applier.Apply(item, iconPath);
            if (!applier.Verify(item, iconPath)) throw new InvalidOperationException("写入后验证失败");
            backupStore.MarkGenerated(item.Id, iconPath);
            item.BackgroundKind = analysis.Kind;
            item.GeneratedPreview = IconRenderer.Scale(master, 48, 48);
            item.Status = "已升级";
            return new OperationResult(item, true, $"{item.BackgroundLabel}，主色 {analysis.HexColor}", iconPath);
        }
        catch (Exception exception)
        {
            if (baseline is not null)
            {
                try { applier.Restore(baseline); } catch { }
            }
            item.Status = "升级失败";
            return new OperationResult(item, false, exception.Message);
        }
    }
}
