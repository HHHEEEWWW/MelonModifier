using System.Windows.Controls;
using MelonModifier.App.ViewModels;

namespace MelonModifier.App.Views;

public partial class GameLibraryView : UserControl
{
    public GameLibraryView()
    {
        InitializeComponent();
        DataContext = new GameLibraryViewModel(App.State);
    }
}
