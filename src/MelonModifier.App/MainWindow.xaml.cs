using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MelonModifier.App.ViewModels;

namespace MelonModifier.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // DataContext 在 BAML 加载完成后创建：
        // MainViewModel 的构造函数会实例化各页面（含复杂模板控件），
        // 若放在 XAML Resources 里，会发生在 BAML 加载过程中，
        // 此时资源字典未完全就绪，模板 TemplateBinding 可能取到 UnsetValue。
        DataContext = new MainViewModel();

        // 页面切换：手动设置 Content（避免 XAML 绑定在窗口渲染期才求值，
        // 与模板加载交错导致 TemplateBinding 取到 UnsetValue）
        var vm = (MainViewModel)DataContext;
        PageHost.Content = vm.SelectedPage.View;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedPage))
            {
                PageHost.Content = vm.SelectedPage.View;
                AnimatePageIn();
            }
        };
    }

    private void AnimatePageIn()
    {
        var host = PageHost;
        var content = host.Content as UIElement;
        if (content is null)
            return;

        content.Opacity = 0;
        var translate = new TranslateTransform(0, 8);
        content.RenderTransform = translate;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        var slide = new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        content.BeginAnimation(UIElement.OpacityProperty, fade);
        translate.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Maximize_Click(sender, e);
            return;
        }
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        MaxBtn.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
