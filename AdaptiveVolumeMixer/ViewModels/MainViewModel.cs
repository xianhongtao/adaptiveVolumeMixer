using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AdaptiveVolumeMixer.Models;
using AdaptiveVolumeMixer.Services;

namespace AdaptiveVolumeMixer.ViewModels;

/// <summary>
/// 主界面 ViewModel
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly AudioSessionService _audioManager;
    private readonly VolumeController _volumeController;
    private readonly ConfigManager _configManager;

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
    [ObservableProperty]
    private string _statusText = "就绪";

    /// <summary>
    /// 监控是否运行中
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotMonitoring))]
    [NotifyCanExecuteChangedFor(nameof(StartMonitoringCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopMonitoringCommand))]
    private bool _isMonitoring;

    public bool IsNotMonitoring => !IsMonitoring;

    /// <summary>
    /// 选中的可用进程
    /// </summary>
    [ObservableProperty]
    private AudioProcess? _selectedAvailableProcess;

    public MainViewModel(AudioSessionService audioManager, VolumeController volumeController, ConfigManager configManager)
    {
        _audioManager = audioManager;
        _volumeController = volumeController;
        _configManager = configManager;

        _volumeController.OnStateChanged += OnVolumeStateChanged;

        Levels.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Levels));
            RemoveLevelCommand.NotifyCanExecuteChanged();
        };

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
        // 按 Level 降序排列（0 为最高优先级，排在最上面）
        var sorted = _configManager.Config.Levels.OrderByDescending(l => l.Level).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            var level = sorted[i];
            var levelVm = new LevelViewModel
            {
                Level = level.Level,
                LevelIndex = i,
                DisplayName = level.DisplayName,
                SuppressRatio = level.SuppressVolumeRatio,
            };
            // 用闭包绑定，确保回调能识别是哪个 LevelViewModel
            var capturedVm = levelVm;
            levelVm.PropertyUpdated += (propName) => OnLevelPropertyUpdated(capturedVm, propName);
            Levels.Add(levelVm);
        }
    }

    /// <summary>
    /// 刷新可用进程列表
    /// </summary>
    [RelayCommand]
    private void RefreshAvailableProcesses()
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
    [RelayCommand(CanExecute = nameof(CanStartMonitoring))]
    private void StartMonitoring()
    {
        _volumeController.Start();
        IsMonitoring = true;
        StatusText = "监控已启动";
    }

    /// <summary>
    /// 停止监控
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStopMonitoring))]
    private void StopMonitoring()
    {
        _volumeController.Stop();
        IsMonitoring = false;
        StatusText = "监控已停止";
    }

    private bool CanStartMonitoring() => !IsMonitoring;
    private bool CanStopMonitoring() => IsMonitoring;

    /// <summary>
    /// 从层级移除进程
    /// </summary>
    [RelayCommand]
    private void RemoveProcess(LevelItemViewModel? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.ProcessName))
            return;

        _volumeController.RemoveProcessFromLevel(item.ProcessName, item.Level);
        RefreshLevelViews();
        StatusText = $"已从层级 {item.Level} 移除 {item.ProcessName}";
    }

    /// <summary>
    /// 将指定音频进程添加到指定层级（供拖拽调用）
    /// </summary>
    public void AddProcessToLevel(AudioProcess process, int level)
    {
        _volumeController.AddProcessToLevel(process.ProcessName, level);
        RefreshLevelViews();
        var levelVm = Levels.FirstOrDefault(l => l.Level == level);
        StatusText = $"已添加 {process.DisplayName} 到 {levelVm?.DisplayName ?? level.ToString()}";
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    [RelayCommand]
    private void SaveConfig()
    {
        _configManager.SaveConfig();
        StatusText = "配置已保存";
    }

    /// <summary>
    /// 添加新层级
    /// </summary>
    [RelayCommand]
    private void AddLevel()
    {
        // 新层级放在列表末尾（最低优先级），使用极低的 Level 值确保排序后排最后
        var newLevelConfig = new LevelConfig(int.MinValue, "新层级");
        _configManager.Config.Levels.Add(newLevelConfig);

        RenumberLevels();
        _configManager.SaveConfig();

        StatusText = "已添加新层级";
    }

    /// <summary>
    /// 删除指定层级
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRemoveLevel))]
    private void RemoveLevel(LevelViewModel? levelVm)
    {
        if (levelVm == null)
            return;

        string removedName = levelVm.DisplayName;

        // 从配置中移除
        var configToRemove = _configManager.Config.Levels
            .FirstOrDefault(l => l.Level == levelVm.Level);
        if (configToRemove != null)
        {
            // 将该层级的进程全部移除追踪
            foreach (var processName in configToRemove.ProcessNames)
            {
                _volumeController.RemoveProcessFromLevel(processName, levelVm.Level);
            }
            _configManager.Config.Levels.Remove(configToRemove);
        }

        RenumberLevels();
        _configManager.SaveConfig();

        StatusText = $"已删除 {removedName}";
    }

    private bool CanRemoveLevel => Levels.Count > 1;

    /// <summary>
    /// 按列表位置重新编号所有层级（索引 0 → Level 0，索引 N → Level -N）
    /// 同步更新配置、VolumeController 追踪进程的 Level，以及 LevelIndex
    /// </summary>
    private void RenumberLevels()
    {
        var config = _configManager.Config;
        // 按当前 Level 降序排列后重新编号
        var sortedConfigs = config.Levels.OrderByDescending(l => l.Level).ToList();

        // 更新配置中的 Level 值和 DisplayName
        for (int i = 0; i < sortedConfigs.Count; i++)
        {
            var cfg = sortedConfigs[i];
            cfg.Level = -i;

            // 如果名称仍为默认模板（如 "层级 N" 或 "新层级"），则自动更新
            if (string.IsNullOrWhiteSpace(cfg.DisplayName) ||
                cfg.DisplayName.StartsWith("层级 ") ||
                cfg.DisplayName == "新层级")
            {
                cfg.DisplayName = i == 0 ? "层级 0 (最高优先级)" : $"层级 {cfg.Level}";
            }
        }

        // 重新加载 UI 层级集合
        LoadConfig();

        // 同步 VolumeController 中追踪进程的层级
        _volumeController.ReassignProcessLevels();

        // 刷新层级视图
        RefreshLevelViews();
    }

    /// <summary>
    /// 层级属性变更回调（DisplayName 或 SuppressRatio 改变时同步到配置）
    /// </summary>
    private void OnLevelPropertyUpdated(LevelViewModel levelVm, string propertyName)
    {
        var configLevel = _configManager.Config.Levels.FirstOrDefault(l => l.Level == levelVm.Level);
        if (configLevel == null) return;

        configLevel.DisplayName = levelVm.DisplayName;
        configLevel.SuppressVolumeRatio = levelVm.SuppressRatio;
        _configManager.SaveConfig();
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
        App.Current.Dispatcher.Invoke(RefreshLevelViews);
    }
}
