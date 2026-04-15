namespace AgeLanServer.Server.Models;

/// <summary>
/// Giao diện tệp đám mây (cloud files).
/// </summary>
public interface ICloudFiles
{
    // Cloud file operations
}

/// <summary>
/// Triển khai mặc định trống cho CloudFiles.
/// </summary>
public class EmptyCloudFiles : ICloudFiles { }

/// <summary>
/// Helper để xây dựng chỉ mục cloud files.
/// </summary>
public static class CloudFilesHelper
{
    public static ICloudFiles? BuildCloudfilesIndex(string configPath, string cloudFolder)
    {
        if (!Directory.Exists(cloudFolder)) return null;
        return new EmptyCloudFiles();
    }
}
