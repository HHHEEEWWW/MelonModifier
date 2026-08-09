using System.Windows.Controls;
using MelonModifier.App.ViewModels;

namespace MelonModifier.App.Views;

public partial class LogsView : UserControl
{
    public LogsView()
    {
        InitializeComponent();
        DataContext = new LogsViewModel(App.State);
    }
}
