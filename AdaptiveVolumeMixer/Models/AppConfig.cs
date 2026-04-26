namespace AdaptiveVolumeMixer.Models;

/// <summary>
/// 应用程序全局配置
/// </summary>
public class AppConfig
{
    /// <summary>
    /// 层级列表（从高到低排序）
    /// </summary>
    public List<LevelConfig> Levels { get; set; } = new();

    /// <summary>
    /// 轮询间隔（毫秒），用于检测音频播放状态
    /// </summary>
    public int PollingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 创建默认配置
    /// </summary>
    public static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            PollingIntervalMs = 1000,
            Levels = new List<LevelConfig>
            {
                new(0, "层级 0 (最高优先级)"),
                new(-1, "层级 -1"),
                new(-2, "层级 -2"),
                new(-3, "层级 -3"),
                new(-4, "层级 -4"),
                new(-5, "层级 -5 (最低优先级)"),
            }
        };
    }
}
