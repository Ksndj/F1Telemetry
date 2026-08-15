using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace F1Telemetry.Tests;

/// <summary>
/// Verifies the release-note generator against a real temporary Git repository.
/// </summary>
public sealed class ReleaseNotesScriptTests
{
    /// <summary>
    /// Verifies only commits after the previous release are rendered as user-facing changes.
    /// </summary>
    [Fact]
    public void GenerateReleaseNotes_ListsOnlyChangesAfterPreviousRelease()
    {
        var repositoryRoot = FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "build", "generate-release-notes.ps1");
        var temporaryRepository = Path.Combine(Path.GetTempPath(), $"f1telemetry-release-notes-{Guid.NewGuid():N}");

        Directory.CreateDirectory(temporaryRepository);
        try
        {
            InitializeRepository(temporaryRepository);

            Commit(temporaryRepository, 1, "feat: 初始功能");
            RunGit(temporaryRepository, "tag", "v1.0.0");
            Commit(temporaryRepository, 2, "fix: 修复遥测连接");
            Commit(temporaryRepository, 3, "feat(ui): 新增仪表盘");

            var result = RunProcess(
                "pwsh",
                "-NoProfile",
                "-File",
                scriptPath,
                "-RepositoryPath",
                temporaryRepository,
                "-PreviousTag",
                "v1.0.0",
                "-TargetSha",
                "HEAD");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("## 更新与修复", result.StandardOutput, StringComparison.Ordinal);
            Assert.Matches(@"- 修复：修复遥测连接 \(`[0-9a-f]{7,}`\)", result.StandardOutput);
            Assert.Matches(@"- 新增：新增仪表盘 \(`[0-9a-f]{7,}`\)", result.StandardOutput);
            Assert.DoesNotContain("初始功能", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(temporaryRepository);
        }
    }

    /// <summary>
    /// Verifies release notes do not silently omit changes when a release contains many commits.
    /// </summary>
    [Fact]
    public void GenerateReleaseNotes_ListsEveryChangeAfterPreviousRelease()
    {
        var repositoryRoot = FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "build", "generate-release-notes.ps1");
        var temporaryRepository = Path.Combine(Path.GetTempPath(), $"f1telemetry-release-notes-{Guid.NewGuid():N}");

        Directory.CreateDirectory(temporaryRepository);
        try
        {
            InitializeRepository(temporaryRepository);
            Commit(temporaryRepository, 0, "feat: 初始功能");
            RunGit(temporaryRepository, "tag", "v1.0.0");
            for (var sequence = 1; sequence <= 55; sequence++)
            {
                Commit(temporaryRepository, sequence, $"fix: 修复问题 {sequence}");
            }

            var result = RunProcess(
                "pwsh",
                "-NoProfile",
                "-File",
                scriptPath,
                "-RepositoryPath",
                temporaryRepository,
                "-PreviousTag",
                "v1.0.0",
                "-TargetSha",
                "HEAD");

            Assert.Equal(0, result.ExitCode);
            var changeCount = result.StandardOutput
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Count(line => line.StartsWith("- ", StringComparison.Ordinal));
            Assert.Equal(55, changeCount);
            Assert.Contains("修复问题 1", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("修复问题 55", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(temporaryRepository);
        }
    }

    private static void InitializeRepository(string repositoryPath)
    {
        RunGit(repositoryPath, "init");
        RunGit(repositoryPath, "config", "user.email", "release-tests@example.invalid");
        RunGit(repositoryPath, "config", "user.name", "Release Tests");
        RunGit(repositoryPath, "config", "commit.gpgsign", "false");
        RunGit(repositoryPath, "config", "tag.gpgsign", "false");
    }

    private static void Commit(string repositoryPath, int sequence, string message)
    {
        File.WriteAllText(
            Path.Combine(repositoryPath, "change.txt"),
            sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Encoding.UTF8);
        RunGit(repositoryPath, "add", "change.txt");
        RunGit(repositoryPath, "commit", "-m", message);
    }

    private static void RunGit(string repositoryPath, params string[] arguments)
    {
        var commandArguments = new string[arguments.Length + 2];
        commandArguments[0] = "-C";
        commandArguments[1] = repositoryPath;
        Array.Copy(arguments, 0, commandArguments, 2, arguments.Length);

        var result = RunProcess("git", commandArguments);
        Assert.True(
            result.ExitCode == 0,
            $"Git command failed with exit code {result.ExitCode}: {result.StandardError}");
    }

    private static ProcessResult RunProcess(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start process '{fileName}'.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private static void TryDeleteDirectory(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(entry, FileAttributes.Normal);
                }

                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException) when (attempt < 2)
            {
                Thread.Sleep(50);
            }
        }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
