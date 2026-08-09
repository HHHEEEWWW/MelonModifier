using System.Windows;
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
    }
}
