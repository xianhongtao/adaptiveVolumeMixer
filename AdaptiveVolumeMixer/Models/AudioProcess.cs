using System.Diagnostics;

namespace AdaptiveVolumeMixer.Models;

/// <summary>
/// 表示一个受管理的音频进程
/// </summary>
public class AudioProcess
{
    /// <summary>
    /// 进程ID
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// 进程名称（如 notepad.exe）
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// 进程显示名称（如 "Windows Media Player"）
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 所属层级（0 到 -5）
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// 当前音量（0.0 - 1.0）
    /// </summary>
    public float CurrentVolume { get; set; } = 1.0f;

    /// <summary>
    /// 原始音量（用户设定的正常音量）
    /// </summary>
    public float OriginalVolume { get; set; } = 1.0f;

    /// <summary>
    /// 是否正在播放音频
    /// </summary>
    public bool IsPlaying { get; set; }

    /// <summary>
    /// 是否被上级压制（音量被降低）
    /// </summary>
    public bool IsSuppressed { get; set; }

    /// <summary>
    /// 进程是否仍然存活
    /// </summary>
    public bool IsAlive
    {
        get
        {
            try
            {
                using var process = Process.GetProcessById(ProcessId);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }

    public override string ToString()
    {
        return $"{DisplayName} (层级 {Level}) - 音量: {CurrentVolume:P0} - {(IsPlaying ? "播放中" : "静默")}";
    }
}
