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

        // ==== 临时诊断 ====
        Dispatcher.BeginInvoke(() =>
        {
            var diag = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "melondiag.txt");
            void Log(string s) => System.IO.File.AppendAllText(diag, s + "\n");
            System.IO.File.WriteAllText(diag, "start\n");
            try
            {
                Log("creating w1");
                var w1 = new System.Windows.Window
                {
                    Content = new System.Windows.Controls.Button(),
                    Width = 400, Height = 300,
                    WindowStyle = System.Windows.WindowStyle.None,
                    ShowInTaskbar = false,
                };
                Log("showing w1");
                w1.Show();
                w1.UpdateLayout();
                Log("w1 ok");

                Log("creating MainWindow");
                var mw = new MainWindow();
                var host = (System.Windows.Controls.ContentControl)mw.FindName("PageHost");
                host.Content = null;
                Log("showing MainWindow (empty)");
                mw.Show();
                mw.UpdateLayout();
                Log("MainWindow empty ok");

                void TryContent(string name, object content)
                {
                    try
                    {
                        host.Content = content;
                        mw.UpdateLayout();
                        Log("  content " + name + " OK");
                    }
                    catch (Exception ex)
                    {
                        var inner = ex;
                        while (inner.InnerException is not null) inner = inner.InnerException;
                        Log("  content " + name + " FAIL: " + inner.Message);
                    }
                }

                TryContent("AboutView", new Views.AboutView());
                TryContent("GameLibraryView", new Views.GameLibraryView());
                TryContent("empty", null);
                TryContent("ProgressBar only", new System.Windows.Controls.StackPanel
                {
                    Children =
                    {
                        new System.Windows.Controls.ProgressBar { Value = 50, Height = 4 },
                    },
                });
                TryContent("ScrollViewer only", new System.Windows.Controls.ScrollViewer
                {
                    Style = (System.Windows.Style)FindResource("SciScrollViewer"),
                });
                TryContent("ItemsControl only", new System.Windows.Controls.ItemsControl());

                w1.Close();
                Log("all ok");
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException is not null) inner = inner.InnerException;
                Log("FAIL: " + inner.Message);
            }
            Shutdown();
        });
    }
}
