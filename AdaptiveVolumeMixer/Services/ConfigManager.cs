using System.IO;
using System.Text.Json;
using AdaptiveVolumeMixer.Models;

namespace AdaptiveVolumeMixer.Services;

/// <summary>
/// 配置管理器，负责读写 JSON 配置文件
/// </summary>
public class ConfigManager
{
    private readonly string _configPath;
    private AppConfig _config;

    /// <summary>
    /// 当前配置
    /// </summary>
    public AppConfig Config => _config;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public ConfigManager()
    {
        // 配置文件放在可执行文件同目录
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        _configPath = Path.Combine(exeDir, "config.json");
        _config = LoadConfig();
    }

    /// <summary>
    /// 加载配置
    /// </summary>
    private AppConfig LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (config != null)
                    return config;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}");
        }

        return AppConfig.CreateDefault();
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    public void SaveConfig()
    {
        try
        {
            string json = JsonSerializer.Serialize(_config, JsonOptions);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 重置为默认配置
    /// </summary>
    public void ResetToDefault()
    {
        _config = AppConfig.CreateDefault();
        SaveConfig();
    }
}
