[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$RepositoryPath,

    [AllowEmptyString()]
    [string]$PreviousTag = '',

    [Parameter(Mandatory)]
    [string]$TargetSha
)

$ErrorActionPreference = 'Stop'
$utf8Encoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = $utf8Encoding
$OutputEncoding = $utf8Encoding

$revisionRange = if ([string]::IsNullOrWhiteSpace($PreviousTag)) {
    $TargetSha
}
else {
    "$PreviousTag..$TargetSha"
}

$gitArguments = @(
    '-C', $RepositoryPath,
    'log',
    '--no-merges',
    '--format=%h%x09%s',
    $revisionRange
)
$gitOutput = git @gitArguments 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Could not read release changes from Git range '$revisionRange'. $($gitOutput | Out-String)"
}

$changeLines = @(
    foreach ($line in @($gitOutput)) {
        $parts = ([string]$line).Split("`t", 2)
        if ($parts.Count -ne 2) {
            continue
        }

        $shortHash = $parts[0]
        $subject = $parts[1].Trim()
        $category = '更新'
        $message = $subject

        if ($subject -match '^(?<type>feat|fix|perf|refactor|build|ci|chore|docs|test)(?:\([^)]+\))?!?:\s*(?<message>.+)$') {
            $message = $Matches.message.Trim()
            $category = switch ($Matches.type) {
                'feat' { '新增' }
                'fix' { '修复' }
                'perf' { '优化' }
                'refactor' { '重构' }
                'docs' { '文档' }
                'test' { '测试' }
                default { '调整' }
            }
        }

        "- ${category}：$message (``$shortHash``)"
    }
)

$releaseNotes = @('## 更新与修复', '')
if ($changeLines.Count -eq 0) {
    $releaseNotes += '- 本版本没有检测到新的非合并提交。'
}
else {
    $releaseNotes += $changeLines
}

$releaseNotes -join [Environment]::NewLine
