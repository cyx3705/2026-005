using System.Text.Json;
using DesktopIconUpgrader.Models;
using Microsoft.Win32;

namespace DesktopIconUpgrader.Services;

public sealed class BackupStore
{
    private readonly ShellLinkService _shellLinks;
    private readonly object _sync = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private BackupCatalog? _catalog;

    public string DataRoot { get; }

    public BackupStore(ShellLinkService shellLinks, string? dataRoot = null)
    {
        _shellLinks = shellLinks;
        DataRoot = dataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OneHistory", "DesktopIconUpgrader");
    }

    public string IconDirectory => Path.Combine(DataRoot, "Icons");
    private string BackupDirectory => Path.Combine(DataRoot, "Backups");
    private string CatalogPath => Path.Combine(BackupDirectory, "baseline.json");

    public BackupCatalog Catalog
    {
        get
        {
            lock (_sync) return _catalog ??= Load();
        }
    }

    public BackupRecord EnsureBaseline(DesktopIconItem item)
    {
        lock (_sync)
        {
            var catalog = Catalog;
            if (catalog.Items.TryGetValue(item.Id, out var existing)) return existing;
            var record = Capture(item);
            catalog.Items[item.Id] = record;
            Save(catalog);
            return record;
        }
    }

    public void MarkGenerated(string id, string generatedPath)
    {
        lock (_sync)
        {
            if (!Catalog.Items.TryGetValue(id, out var record)) return;
            record.GeneratedIconPath = generatedPath;
            Save(Catalog);
        }
    }

    public bool HasBaseline(string id)
    {
        lock (_sync) return Catalog.Items.ContainsKey(id);
    }

    public BackupRecord? Find(string id)
    {
        lock (_sync) return Catalog.Items.GetValueOrDefault(id);
    }

    private BackupRecord Capture(DesktopIconItem item)
    {
        var record = new BackupRecord
        {
            Id = item.Id,
            Name = item.Name,
            Kind = item.Kind,
            Path = item.Path,
            SystemClsid = item.SystemClsid
        };

        switch (item.Kind)
        {
            case DesktopItemKind.Shortcut:
                var data = _shellLinks.Read(item.Path);
                record.OriginalIconLocation = data.IconLocation;
                record.OriginalIconIndex = data.IconIndex;
                record.OriginalFileBase64 = Convert.ToBase64String(File.ReadAllBytes(item.Path));
                break;
            case DesktopItemKind.InternetShortcut:
                record.OriginalFileBase64 = Convert.ToBase64String(File.ReadAllBytes(item.Path));
                record.OriginalAttributes = (int)File.GetAttributes(item.Path);
                break;
            case DesktopItemKind.Folder:
                record.OriginalAttributes = (int)File.GetAttributes(item.Path);
                var desktopIni = Path.Combine(item.Path, "desktop.ini");
                record.AuxiliaryFileExisted = File.Exists(desktopIni);
                if (record.AuxiliaryFileExisted)
                {
                    record.AuxiliaryFileBase64 = Convert.ToBase64String(File.ReadAllBytes(desktopIni));
                    record.AuxiliaryAttributes = (int)File.GetAttributes(desktopIni);
                }
                break;
            case DesktopItemKind.SystemIcon:
                CaptureRegistry(record);
                break;
        }
        return record;
    }

    private static void CaptureRegistry(BackupRecord record)
    {
        var relative = $@"Software\Classes\CLSID\{record.SystemClsid}\DefaultIcon";
        using var key = Registry.CurrentUser.OpenSubKey(relative);
        record.RegistryValues["__keyExists"] = key is null ? "false" : "true";
        foreach (var name in new[] { "", "empty", "full" })
            record.RegistryValues[name] = key?.GetValue(name) as string;
    }

    private BackupCatalog Load()
    {
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(IconDirectory);
        if (!File.Exists(CatalogPath)) return new BackupCatalog();
        try
        {
            return JsonSerializer.Deserialize<BackupCatalog>(File.ReadAllText(CatalogPath), _jsonOptions) ?? new BackupCatalog();
        }
        catch
        {
            var corruptPath = CatalogPath + $".corrupt-{DateTime.Now:yyyyMMddHHmmss}";
            File.Copy(CatalogPath, corruptPath, true);
            throw new InvalidDataException($"备份清单损坏，已保留为 {corruptPath}");
        }
    }

    private void Save(BackupCatalog catalog)
    {
        catalog.UpdatedAt = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(BackupDirectory);
        var tempPath = CatalogPath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(catalog, _jsonOptions));
        File.Move(tempPath, CatalogPath, true);
    }
}
