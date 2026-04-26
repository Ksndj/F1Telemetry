# F1Telemetry

F1Telemetry 是一个 **F1 25 Windows 遥测助手**。它接收游戏 UDP 遥测数据，把实时比赛状态整理成中文概览、图表、单圈历史、对手信息、事件日志，并可通过 DeepSeek AI 和 Windows TTS 给出短提示。

## 当前主要功能

- UDP 接收：监听 F1 25 遥测端口并解析主要比赛数据。
- 实时概览：显示赛道、赛制、轮胎、燃油、ERS、前后车差距和关键事件摘要。
- 图表：显示当前圈速度、油门/刹车、多圈燃油趋势、多圈四轮磨损趋势，并在无数据时显示空状态。
- 单圈历史：记录最近圈速、有效圈、燃油消耗和轮胎磨损摘要。
- 对手信息：展示可读化的对手状态、进站、遥测限制和差距。
- 事件日志：按 System / UDP / RaceEvent / AI / TTS / Storage 分类查看完整日志。
- AI 分析：使用 DeepSeek OpenAI compatible endpoint 生成短结论。
- Windows TTS：使用本机 Windows 语音播报关键事件和 AI 结论。
- SQLite 持久化：保存会话、单圈、事件、AI 报告和 TTS 记录。

## 运行环境

- Windows
- .NET 10 或与项目目标框架兼容的 .NET 10 SDK / Windows Desktop Runtime
- F1 25 游戏 UDP 遥测已开启

## 基本使用

1. 在 F1 25 中开启 UDP Telemetry。
2. 确认游戏 UDP 端口为 `20777`。
3. 启动 F1Telemetry。
4. 点击开始监听。
5. 进入赛道后查看实时概览、图表、单圈历史和对手列表。
6. 如需 AI 分析，在设置中配置 DeepSeek API Key。
7. 如需语音播报，启用 Windows TTS 并选择本机 Voice。

## AI 配置

- AI 接入 DeepSeek OpenAI compatible endpoint。
- API Key 只保存在本机配置中，并通过 Windows DPAPI 保护。
- 未配置 API Key 时不会调用 AI，日志页会显示“AI 未配置 API Key”。
- AI 请求失败时，日志只显示归一化错误文本，不会输出 API Key、Header 或完整异常。
- AI 结论会按当前赛制收敛为短文本，适合 TTS 播报。

## V1.1 改进摘要

- 中文赛道、赛制、轮胎显示。
- 图表空状态和真实数据接线。
- Overview 事件摘要压缩。
- LogsView 日志分类。
- 赛制专用提示：练习赛、排位赛、冲刺排位、冲刺赛、正赛、时间试跑分别使用不同关注点。
- AI/TTS 真实比赛联调：Prompt 更短、AI 错误更清晰、AI 播报更克制。

## 已知限制

- `actual dry compound` 仍需更多 F1 25 UDP 实测包或 tyre-set allocation 数据校准，未确认前不会硬猜红胎、黄胎、白胎。
- UDP raw log 记录计划放到 V1.1.1，用于完整正赛数据采集和离线分析。
- 弯角级分析属于 V3 目标，不在当前版本范围内。
