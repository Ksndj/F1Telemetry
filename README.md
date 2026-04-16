# F1Telemetry

F1Telemetry 是一个基于 .NET 8 的 Windows WPF 解决方案骨架，用于承载 F1 25 遥测软件 V1 的桌面端、数据接入、分析、AI、语音播报与存储能力。

## 项目用途

- 提供 F1 25 遥测桌面应用的基础解决方案结构。
- 将 UDP 接入、分析、AI、TTS、存储与 UI 分层解耦。
- 为后续业务实现、测试补充与部署留出清晰扩展点。

## 模块职责

- `F1Telemetry.App`
  - WPF 桌面入口。
  - 当前包含最小可运行主窗口与 MVVM 基础视图模型。
- `F1Telemetry.Core`
  - 存放共享模型、基础接口与 MVVM 抽象。
- `F1Telemetry.Udp`
  - 负责 F1 25 UDP 遥测接入。
- `F1Telemetry.Analytics`
  - 负责遥测分析与衍生指标计算。
- `F1Telemetry.AI`
  - 负责 AI 赛道工程师建议生成。
- `F1Telemetry.TTS`
  - 负责语音播报抽象与实现占位。
- `F1Telemetry.Storage`
  - 负责遥测数据和会话持久化。
- `F1Telemetry.Tests`
  - 预留测试工程，当前为零外部依赖的占位骨架。

## 构建方式

1. 确保本机安装 .NET 8 SDK。
2. 在解决方案根目录执行：

```powershell
$env:DOTNET_CLI_HOME = "$PWD\.dotnet_cli"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:NUGET_PACKAGES = "$PWD\.nuget\packages"
C:\Users\10670\.dotnet\dotnet.exe restore .\F1Telemetry.sln --configfile .\NuGet.Config
C:\Users\10670\.dotnet\dotnet.exe build .\F1Telemetry.sln --no-restore --configfile .\NuGet.Config
```

## 当前状态

- 已完成解决方案、项目、引用关系与目录骨架。
- 已完成最小可运行 WPF 主窗口。
- 暂未加入任何 F1 25 业务协议解析或实际遥测逻辑。

## 任务规范

- 项目任务执行规范见 `TASK_SPEC.md`。
- 仓库强约束入口见 `AGENTS.md`。
