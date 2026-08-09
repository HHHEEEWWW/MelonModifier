using System.Windows.Controls;
using MelonModifier.App.ViewModels;

namespace MelonModifier.App.Views;

public partial class CompatibilityView : UserControl
{
    public CompatibilityView()
    {
        InitializeComponent();
        DataContext = new CompatibilityViewModel();
    }
}
