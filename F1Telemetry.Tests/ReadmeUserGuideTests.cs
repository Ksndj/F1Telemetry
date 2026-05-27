using System.IO;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the README stays focused on the project introduction and current capabilities.
/// </summary>
public sealed class ReadmeUserGuideTests
{
    /// <summary>
    /// Verifies the README covers the project introduction and current capabilities.
    /// </summary>
    [Fact]
    public void Readme_IncludesProjectIntroductionAndCurrentCapabilities()
    {
        var readme = File.ReadAllText(FindRepositoryFile("README.md"));

        foreach (var requiredText in new[]
        {
            "F1 25 Windows 遥测助手",
            "项目介绍",
            "项目现有功能",
            "3.1.0",
            "3.1.0 发布说明",
            "UDP 接收",
            "实时概览",
            "AI 分析播报",
            "语音输入 AI 问答",
            "方向盘/手柄按钮",
            "Race Assistant 问工程师",
            "语音/文字策略问答",
            "App 分类日志",
            "RaceAssistant 审计 JSONL",
            "AI/TTS 播报收敛",
            "轮胎库存 UI",
            "ERS MJ 格式化",
            "弯角分析参考圈与 UI 优化",
            "单圈历史",
            "对手信息",
            "事件日志",
            "AI 分析",
            "Windows TTS",
            "SQLite 持久化",
            "DeepSeek",
            "V3"
        })
        {
            Assert.Contains(requiredText, readme, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Verifies roadmap and setup sections stay out of the compact README.
    /// </summary>
    [Fact]
    public void Readme_DoesNotIncludeRoadmapOrSetupSections()
    {
        var readme = File.ReadAllText(FindRepositoryFile("README.md"));

        Assert.DoesNotContain("版本路线图", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("建议里程碑", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("运行环境", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("基本使用", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AI 配置", readme, StringComparison.OrdinalIgnoreCase);
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
