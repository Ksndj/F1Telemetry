using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace F1Telemetry.App.Logging;

/// <summary>
/// Represents display metadata for the latest app-owned log file.
/// </summary>
public sealed record LogFileInfo(
    string FilePathText,
    string FileSizeText,
    string LastWriteTimeText,
    string ErrorMessage);

/// <summary>
/// Represents the result of opening an app-owned log directory.
/// </summary>
public sealed record LogDirectoryOpenResult(bool Succeeded, string ErrorMessage);

/// <summary>
/// Provides generic log directory and latest-file metadata for Settings.
/// </summary>
public sealed class LogDirectoryService
{
    private const string EmptyText = "无";
    private const string UnavailableText = "不可用";
    private readonly Action<string> _openDirectory;

    /// <summary>
    /// Initializes a new log directory helper.
    /// </summary>
    public LogDirectoryService(Action<string>? openDirectory = null)
    {
        _openDirectory = openDirectory ?? OpenDirectoryWithShell;
    }

    /// <summary>
    /// Gets safe display metadata for the latest matching log file.
    /// </summary>
    public LogFileInfo GetLatestFileInfo(LogWriterStatus status, string searchPattern)
    {
        ArgumentNullException.ThrowIfNull(status);

        try
        {
            var filePath = ResolveLatestFilePath(status, searchPattern);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return new LogFileInfo(EmptyText, EmptyText, EmptyText, string.Empty);
            }

            var fileInfo = new FileInfo(filePath);
            return fileInfo.Exists
                ? new LogFileInfo(
                    fileInfo.FullName,
                    FormatFileSize(fileInfo.Length),
                    fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    string.Empty)
                : new LogFileInfo(EmptyText, EmptyText, EmptyText, string.Empty);
        }
        catch (Exception ex)
        {
            return new LogFileInfo(
                UnavailableText,
                UnavailableText,
                UnavailableText,
                $"日志文件信息不可用：{ex.Message}");
        }
    }

    /// <summary>
    /// Creates and opens a log directory through the operating system shell.
    /// </summary>
    public LogDirectoryOpenResult OpenDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return new LogDirectoryOpenResult(false, "打开日志目录失败：日志目录未配置。");
        }

        try
        {
            var directoryInfo = Directory.CreateDirectory(directoryPath);
            _openDirectory(directoryInfo.FullName);
            return new LogDirectoryOpenResult(true, string.Empty);
        }
        catch (Exception ex)
        {
            return new LogDirectoryOpenResult(false, $"打开日志目录失败：{ex.Message}");
        }
    }

    private static string ResolveLatestFilePath(LogWriterStatus status, string searchPattern)
    {
        if (!string.IsNullOrWhiteSpace(status.CurrentFilePath) && File.Exists(status.CurrentFilePath))
        {
            return status.CurrentFilePath;
        }

        if (string.IsNullOrWhiteSpace(status.DirectoryPath) || !Directory.Exists(status.DirectoryPath))
        {
            return string.Empty;
        }

        return Directory
            .EnumerateFiles(status.DirectoryPath, string.IsNullOrWhiteSpace(searchPattern) ? "*" : searchPattern, SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(fileInfo => fileInfo.Exists)
            .OrderByDescending(fileInfo => fileInfo.LastWriteTimeUtc)
            .Select(fileInfo => fileInfo.FullName)
            .FirstOrDefault() ?? string.Empty;
    }

    private static string FormatFileSize(long byteCount)
    {
        if (byteCount < 1024)
        {
            return $"{byteCount} B";
        }

        string[] units = ["KB", "MB", "GB"];
        var size = (double)byteCount;
        var unitIndex = -1;
        do
        {
            size /= 1024d;
            unitIndex++;
        }
        while (size >= 1024d && unitIndex < units.Length - 1);

        return $"{size:0.#} {units[unitIndex]}";
    }

    private static void OpenDirectoryWithShell(string directoryPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = directoryPath,
            UseShellExecute = true
        });
    }
}
