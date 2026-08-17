namespace DesktopIconUpgrader.Models;

public sealed class BackupCatalog
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, BackupRecord> Items { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BackupRecord
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required DesktopItemKind Kind { get; set; }
    public required string Path { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? OriginalIconLocation { get; set; }
    public int OriginalIconIndex { get; set; }
    public string? OriginalFileBase64 { get; set; }
    public int OriginalAttributes { get; set; }
    public bool AuxiliaryFileExisted { get; set; }
    public string? AuxiliaryFileBase64 { get; set; }
    public int AuxiliaryAttributes { get; set; }
    public string? SystemClsid { get; set; }
    public Dictionary<string, string?> RegistryValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? GeneratedIconPath { get; set; }
}

public sealed record ShortcutData(
    string TargetPath,
    string Arguments,
    string WorkingDirectory,
    string Description,
    string IconLocation,
    int IconIndex);

public sealed record IconAnalysis(BackgroundKind Kind, byte Red, byte Green, byte Blue, double TransparentRatio, double Confidence)
{
    public string HexColor => $"#{Red:X2}{Green:X2}{Blue:X2}";
}

public sealed record OperationResult(DesktopIconItem Item, bool Success, string Message, string? GeneratedIconPath = null);
