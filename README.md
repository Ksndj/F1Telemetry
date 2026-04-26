# F1Telemetry

当前版本：`v1.0.2`

F1Telemetry 是一个基于 .NET 10 的 Windows WPF 遥测桌面应用，用于接收、解析和展示 F1 25 UDP 遥测数据，并为实时分析、AI 建议、Windows TTS 播报、SQLite 持久化和后续复盘能力提供基础。

## 当前进度

- 已完成解决方案骨架与 WPF + MVVM 主界面。
- 已完成 F1 25 UDP 接收、packet header 分发和主要 packet DTO/parser。
- 已完成中央状态仓库、实时聚合、实时主界面、单圈聚合与最近 12 圈历史表。
- 已完成事件检测、DeepSeek AI 分析、Windows TTS 播报和本地 `settings.json` 配置。
- 已完成 SQLite 持久化层和主界面 4 张真实数据图表。
- 当前目标框架已升级到 `.NET 10`，SDK 由 `global.json` 锁定到 `10.0.201` 并允许滚动到兼容的更新特性版本。

## 模块职责

- `F1Telemetry.App`
  - WPF 桌面入口、Composition Root、主界面 ViewModel、图表适配层和控件。
- `F1Telemetry.Core`
  - 共享接口、基础模型和跨模块抽象。
- `F1Telemetry.Udp`
  - UDP 监听、packet header 解析、packetId 分发和 F1 25 协议 DTO/parser。
- `F1Telemetry.Analytics`
  - 中央状态仓库、单圈聚合、事件检测和面向 UI/AI 的分析结果。
- `F1Telemetry.AI`
  - DeepSeek OpenAI 兼容接口接入、Prompt 构造、AI 配置和结果解析。
- `F1Telemetry.TTS`
  - Windows SpeechSynthesizer 播报、单消费者队列、优先级、去重和冷却。
- `F1Telemetry.Storage`
  - SQLite 数据库初始化、Repository 和后台持久化协调。
- `F1Telemetry.Tests`
  - UDP、分析、AI、TTS、Storage、图表和 UI 支撑逻辑测试。

## 本地配置

运行时配置文件位于：

```text
%AppData%\F1Telemetry\settings.json
```

当前包含：

- `ai`: `apiKey`、`baseUrl`、`model`、`enabled`、`requestTimeoutSeconds`
- `tts`: `enabled`、`voiceName`、`volume`、`rate`、`cooldownSeconds`

API Key 在 Windows 上通过 DPAPI 保护后写入配置文件；日志和界面只显示“已配置”或脱敏状态。

SQLite 数据库默认位于：

```text
%AppData%\F1Telemetry\f1telemetry.db
```

日志目录位于：

```text
%AppData%\F1Telemetry\logs
```

## 构建方式

前置要求：

- Windows
- .NET SDK `10.0.201` 或兼容的 .NET 10 SDK
- .NET 10 Windows Desktop Runtime

建议在仓库根目录执行：

```powershell
$env:DOTNET_ROOT = "C:\Program Files\dotnet"
$env:DOTNET_ROOT_X86 = "C:\Program Files\dotnet"
$env:PATH = "C:\Program Files\dotnet;$env:PATH"
$env:DOTNET_CLI_HOME = "$PWD\.dotnet-cli-home"
$env:NUGET_PACKAGES = "$PWD\.nuget-packages"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:MSBUILDUSESERVER = "0"

dotnet restore .\F1Telemetry.sln -m:1 -p:MSBuildEnableWorkloadResolver=false
dotnet build .\F1Telemetry.sln -m:1 -p:MSBuildEnableWorkloadResolver=false --no-restore
dotnet test .\F1Telemetry.Tests\F1Telemetry.Tests.csproj -m:1 -p:MSBuildEnableWorkloadResolver=false --no-restore
```

如果当前网络无法访问 NuGet 漏洞源，可能出现 `NU1900` 警告；这不等同于编译失败，但需要在网络恢复后重新执行漏洞检查。

## 运行方式

构建成功后可运行：

```powershell
dotnet run --project .\F1Telemetry.App\F1Telemetry.App.csproj
```

应用启动后可在主界面配置 UDP 监听、AI 设置和 TTS 设置。图表数据来自 `LapAnalyzer` 当前圈采样和 `LapSummary` 历史，不直接绑定原始 UDP packet。

## 发布与更新

开发者发布流程：

1. 修改集中版本号，目前版本为 `1.0.2`。
2. 在仓库根目录运行：

   ```powershell
   build/publish.ps1
   ```

3. 使用 Inno Setup 编译：

   ```text
   build/F1Telemetry.iss
   ```

4. 生成安装包：

   ```text
   build/output/F1Telemetry-1.0.2-win-x64-setup.exe
   ```

5. 将安装包上传到 GitHub Releases。

用户更新流程：

1. 打开软件。
2. 点击“下载最新版本”。
3. 浏览器进入 GitHub Releases。
4. 下载新版安装包。
5. 关闭当前程序。
6. 直接运行安装包覆盖安装。
7. 用户数据会保留。

用户数据目录：

```text
%AppData%\F1Telemetry
```

覆盖安装不会删除以下内容：

- `settings.json`
- `f1telemetry.db`
- `logs`

卸载程序可能不会删除 AppData 中的数据；如果用户希望完全清理本地数据，需要手动删除 `%AppData%\F1Telemetry`。

## 任务规范

- 项目任务执行规范见 `TASK_SPEC.md`。
- 仓库强约束入口见 `AGENTS.md`。
- 不提交 `bin/obj`、`wpftmp`、本地数据库、API Key 或任何敏感信息。
