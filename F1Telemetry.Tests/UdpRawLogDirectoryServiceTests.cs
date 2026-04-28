using System.IO;
using F1Telemetry.App.Services;
using F1Telemetry.Core.Models;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies raw UDP log directory and file metadata helpers.
/// </summary>
public sealed class UdpRawLogDirectoryServiceTests
{
    /// <summary>
    /// Verifies missing raw log directories are surfaced as stable empty metadata.
    /// </summary>
    [Fact]
    public void GetLatestFileInfo_WhenDirectoryMissing_ReturnsEmptyMetadata()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var service = new UdpRawLogDirectoryService(_ => { });

        var fileInfo = service.GetLatestFileInfo(new UdpRawLogStatus { DirectoryPath = directoryPath });

        Assert.Equal("无", fileInfo.FilePathText);
        Assert.Equal("无", fileInfo.FileSizeText);
        Assert.Equal("无", fileInfo.LastWriteTimeText);
        Assert.Equal(string.Empty, fileInfo.ErrorMessage);
    }

    /// <summary>
    /// Verifies a missing current file falls back to the latest JSONL file in the raw log directory.
    /// </summary>
    [Fact]
    public void GetLatestFileInfo_WhenCurrentFileMissing_UsesLatestJsonlFile()
    {
        var directoryPath = CreateTempDirectory();
        var olderFile = Path.Combine(directoryPath, "older.jsonl");
        var latestFile = Path.Combine(directoryPath, "latest.jsonl");
        File.WriteAllText(olderFile, "older");
        File.WriteAllText(latestFile, "latest");
        File.SetLastWriteTimeUtc(olderFile, DateTime.UtcNow.AddMinutes(-5));
        File.SetLastWriteTimeUtc(latestFile, DateTime.UtcNow);
        var missingCurrentFile = Path.Combine(directoryPath, "missing.jsonl");
        var service = new UdpRawLogDirectoryService(_ => { });

        var fileInfo = service.GetLatestFileInfo(new UdpRawLogStatus
        {
            DirectoryPath = directoryPath,
            CurrentFilePath = missingCurrentFile
        });

        Assert.Equal(latestFile, fileInfo.FilePathText);
        Assert.NotEqual("无", fileInfo.LastWriteTimeText);
    }

    /// <summary>
    /// Verifies existing raw log files expose readable size and last-write metadata.
    /// </summary>
    [Fact]
    public void GetLatestFileInfo_WhenFileExists_FormatsSizeAndLastWriteTime()
    {
        var directoryPath = CreateTempDirectory();
        var filePath = Path.Combine(directoryPath, "sample.jsonl");
        File.WriteAllBytes(filePath, new byte[1536]);
        var lastWriteTime = new DateTime(2026, 4, 28, 9, 30, 0, DateTimeKind.Local);
        File.SetLastWriteTime(filePath, lastWriteTime);
        var service = new UdpRawLogDirectoryService(_ => { });

        var fileInfo = service.GetLatestFileInfo(new UdpRawLogStatus
        {
            DirectoryPath = directoryPath,
            CurrentFilePath = filePath
        });

        Assert.Equal(filePath, fileInfo.FilePathText);
        Assert.Equal("1.5 KB", fileInfo.FileSizeText);
        Assert.Equal(lastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"), fileInfo.LastWriteTimeText);
    }

    /// <summary>
    /// Verifies opening the raw log directory creates a missing directory before invoking the opener.
    /// </summary>
    [Fact]
    public void OpenDirectory_WhenDirectoryMissing_CreatesDirectoryAndInvokesOpener()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var openedPath = string.Empty;
        var service = new UdpRawLogDirectoryService(path => openedPath = path);

        var result = service.OpenDirectory(directoryPath);

        Assert.True(result.Succeeded);
        Assert.True(Directory.Exists(directoryPath));
        Assert.Equal(directoryPath, openedPath);
        Assert.Equal(string.Empty, result.ErrorMessage);
    }

    /// <summary>
    /// Verifies opener failures are returned as displayable errors instead of escaping.
    /// </summary>
    [Fact]
    public void OpenDirectory_WhenOpenerThrows_ReturnsFailure()
    {
        var directoryPath = CreateTempDirectory();
        var service = new UdpRawLogDirectoryService(_ => throw new InvalidOperationException("blocked"));

        var result = service.OpenDirectory(directoryPath);

        Assert.False(result.Succeeded);
        Assert.Contains("blocked", result.ErrorMessage, StringComparison.Ordinal);
    }

    private static string CreateTempDirectory()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }
}
