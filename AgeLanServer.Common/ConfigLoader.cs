using Tomlyn;
using Tomlyn.Model;

namespace AgeLanServer.Common;

/// <summary>
/// Tiện ích nạp cấu hình từ file TOML, biến môi trường và dòng lệnh.
/// Tương đương common/config.go trong bản Go gốc (dùng Koanf).
/// </summary>
public static class ConfigLoader
{
    /// <summary>
    /// Nạp cấu hình TOML từ file. Ưu tiên theo thứ tự: thư mục exe → thư mục hiện tại.
    /// Trả về null nếu không tìm thấy file.
    /// </summary>
    public static TomlTable? LoadTomlConfig(params string[] candidatePaths)
    {
        foreach (var path in candidatePaths)
        {
            if (string.IsNullOrEmpty(path))
                continue;

            if (File.Exists(path))
            {
                try
                {
                    var content = File.ReadAllText(path);
                    var model = Toml.ToModel(content);
                    return model;
                }
                catch (Exception ex)
                {
                    throw new ConfigLoadException(path, ex);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Ghi cấu hình dưới dạng TOML ra file.
    /// </summary>
    public static void SaveTomlConfig(Dictionary<string, object> config, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var toml = Toml.FromModel(config);
        File.WriteAllText(path, toml);
    }

    /// <summary>
    /// Lấy giá trị từ biến môi trường với tiền tố ứng dụng.
    /// Chuyển đổi key từ dạng "Section_Key" sang "Section.Key".
    /// </summary>
    public static string? GetEnvValue(string key, string? prefix = null)
    {
        var envPrefix = (prefix ?? AppConstants.Name).Replace("-", "_").ToUpperInvariant() + "_";
        var envKey = (envPrefix + key).Replace(".", "_").Replace("-", "_").ToUpperInvariant();
        return Environment.GetEnvironmentVariable(envKey);
    }

    /// <summary>
    /// Đọc giá trị từ cấu hình với fallback theo thứ tự: CLI args → env → TOML → default.
    /// </summary>
    public static T ResolveValue<T>(
        T? cliValue,
        string configKey,
        TomlTable? tomlConfig,
        T defaultValue)
    {
        // Ưu tiên CLI
        if (cliValue != null)
            return cliValue;

        // Biến môi trường
        var envValue = GetEnvValue(configKey);
        if (!string.IsNullOrEmpty(envValue))
        {
            try
            {
                return (T)Convert.ChangeType(envValue, typeof(T));
            }
            catch
            {
                // Fallback xuống dưới
            }
        }

        // TOML config
        if (tomlConfig != null)
        {
            var keys = configKey.Split('.');
            object? current = tomlConfig;
            foreach (var k in keys)
            {
                if (current is TomlTable table && table.TryGetValue(k, out var val))
                    current = val;
                else
                {
                    current = null;
                    break;
                }
            }

            if (current != null)
            {
                try
                {
                    return (T)Convert.ChangeType(current, typeof(T));
                }
                catch
                {
                    // Fallback xuống dưới
                }
            }
        }

        // Default
        return defaultValue;
    }
}

/// <summary>
/// Ngoại lệ khi không thể nạp file cấu hình.
/// </summary>
public class ConfigLoadException : Exception
{
    public string ConfigPath { get; }

    public ConfigLoadException(string path, Exception inner)
        : base($"Không thể nạp file cấu hình '{path}': {inner.Message}", inner)
    {
        ConfigPath = path;
    }
}
