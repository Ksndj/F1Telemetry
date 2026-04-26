using System.IO;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the README stays focused on user-facing setup and current capabilities.
/// </summary>
public sealed class ReadmeUserGuideTests
{
    /// <summary>
    /// Verifies the README covers the public usage guide required by V1.1.
    /// </summary>
    [Fact]
    public void Readme_IncludesUserFacingCapabilitiesAndConfiguration()
    {
        var readme = File.ReadAllText(FindRepositoryFile("README.md"));

        foreach (var requiredText in new[]
        {
            "F1 25 Windows 遥测助手",
            "UDP 接收",
            "实时概览",
            "图表",
            "单圈历史",
            "对手信息",
            "事件日志",
            "AI 分析",
            "Windows TTS",
            "SQLite 持久化",
            "20777",
            "DeepSeek",
            "DPAPI",
            "V1.1",
            "actual dry compound",
            "V1.1.1",
            "V3"
        })
        {
            Assert.Contains(requiredText, readme, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Verifies the README does not include local paths, Codex workflow notes, or API key samples.
    /// </summary>
    [Fact]
    public void Readme_DoesNotExposeLocalOrSecretMaterial()
    {
        var readme = File.ReadAllText(FindRepositoryFile("README.md"));

        Assert.DoesNotContain("C:\\", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Codex", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sk-", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", readme, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(pathParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(pathParts)}");
    }
}
