using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using F1Telemetry.Core.Models;

namespace F1Telemetry.App.Services;

/// <summary>
/// Represents display metadata for the most recent raw UDP log file.
/// </summary>
/// <param name="FilePathText">The file path text shown in Settings.</param>
/// <param name="FileSizeText">The formatted file size text shown in Settings.</param>
/// <param name="LastWriteTimeText">The formatted last-write time text shown in Settings.</param>
/// <param name="ErrorMessage">The metadata read error, or an empty string when no error occurred.</param>
public sealed record UdpRawLogFileInfo(
    string FilePathText,
    string FileSizeText,
    string LastWriteTimeText,
    string ErrorMessage);

/// <summary>
/// Represents the result of opening the raw UDP log directory.
/// </summary>
/// <param name="Succeeded">A value indicating whether the directory was opened.</param>
/// <param name="ErrorMessage">The displayable error text, or an empty string when the operation succeeded.</param>
public sealed record UdpRawLogDirectoryOpenResult(bool Succeeded, string ErrorMessage);

/// <summary>
/// Provides raw UDP log directory and latest-file metadata for the Settings page.
/// </summary>
public interface IUdpRawLogDirectoryService
{
    /// <summary>
    /// Gets safe display metadata for the latest raw UDP log file.
    /// </summary>
    /// <param name="status">The latest raw UDP log writer status.</param>
    /// <returns>The display metadata for Settings.</returns>
    UdpRawLogFileInfo GetLatestFileInfo(UdpRawLogStatus status);

    /// <summary>
    /// Creates and opens the raw UDP log directory through the operating system shell.
    /// </summary>
    /// <param name="directoryPath">The raw UDP log directory path.</param>
    /// <returns>The open-directory result.</returns>
    UdpRawLogDirectoryOpenResult OpenDirectory(string directoryPath);
}

/// <summary>
/// Reads raw UDP log file metadata and opens the raw log directory with Explorer.
/// </summary>
public sealed class UdpRawLogDirectoryService : IUdpRawLogDirectoryService
{
    private const string EmptyText = "无";
    private const string UnavailableText = "不可用";
    private readonly Action<string> _openDirectory;

    /// <summary>
    /// Initializes a new raw UDP log directory service.
    /// </summary>
    /// <param name="openDirectory">The optional directory opener used by tests.</param>
    public UdpRawLogDirectoryService(Action<string>? openDirectory = null)
    {
        _openDirectory = openDirectory ?? OpenDirectoryWithShell;
    }

    /// <inheritdoc />
    public UdpRawLogFileInfo GetLatestFileInfo(UdpRawLogStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        try
        {
            var filePath = ResolveLatestFilePath(status);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return new UdpRawLogFileInfo(EmptyText, EmptyText, EmptyText, string.Empty);
            }

            var fileInfo = new FileInfo(filePath);
            return fileInfo.Exists
                ? new UdpRawLogFileInfo(
                    fileInfo.FullName,
                    FormatFileSize(fileInfo.Length),
                    fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    string.Empty)
                : new UdpRawLogFileInfo(EmptyText, EmptyText, EmptyText, string.Empty);
        }
        catch (Exception ex)
        {
            return new UdpRawLogFileInfo(
                UnavailableText,
                UnavailableText,
                UnavailableText,
                $"Raw Log 文件信息不可用：{ex.Message}");
        }
    }

    /// <inheritdoc />
    public UdpRawLogDirectoryOpenResult OpenDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return new UdpRawLogDirectoryOpenResult(false, "打开日志目录失败：日志目录未配置。");
        }

        try
        {
            var directoryInfo = Directory.CreateDirectory(directoryPath);
            _openDirectory(directoryInfo.FullName);
            return new UdpRawLogDirectoryOpenResult(true, string.Empty);
        }
        catch (Exception ex)
        {
            return new UdpRawLogDirectoryOpenResult(false, $"打开日志目录失败：{ex.Message}");
        }
    }

    private static string ResolveLatestFilePath(UdpRawLogStatus status)
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
            .EnumerateFiles(status.DirectoryPath, "*.jsonl", SearchOption.TopDirectoryOnly)
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
