using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace DesktopIconUpgrader.Models;

public enum DesktopItemKind
{
    Shortcut,
    InternetShortcut,
    Folder,
    SystemIcon,
    UnsupportedFile
}

public enum BackgroundKind
{
    Unknown,
    Transparent,
    Colored
}

public sealed class DesktopIconItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private string _status = "待处理";
    private ImageSource? _originalPreview;
    private ImageSource? _generatedPreview;
    private BackgroundKind _backgroundKind;

    public required string Id { get; init; }
    public required string Name { get; init; }
    public required DesktopItemKind Kind { get; init; }
    public required string Path { get; init; }
    public string? TargetPath { get; init; }
    public string? IconLocation { get; init; }
    public int IconIndex { get; init; }
    public string? SystemClsid { get; init; }
    public bool IsSupported { get; init; }
    public bool IsPublicDesktop { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public ImageSource? OriginalPreview
    {
        get => _originalPreview;
        set => SetField(ref _originalPreview, value);
    }

    public ImageSource? GeneratedPreview
    {
        get => _generatedPreview;
        set => SetField(ref _generatedPreview, value);
    }

    public BackgroundKind BackgroundKind
    {
        get => _backgroundKind;
        set => SetField(ref _backgroundKind, value);
    }

    public string KindLabel => Kind switch
    {
        DesktopItemKind.Shortcut => "快捷方式",
        DesktopItemKind.InternetShortcut => "网址",
        DesktopItemKind.Folder => "文件夹",
        DesktopItemKind.SystemIcon => "系统图标",
        _ => "不可独立替换"
    };

    public string BackgroundLabel => BackgroundKind switch
    {
        BackgroundKind.Transparent => "透明底",
        BackgroundKind.Colored => "有色底",
        _ => "待判断"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        if (propertyName == nameof(BackgroundKind))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundLabel)));
    }
}
