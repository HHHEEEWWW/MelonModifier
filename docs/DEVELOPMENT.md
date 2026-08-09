# 开发文档

## 架构

```
src/
├── MelonModifier.Core/          # 纯 .NET 类库（无 UI 依赖，可单测）
│   ├── Models/                  # GameInfo / ModInfo / MelonLoaderRelease / GameEngine
│   ├── Helpers/                 # VdfParser（Steam VDF/ACF 解析）、AppPaths
│   └── Services/
│       ├── GameScanner.cs       # Steam 扫描、Unity 引擎检测（Il2Cpp/Mono）、ML 状态检测
│       ├── MelonLoaderService.cs# GitHub Releases 下载、打补丁安装、卸载
│       ├── ModService.cs        # Mods/Plugins 列表、启停（.disabled）、安装、删除
│       ├── LogService.cs        # 读取 MelonLoader/Logs
│       ├── ConfigService.cs     # Loader.cfg 全文读写（保留注释）
│       └── GameRegistry.cs      # 手动添加游戏的持久化（%AppData%/MelonModifier/games.json）
└── MelonModifier.App/           # WPF
    ├── Themes/                  # Palette.xaml（色板）+ Controls.xaml（控件模板）
    ├── ViewModels/              # AppState（共享状态+服务实例）/ MainViewModel / 各页 VM
    ├── Views/                   # 游戏库 / Mods / 日志 / 配置 / 关于
    └── Converters/              # 布尔/状态 → Visibility/Brush 等转换器
```

## 关键设计

### 共享状态（AppState）
- `App.State` 静态单例持有全部 Core 服务实例与 `Games` 列表、`SelectedGame`。
- 页面通过订阅 `AppState.PropertyChanged`（`SelectedGame` / `RefreshTick`）自动重载。
- 安装 / 卸载 / 扫描完成后调用 `State.RequestRefresh()` 通知各页刷新。

### 页面切换
- `MainViewModel.SelectedPage` 驱动 `PageHost`（ContentControl，code-behind 手动赋值 + 淡入动画）。
- 侧边导航 `NavPage.IsSelected`（RadioButton 双向绑定）→ PropertyChanged → 更新 `SelectedPage`。

### 启动要点（踩坑记录）
1. **首个窗口必须是简单窗口**：复杂模板控件（含 `{TemplateBinding}`）作为首个窗口创建时，
   资源字典未完全就绪，`TemplateBinding` 求值可能得到 `UnsetValue`（抛
   `'UnsetValue' is not a valid value for property 'BorderBrush'`）。
   使用 `StartupUri`（框架在消息循环内创建首个窗口）可规避；不要用 `Dispatcher.BeginInvoke` 手动 Show。
2. **不要在任何 XAML Resources 里实例化 ViewModel**（其构造函数创建复杂 UserControl 会在 BAML 加载中执行）。
3. `Brush.BorderGlow` 曾缺失导致模板 StaticResource 求值失败（`UnsetValue`）——修改 Palette.xaml 后务必
   用 `grep` 核对所有 `{StaticResource X}` 引用都有定义。
4. 无 GPU / 远程桌面环境：`RenderOptions.ProcessRenderMode = SoftwareOnly`（App.OnStartup）。
5. .NET 9 有 WPF 资源引用回归（dotnet/wpf#9354），本项目钉在 **net8.0-windows**。

### Steam 扫描
- 注册表 `HKCU\Software\Valve\Steam\SteamPath` → `steamapps/libraryfolders.vdf`（多库）→ `appmanifest_*.acf`。
- Unity 判定：`GameAssembly.dll`（Il2Cpp）或 `*_Data/Managed/Assembly-CSharp.dll`（Mono）。
- MelonLoader 判定：`version.dll` + `MelonLoader/` 存在；版本读 `MelonLoader/net6/MelonLoader.dll` 文件版本。
- 手动添加的游戏持久化在 `games.json`，扫描结果动态合并。

## 验证方式

- 构建：`dotnet build MelonModifier.sln`（0 警告 0 错误）
- 运行：`dotnet run --project src/MelonModifier.App`
- 本机实测（Windows + Steam）：扫描发现 IRON NEST（Il2Cpp）、安装/升级全链路、Mods 列表、日志、配置读写均通过。
