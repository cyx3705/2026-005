# 桌面图标升级器

基于 C#、.NET 8 和 WPF 的 Windows 桌面图标智能升级工具。

## 已实现

- 批量扫描当前用户桌面和公共桌面，不为每个项目启动 PowerShell。
- 支持 `.lnk`、`.url`、桌面文件夹和可见系统桌面图标。
- 普通文件会显示为“不可独立替换”，不会偷偷修改整个文件类型关联。
- 优先通过 Windows Shell 提取 256px 图标资源。
- 自动判断透明背景或有色背景。
- 有色背景保留原背景并直接裁成圆角矩形。
- 透明背景提取主体主色，生成向白色偏移 38% 的圆角背景。
- 从 1024px 母版独立生成 16、20、24、32、40、48、64、96、128、256px 十尺寸 ICO。
- 一键升级全部、升级选中、一键还原全部、还原选中和中途取消。
- 首次修改前建立不可覆盖的原始基线；再次升级不会覆盖首次备份。
- 替换后逐项验证，失败时自动尝试回滚该项目。

## 运行

直接运行发布目录中的 `DesktopIconUpgrader.exe`。

首次扫描后：

1. 点击“一键升级全部”处理所有受支持项目。
2. 或通过列表复选框选择项目，再点击“升级选中”。
3. 点击“一键还原全部”可恢复到首次修改前状态。

备份和生成图标存储在：

```text
%LocalAppData%\OneHistory\DesktopIconUpgrader
```

不要手动删除该目录，否则会失去还原所需的基线数据。

## 构建

```powershell
dotnet build DesktopIconUpgrader.sln -c Release --configfile NuGet.Config
dotnet run --project tests\DesktopIconUpgrader.SmokeTests -c Release
```

项目没有第三方 NuGet 依赖，可完全离线构建。

## 项目结构

- `src/DesktopIconUpgrader`：WPF 应用。
- `Models`：桌面项目、图标分析和备份数据模型。
- `Interop`：Windows Shell、快捷方式和图标提取接口。
- `Services/DesktopInventoryService`：桌面项目扫描。
- `Services/IconSourceResolver`：高分辨率原图提取。
- `Services/IconRenderer`：背景判断和图标重绘。
- `Services/IcoWriter`：多尺寸 ICO 写入与回读验证。
- `Services/BackupStore`：不可覆盖基线备份。
- `Services/DesktopIconApplier`：替换、验证与还原。
- `tests/DesktopIconUpgrader.SmokeTests`：不修改真实桌面的事务测试。

## 权限说明

应用默认以普通用户权限运行。当前用户桌面和用户级系统图标无需管理员权限；公共桌面中受保护的项目可能返回“拒绝访问”，其他项目仍会继续处理并在结果中报告。
