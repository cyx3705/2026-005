using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using DesktopIconUpgrader.Models;
using DesktopIconUpgrader.Services;

namespace DesktopIconUpgrader;

public partial class MainWindow : Window
{
    private readonly ShellLinkService _shellLinks = new();
    private readonly BackupStore _backups;
    private readonly DesktopInventoryService _inventory;
    private readonly DesktopIconCoordinator _coordinator;
    private CancellationTokenSource? _operationCancellation;

    public ObservableCollection<DesktopIconItem> Items { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _backups = new BackupStore(_shellLinks);
        _inventory = new DesktopInventoryService(_shellLinks);
        var renderer = new IconRenderer();
        _coordinator = new DesktopIconCoordinator(
            new IconSourceResolver(),
            renderer,
            new IcoWriter(),
            _backups,
            new DesktopIconApplier(_shellLinks),
            new ShellRefreshService());
        Loaded += async (_, _) => await RefreshItemsAsync();
    }

    private async Task RefreshItemsAsync()
    {
        if (_operationCancellation is not null) return;
        SetBusy(true, "正在扫描桌面项目…");
        using var cancellation = new CancellationTokenSource();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var scanned = await _inventory.ScanAsync(cancellation.Token);
            Items.Clear();
            foreach (var item in scanned)
            {
                if (_backups.HasBaseline(item.Id) && item.IsSupported) item.Status = "可还原";
                Items.Add(item);
            }
            SummaryText.Text = BuildSummary();
            StatusText.Text = $"扫描完成，共 {Items.Count} 项，用时 {stopwatch.Elapsed.TotalSeconds:F1} 秒；正在加载预览…";
            await _coordinator.PreparePreviewsAsync(Items.ToList(), cancellation.Token);
            StatusText.Text = "预览加载完成";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "扫描失败", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "扫描失败";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private string BuildSummary()
    {
        var supported = Items.Count(item => item.IsSupported);
        var restorable = Items.Count(item => _backups.HasBaseline(item.Id));
        return $"发现 {Items.Count} 项 · 可升级 {supported} 项 · 可还原 {restorable} 项 · 不支持 {Items.Count - supported} 项";
    }

    private async Task RunUpgradeAsync(IReadOnlyList<DesktopIconItem> targets)
    {
        if (targets.Count == 0)
        {
            MessageBox.Show(this, "没有选中可升级项目。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _operationCancellation = new CancellationTokenSource();
        SetBusy(true, "准备升级…");
        var progress = CreateProgress();
        try
        {
            var results = await _coordinator.UpgradeAsync(targets, progress, _operationCancellation.Token);
            ShowResults("升级完成", results);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "操作已取消，已完成项目保持有效";
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            Progress.Value = 0;
            SummaryText.Text = BuildSummary();
            SetBusy(false);
        }
    }

    private async Task RunRestoreAsync(IReadOnlyList<DesktopIconItem> targets)
    {
        if (targets.Count == 0)
        {
            MessageBox.Show(this, "没有可还原项目。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _operationCancellation = new CancellationTokenSource();
        SetBusy(true, "准备还原…");
        try
        {
            var results = await _coordinator.RestoreAsync(targets, CreateProgress(), _operationCancellation.Token);
            ShowResults("还原完成", results);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "还原已取消";
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            Progress.Value = 0;
            SummaryText.Text = BuildSummary();
            SetBusy(false);
        }
    }

    private Progress<(int Current, int Total, string Name)> CreateProgress() => new(value =>
    {
        Progress.Value = value.Total == 0 ? 0 : value.Current * 100d / value.Total;
        StatusText.Text = $"{value.Current}/{value.Total}  {value.Name}";
    });

    private void ShowResults(string title, IReadOnlyList<OperationResult> results)
    {
        var success = results.Count(result => result.Success);
        var failures = results.Where(result => !result.Success).ToList();
        var message = $"成功 {success} 项，失败或跳过 {failures.Count} 项。";
        if (failures.Count > 0)
            message += "\n\n" + string.Join("\n", failures.Take(8).Select(result => $"{result.Item.Name}：{result.Message}"));
        MessageBox.Show(this, message, title, MessageBoxButton.OK,
            failures.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        StatusText.Text = message.Split('\n')[0];
    }

    private void SetBusy(bool busy, string? status = null)
    {
        CancelButton.IsEnabled = busy && _operationCancellation is not null;
        ItemsGrid.IsEnabled = !busy;
        if (status is not null) StatusText.Text = status;
    }

    private List<DesktopIconItem> CurrentSupported(bool selectedOnly) => Items
        .Where(item => item.IsSupported && (!selectedOnly || item.IsSelected))
        .ToList();

    private List<DesktopIconItem> AllRestoreItems()
    {
        var current = Items.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        return _backups.Catalog.Items.Values.Select(record => current.GetValueOrDefault(record.Id) ?? new DesktopIconItem
        {
            Id = record.Id,
            Name = record.Name,
            Kind = record.Kind,
            Path = record.Path,
            SystemClsid = record.SystemClsid,
            IsSupported = true
        }).ToList();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshItemsAsync();
    private async void UpgradeAll_Click(object sender, RoutedEventArgs e) => await RunUpgradeAsync(CurrentSupported(false));
    private async void UpgradeSelected_Click(object sender, RoutedEventArgs e) => await RunUpgradeAsync(CurrentSupported(true));
    private async void RestoreSelected_Click(object sender, RoutedEventArgs e) => await RunRestoreAsync(CurrentSupported(true).Where(item => _backups.HasBaseline(item.Id)).ToList());

    private async void RestoreAll_Click(object sender, RoutedEventArgs e)
    {
        var targets = AllRestoreItems();
        if (targets.Count == 0)
        {
            MessageBox.Show(this, "还没有原始基线备份。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show(this, $"将还原 {targets.Count} 个项目到首次修改前状态，是否继续？", "确认一键还原",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            await RunRestoreAsync(targets);
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items.Where(item => item.IsSupported)) item.IsSelected = true;
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items) item.IsSelected = false;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _operationCancellation?.Cancel();
}
