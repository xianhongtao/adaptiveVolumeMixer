using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using AdaptiveVolumeMixer.Models;
using AdaptiveVolumeMixer.Services;

namespace AdaptiveVolumeMixer.ViewModels;

/// <summary>
/// 主界面 ViewModel
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly AudioSessionService _audioManager;
    private readonly VolumeController _volumeController;
    private readonly ConfigManager _configManager;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 层级视图集合
    /// </summary>
    public ObservableCollection<LevelViewModel> Levels { get; } = new();

    /// <summary>
    /// 所有可用的音频进程（用于添加到层级）
    /// </summary>
    public ObservableCollection<AudioProcess> AvailableProcesses { get; } = new();

    /// <summary>
    /// 状态信息
    /// </summary>
    private string _statusText = "就绪";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 监控是否运行中
    /// </summary>
    private bool _isMonitoring;
    public bool IsMonitoring
    {
        get => _isMonitoring;
        set { _isMonitoring = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotMonitoring)); }
    }

    public bool IsNotMonitoring => !IsMonitoring;

    /// <summary>
    /// 选中的可用进程
    /// </summary>
    private AudioProcess? _selectedAvailableProcess;
    public AudioProcess? SelectedAvailableProcess
    {
        get => _selectedAvailableProcess;
        set { _selectedAvailableProcess = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 选中的目标层级
    /// </summary>
    private LevelViewModel? _selectedTargetLevel;
    public LevelViewModel? SelectedTargetLevel
    {
        get => _selectedTargetLevel;
        set { _selectedTargetLevel = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 新进程名称输入
    /// </summary>
    private string _newProcessName = string.Empty;
    public string NewProcessName
    {
        get => _newProcessName;
        set { _newProcessName = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 新进程目标层级
    /// </summary>
    private int _newProcessLevel;
    public int NewProcessLevel
    {
        get => _newProcessLevel;
        set { _newProcessLevel = value; OnPropertyChanged(); }
    }

    public ICommand StartMonitoringCommand { get; }
    public ICommand StopMonitoringCommand { get; }
    public ICommand RefreshProcessesCommand { get; }
    public ICommand AddProcessCommand { get; }
    public ICommand RemoveProcessCommand { get; }
    public ICommand AddAvailableProcessToLevelCommand { get; }
    public ICommand SaveConfigCommand { get; }

    public MainViewModel()
    {
        _configManager = new ConfigManager();
        _audioManager = new AudioSessionService();
        _volumeController = new VolumeController(_audioManager, _configManager);

        StartMonitoringCommand = new RelayCommand(StartMonitoring, () => IsNotMonitoring);
        StopMonitoringCommand = new RelayCommand(StopMonitoring, () => IsMonitoring);
        RefreshProcessesCommand = new RelayCommand(RefreshAvailableProcesses);
        AddProcessCommand = new RelayCommand(AddProcess);
        RemoveProcessCommand = new RelayCommand<LevelItemViewModel>(RemoveProcess);
        AddAvailableProcessToLevelCommand = new RelayCommand(AddAvailableProcessToLevel);
        SaveConfigCommand = new RelayCommand(SaveConfig);

        _volumeController.OnStateChanged += OnVolumeStateChanged;

        LoadConfig();
        InitializeAudio();
        RefreshAvailableProcesses();
    }

    private void InitializeAudio()
    {
        bool success = _audioManager.Initialize();
        StatusText = success ? "音频系统初始化成功" : "音频系统初始化失败，请以管理员身份运行";
    }

    private void LoadConfig()
    {
        Levels.Clear();
        foreach (var level in _configManager.Config.Levels.OrderByDescending(l => l.Level))
        {
            Levels.Add(new LevelViewModel
            {
                Level = level.Level,
                DisplayName = level.DisplayName,
                SuppressRatio = level.SuppressVolumeRatio,
            });
        }
    }

    /// <summary>
    /// 刷新可用进程列表
    /// </summary>
    public void RefreshAvailableProcesses()
    {
        var sessions = _audioManager.GetAllAudioSessions();
        AvailableProcesses.Clear();
        foreach (var session in sessions.OrderBy(s => s.DisplayName))
        {
            AvailableProcesses.Add(session);
        }
        StatusText = $"已发现 {sessions.Count} 个音频进程";
    }

    /// <summary>
    /// 启动监控
    /// </summary>
    public void StartMonitoring()
    {
        _volumeController.Start();
        IsMonitoring = true;
        StatusText = "监控已启动";
    }

    /// <summary>
    /// 停止监控
    /// </summary>
    public void StopMonitoring()
    {
        _volumeController.Stop();
        IsMonitoring = false;
        StatusText = "监控已停止";
    }

    /// <summary>
    /// 添加进程到层级
    /// </summary>
    private void AddProcess()
    {
        if (string.IsNullOrWhiteSpace(NewProcessName))
            return;

        string processName = NewProcessName.Trim();
        if (!processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            processName += ".exe";

        _volumeController.AddProcessToLevel(processName, NewProcessLevel);
        NewProcessName = string.Empty;
        RefreshLevelViews();
        StatusText = $"已添加 {processName} 到层级 {NewProcessLevel}";
    }

    /// <summary>
    /// 从层级移除进程
    /// </summary>
    private void RemoveProcess(LevelItemViewModel? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.ProcessName))
            return;

        _volumeController.RemoveProcessFromLevel(item.ProcessName, item.Level);
        RefreshLevelViews();
        StatusText = $"已从层级 {item.Level} 移除 {item.ProcessName}";
    }

    /// <summary>
    /// 将选中的可用进程添加到层级
    /// </summary>
    private void AddAvailableProcessToLevel()
    {
        if (SelectedAvailableProcess == null || SelectedTargetLevel == null)
            return;

        _volumeController.AddProcessToLevel(SelectedAvailableProcess.ProcessName, SelectedTargetLevel.Level);
        RefreshLevelViews();
        StatusText = $"已添加 {SelectedAvailableProcess.DisplayName} 到 {SelectedTargetLevel.DisplayName}";
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    private void SaveConfig()
    {
        _configManager.SaveConfig();
        StatusText = "配置已保存";
    }

    /// <summary>
    /// 刷新层级视图
    /// </summary>
    private void RefreshLevelViews()
    {
        var config = _configManager.Config;
        foreach (var levelVm in Levels)
        {
            var levelConfig = config.Levels.FirstOrDefault(l => l.Level == levelVm.Level);
            if (levelConfig == null) continue;

            levelVm.Processes.Clear();
            foreach (var processName in levelConfig.ProcessNames)
            {
                // 查找该进程是否在追踪列表中
                var tracked = _volumeController.TrackedProcesses.Values
                    .FirstOrDefault(p => p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));

                levelVm.Processes.Add(new LevelItemViewModel
                {
                    Level = levelVm.Level,
                    ProcessName = processName,
                    DisplayName = tracked?.DisplayName ?? processName,
                    IsPlaying = tracked?.IsPlaying ?? false,
                    IsSuppressed = tracked?.IsSuppressed ?? false,
                    CurrentVolume = tracked?.CurrentVolume ?? 1.0f,
                    OriginalVolume = tracked?.OriginalVolume ?? 1.0f,
                });
            }
        }
    }

    private void OnVolumeStateChanged()
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            RefreshLevelViews();
        });
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// 层级视图模型
/// </summary>
public class LevelViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public int Level { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public float SuppressRatio { get; set; } = 0.2f;

    public ObservableCollection<LevelItemViewModel> Processes { get; } = new();

    public string ProcessCount => $"({Processes.Count} 个进程)";

    public void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>
/// 层级中的进程项视图模型
/// </summary>
public class LevelItemViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public int Level { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set { _isPlaying = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    private bool _isSuppressed;
    public bool IsSuppressed
    {
        get => _isSuppressed;
        set { _isSuppressed = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    private float _currentVolume;
    public float CurrentVolume
    {
        get => _currentVolume;
        set { _currentVolume = value; OnPropertyChanged(); OnPropertyChanged(nameof(VolumeText)); }
    }

    private float _originalVolume;
    public float OriginalVolume
    {
        get => _originalVolume;
        set { _originalVolume = value; OnPropertyChanged(); }
    }

    public string VolumeText => $"{CurrentVolume:P0}";
    public string StatusText => IsPlaying ? "🔊 播放中" : (IsSuppressed ? "🔇 被压制" : "🔈 静默");

    public void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>
/// 简单的 ICommand 实现
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
    {
        if (parameter is T t)
            return _canExecute?.Invoke(t) ?? true;
        return _canExecute?.Invoke(default) ?? true;
    }

    public void Execute(object? parameter)
    {
        if (parameter is T t)
            _execute(t);
        else
            _execute(default);
    }
}
