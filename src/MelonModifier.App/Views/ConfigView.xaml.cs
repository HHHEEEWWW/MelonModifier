using System.Windows.Controls;
using MelonModifier.App.ViewModels;

namespace MelonModifier.App.Views;

public partial class ConfigView : UserControl
{
    public ConfigView()
    {
        InitializeComponent();
        DataContext = new ConfigViewModel(App.State);
    }
}
