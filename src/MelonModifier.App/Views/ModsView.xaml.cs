using System.Windows;
using System.Windows.Controls;
using MelonModifier.App.ViewModels;

namespace MelonModifier.App.Views;

public partial class ModsView : UserControl
{
    private readonly ModsViewModel _vm;

    public ModsView()
    {
        InitializeComponent();
        _vm = new ModsViewModel(App.State);
        DataContext = _vm;
    }

    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Grid_Drop(object sender, DragEventArgs e) => DropCore(e, isPlugin: false);

    private void ModsPanel_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void ModsPanel_Drop(object sender, DragEventArgs e) => DropCore(e, isPlugin: false);

    private void PluginsPanel_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void PluginsPanel_Drop(object sender, DragEventArgs e) => DropCore(e, isPlugin: true);

    private void DropCore(DragEventArgs e, bool isPlugin)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
            return;

        foreach (var file in files)
        {
            if (file.EndsWith(".dll", System.StringComparison.OrdinalIgnoreCase))
                _vm.InstallDroppedFile(file, isPlugin);
        }
        e.Handled = true;
    }
}
