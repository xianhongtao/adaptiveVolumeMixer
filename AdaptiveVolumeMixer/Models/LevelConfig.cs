namespace AdaptiveVolumeMixer.Models;

/// <summary>
/// 层级配置
/// </summary>
public class LevelConfig
{
    /// <summary>
    /// 层级编号（0 到 -5）
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// 层级显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 该层级下的进程列表（按进程名匹配）
    /// </summary>
    public List<string> ProcessNames { get; set; } = new();

    /// <summary>
    /// 被上级压制时的目标音量比例（默认 20%）
    /// </summary>
    public float SuppressVolumeRatio { get; set; } = 0.2f;

    public LevelConfig() { }

    public LevelConfig(int level, string displayName)
    {
        Level = level;
        DisplayName = displayName;
    }
}
