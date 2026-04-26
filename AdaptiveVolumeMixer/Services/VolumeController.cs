using AdaptiveVolumeMixer.Models;

namespace AdaptiveVolumeMixer.Services;

/// <summary>
/// 音量控制核心逻辑
/// 当上级软件开始播放时，将下级音量降低到指定比例
/// </summary>
public class VolumeController : IDisposable
{
    private readonly AudioSessionService _audioManager;
    private readonly ConfigManager _configManager;
    private readonly Dictionary<int, AudioProcess> _trackedProcesses = new();
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private bool _isRunning;

    /// <summary>
    /// 受追踪的进程列表
    /// </summary>
    public IReadOnlyDictionary<int, AudioProcess> TrackedProcesses => _trackedProcesses;

    /// <summary>
    /// 状态更新事件
    /// </summary>
    public event Action? OnStateChanged;

    public VolumeController(AudioSessionService audioManager, ConfigManager configManager)
    {
        _audioManager = audioManager;
        _configManager = configManager;
    }

    /// <summary>
    /// 启动监控
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;

        _cts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
    }

    /// <summary>
    /// 停止监控
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
        _monitorTask?.Wait(2000);
    }

    /// <summary>
    /// 手动刷新进程列表
    /// </summary>
    public void RefreshProcesses()
    {
        var config = _configManager.Config;
        var allSessions = _audioManager.GetAllAudioSessions();

        lock (_lock)
        {
            // 标记所有现有进程为待移除
            var toRemove = new List<int>(_trackedProcesses.Keys);

            foreach (var session in allSessions)
            {
                // 检查该进程是否在配置的层级中
                int? matchedLevel = null;
                foreach (var level in config.Levels)
                {
                    if (level.ProcessNames.Any(p =>
                            session.ProcessName.Contains(p, StringComparison.OrdinalIgnoreCase) ||
                            p.Contains(session.ProcessName, StringComparison.OrdinalIgnoreCase)))
                    {
                        matchedLevel = level.Level;
                        break;
                    }
                }

                if (matchedLevel.HasValue)
                {
                    session.Level = matchedLevel.Value;
                    toRemove.Remove(session.ProcessId);

                    if (_trackedProcesses.TryGetValue(session.ProcessId, out var existing))
                    {
                        // 更新现有进程状态
                        existing.IsPlaying = session.IsPlaying;
                        existing.CurrentVolume = session.CurrentVolume;
                    }
                    else
                    {
                        // 添加新进程
                        _trackedProcesses[session.ProcessId] = session;
                    }
                }
            }

            // 移除已退出的进程
            foreach (var pid in toRemove)
            {
                _trackedProcesses.Remove(pid);
            }
        }

        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// 监控循环
    /// </summary>
    private async Task MonitorLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                RefreshProcesses();
                ApplyVolumeRules();
                await Task.Delay(_configManager.Config.PollingIntervalMs, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"监控循环异常: {ex.Message}");
                await Task.Delay(2000, CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// 应用音量规则
    /// 核心逻辑：如果某个层级有软件正在播放，则所有下级音量降至20%
    /// </summary>
    public void ApplyVolumeRules()
    {
        var config = _configManager.Config;
        var levels = config.Levels.OrderByDescending(l => l.Level).ToList();

        lock (_lock)
        {
            // 先实时刷新所有追踪进程的播放状态
            foreach (var process in _trackedProcesses.Values)
            {
                var state = _audioManager.GetProcessPlayingState(process.ProcessId);
                if (state.HasValue)
                {
                    process.IsPlaying = state.Value;
                }
            }

            // 检查每个层级是否有进程正在播放
            var playingLevels = new HashSet<int>();
            foreach (var process in _trackedProcesses.Values)
            {
                if (process.IsPlaying)
                {
                    playingLevels.Add(process.Level);
                }
            }

            System.Diagnostics.Debug.WriteLine(
                $"[ApplyVolumeRules] 追踪进程: {_trackedProcesses.Count}, 播放中层级: [{string.Join(",", playingLevels)}]");

            // 如果有上级正在播放，则下级需要被压制
            foreach (var process in _trackedProcesses.Values)
            {
                bool shouldSuppress = false;
                float targetVolume = process.OriginalVolume;

                foreach (var upperLevel in playingLevels)
                {
                    // 层级数值越大优先级越高：0 > -1 > -2 > -3 > -4 > -5
                    if (upperLevel > process.Level)
                    {
                        shouldSuppress = true;
                        break;
                    }
                }

                if (shouldSuppress)
                {
                    // 找到对应的层级配置，获取压制比例
                    var levelConfig = levels.FirstOrDefault(l => l.Level == process.Level);
                    float suppressRatio = levelConfig?.SuppressVolumeRatio ?? 0.2f;
                    targetVolume = process.OriginalVolume * suppressRatio;
                }

                // 始终应用音量（移除阈值判断，确保每次都能生效）
                process.CurrentVolume = targetVolume;
                process.IsSuppressed = shouldSuppress;
                bool success = _audioManager.SetProcessVolume(process.ProcessId, targetVolume);

                System.Diagnostics.Debug.WriteLine(
                    $"[ApplyVolumeRules] {process.ProcessName}(PID:{process.ProcessId}) " +
                    $"层级:{process.Level} 播放:{process.IsPlaying} 压制:{shouldSuppress} " +
                    $"音量:{targetVolume:P0} 设置{(success ? "成功" : "失败")}");
            }
        }

        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// 手动设置进程的原始音量
    /// </summary>
    public void SetProcessOriginalVolume(int processId, float volume)
    {
        lock (_lock)
        {
            if (_trackedProcesses.TryGetValue(processId, out var process))
            {
                process.OriginalVolume = Math.Clamp(volume, 0.0f, 1.0f);
                if (!process.IsSuppressed)
                {
                    process.CurrentVolume = process.OriginalVolume;
                    _audioManager.SetProcessVolume(processId, process.OriginalVolume);
                }
            }
        }
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// 手动添加进程到指定层级
    /// </summary>
    public void AddProcessToLevel(string processName, int level)
    {
        var config = _configManager.Config;
        var levelConfig = config.Levels.FirstOrDefault(l => l.Level == level);
        if (levelConfig != null)
        {
            if (!levelConfig.ProcessNames.Contains(processName, StringComparer.OrdinalIgnoreCase))
            {
                levelConfig.ProcessNames.Add(processName);
                _configManager.SaveConfig();
                RefreshProcesses();
            }
        }
    }

    /// <summary>
    /// 从层级中移除进程
    /// </summary>
    public void RemoveProcessFromLevel(string processName, int level)
    {
        var config = _configManager.Config;
        var levelConfig = config.Levels.FirstOrDefault(l => l.Level == level);
        if (levelConfig != null)
        {
            levelConfig.ProcessNames.RemoveAll(p =>
                p.Equals(processName, StringComparison.OrdinalIgnoreCase));
            _configManager.SaveConfig();
            RefreshProcesses();
        }
    }

    public void Dispose()
    {
        Stop();
        _audioManager.Dispose();
    }
}
