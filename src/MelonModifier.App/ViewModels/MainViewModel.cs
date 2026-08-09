using System.Collections.ObjectModel;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MelonModifier.App.ViewModels;

/// <summary>侧边导航的一个页面项。</summary>
public sealed partial class NavPage : ObservableObject
{
    public required string Title { get; init; }
    public required string Icon { get; init; }
    public required UserControl View { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>主窗口 ViewModel：导航与页面切换。</summary>
public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel()
    {
        State = App.State;

        Pages = new ObservableCollection<NavPage>
        {
            new() { Title = "游戏库", Icon = "\uE8F1", View = new Views.GameLibraryView() },
            new() { Title = "Mods", Icon = "\uE8D7", View = new Views.ModsView() },
            new() { Title = "日志", Icon = "\uE9D9", View = new Views.LogsView() },
            new() { Title = "配置", Icon = "\uE713", View = new Views.ConfigView() },
            new() { Title = "关于", Icon = "\uE946", View = new Views.AboutView() },
        };

        foreach (var page in Pages)
        {
            var p = page;
            p.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(NavPage.IsSelected) && p.IsSelected)
                    SelectedPage = p;
            };
        }

        SelectedPage = Pages[0];
        Pages[0].IsSelected = true;
    }

    public AppState State { get; }

    public ObservableCollection<NavPage> Pages { get; }

    private NavPage _selectedPage = null!;

    public NavPage SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (ReferenceEquals(_selectedPage, value))
                return;

            if (_selectedPage is not null)
                _selectedPage.IsSelected = false;
            _selectedPage = value;
            OnPropertyChanged();
            if (_selectedPage is not null)
                _selectedPage.IsSelected = true;
        }
    }
}
