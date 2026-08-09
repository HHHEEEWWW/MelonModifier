using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MelonModifier.App.ViewModels;

/// <summary>日志页：读取游戏 MelonLoader/Logs 下的日志文件。</summary>
public sealed partial class LogsViewModel : ObservableObject
{
    private readonly AppState _state;

    public LogsViewModel(AppState state)
    {
        _state = state;

        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.SelectedGame))
                ReloadFiles();
            else if (e.PropertyName == nameof(AppState.RefreshTick))
                ReloadFiles();
        };
    }

    public ObservableCollection<string> LogFiles { get; } = new();

    [ObservableProperty]
    private bool _hasGame;

    [ObservableProperty]
    private string _gameHint = "请在游戏库中选择一个游戏";

    [ObservableProperty]
    private string? _selectedLog;

    [ObservableProperty]
    private string _logContent = "";

    [ObservableProperty]
    private bool _isLoading;

    partial void OnSelectedLogChanged(string? value)
    {
        if (value is not null)
            LoadLog(value);
    }

    [RelayCommand]
    private void RefreshLogs() => ReloadFiles();

    private void ReloadFiles()
    {
        var game = _state.SelectedGame;
        HasGame = game is not null;
        GameHint = game is null ? "请在游戏库中选择一个游戏" : $"日志目录：{game.Name}";

        var previous = SelectedLog;
        LogFiles.Clear();
        SelectedLog = null;
        LogContent = "";

        if (game is null)
            return;

        foreach (var file in _state.LogService.ListLogs(game))
            LogFiles.Add(file.Name);

        // 恢复之前的选中
        if (previous is not null)
        {
            var match = System.Linq.Enumerable.FirstOrDefault(LogFiles, f => f == previous);
            if (match is not null)
            {
                SelectedLog = match;
                return;
            }
        }

        if (LogFiles.Count > 0)
            SelectedLog = LogFiles[0];
    }

    private async void LoadLog(string fileName)
    {
        var game = _state.SelectedGame;
        if (game is null)
            return;

        var dir = Path.Combine(game.Path, "MelonLoader", "Logs");
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
            return;

        IsLoading = true;
        try
        {
            var fileInfo = new FileInfo(path);
            LogContent = await Task.Run(() => _state.LogService.ReadLog(fileInfo));
        }
        catch (Exception ex)
        {
            LogContent = "(读取失败: " + ex.Message + ")";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
