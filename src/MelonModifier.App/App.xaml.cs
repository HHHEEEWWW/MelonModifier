using System.Windows;
using System.Windows.Media;
using MelonModifier.App.ViewModels;

namespace MelonModifier.App;

/// <summary>Interaction logic for App.xaml</summary>
public partial class App : Application
{
    /// <summary>应用级共享状态（所有页面共用同一实例）。</summary>
    public static AppState State { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MelonModifier.Core.Helpers.AppPaths.EnsureCreated();

        // 无 GPU/远程环境下硬件渲染可能直接崩溃（进程静默消失），强制软件渲染
        System.Windows.Media.RenderOptions.ProcessRenderMode =
            System.Windows.Interop.RenderMode.SoftwareOnly;

        // 应用已保存的外观设置（主题/字体）
        var settings = State.SettingsService.Current;
        ApplyTheme(settings.Theme);
        ApplyFont(settings.FontFamilyName);
    }

    /// <summary>
    /// 切换主题：把主题字典的全部 key 覆盖到 App.Resources 根字典。
    /// DynamicResource 引用会随根字典 key 变化自动刷新；
    /// 不修改 MergedDictionaries 列表，避免破坏控件模板的延迟 StaticResource 引用。
    /// </summary>
    public static void ApplyTheme(string themeName)
    {
        var themeFile = themeName == "Light" ? "Light.xaml" : "Dark.xaml";
        // 运行时创建 ResourceDictionary 必须用 pack URI
        // （XAML 里的相对 URI 由编译器转换；运行时相对 URI 会按工作目录解析）
        var theme = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{themeFile}", UriKind.Absolute),
        };

        var res = Current.Resources;
        foreach (var key in theme.Keys)
            res[key] = theme[key];
    }

    /// <summary>切换主字体族（Font.Main 为 DynamicResource，引用自动刷新）。</summary>
    public static void ApplyFont(string familyName)
    {
        if (string.IsNullOrWhiteSpace(familyName))
            return;
        try
        {
            Current.Resources["Font.Main"] = new FontFamily(familyName);
        }
        catch
        {
            // 无效字体名忽略，沿用默认
        }
    }
}
