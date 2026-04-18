# 里程碑 7 设计：DeepSeek 接入

## 目标

在不引入 TTS、SQLite 和原始高频遥测输入的前提下，为 `F1Telemetry` 增加最小可用的 DeepSeek 分析能力。系统在玩家闭圈后自动发起一次 AI 分析，请求使用 DeepSeek OpenAI 兼容 `chat/completions` 接口，结果以固定 JSON 返回，并显示在现有 WPF 主界面的 AI 日志区域。

本次只覆盖：

- `F1Telemetry.AI` 中的客户端、Prompt 构造、结果模型和分析服务
- `F1Telemetry.App` 中的 AI 设置项与闭圈后自动触发
- 本地 JSON 配置文件持久化
- 基础单元测试

本次不覆盖：

- TTS 播报
- SQLite 持久化
- 原始 UDP 包直接输入模型
- 多触发策略或复杂调度

## 约束

- 使用 DeepSeek OpenAI 兼容接口，`BaseUrl`、`ApiKey`、`Model` 可配置，默认模型为 `deepseek-chat`
- 模型输入只允许使用：
  - 最新一圈 `LapSummary`
  - `Best LapSummary`
  - 最近若干圈趋势
  - 当前燃油、轮胎、ERS、前后车差距
  - 最近事件列表
- `PromptBuilder` 必须要求固定 JSON 输出，至少包含：
  - `summary`
  - `tyreAdvice`
  - `fuelAdvice`
  - `trafficAdvice`
  - `ttsText`
- 所有网络请求必须在后台线程执行，不能阻塞 UI
- `ApiKey` 在 UI 中用遮罩输入
- 日志中禁止打印完整 `ApiKey`
- AI 配置文件路径固定为 `%LocalAppData%/F1Telemetry/settings.json`
- `AIAnalysisResult` 额外包含 `IsSuccess` 和 `ErrorMessage`
- 同一闭圈只允许触发一次 AI 分析
- 请求默认超时为 10 秒，并允许由配置覆盖
- `RecentEvents` 只取最近 8 条
- `BaseUrl` 需要做规范化处理，避免尾斜杠或重复路径导致请求地址错误
- 配置项持久化到本地 JSON 文件，启动读取，修改后立即保存；文件不存在或损坏时回退默认值且不得导致程序崩溃

## 方案对比

### 方案 1：AI 模块独立服务 + App 只触发/展示（推荐）

- `F1Telemetry.AI` 负责配置、Prompt、HTTP、响应解析和失败兜底
- `F1Telemetry.App` 只负责绑定设置项、闭圈触发和展示日志
- 优点：
  - 分层清晰
  - 后续接 TTS、策略分析时可直接复用
  - 不把网络和 Prompt 逻辑塞进 ViewModel
- 缺点：
  - 这次需要补齐最小配置与上下文模型

### 方案 2：把 AI 调用直接放进 `DashboardViewModel`

- 优点：接线最少
- 缺点：
  - ViewModel 过重
  - Prompt、网络、错误处理和设置持久化混在 UI 层
  - 后续扩展成本高

### 方案 3：沿用旧 `IAiRaceEngineer` 直接扩展

- 优点：表面改动较少
- 缺点：
  - 现有接口只吃 `TelemetrySnapshot + RaceWeekendContext`
  - 与本次“只吃圈摘要/趋势/事件/当前状态”的输入要求不匹配
  - 容易把协议层输入重新耦合进 AI 层

结论：采用方案 1。

## 模块设计

### `F1Telemetry.AI`

新增或替换以下类型：

- `AIAnalysisResult`
  - 固定 JSON 输出模型
  - 字段：`IsSuccess`、`ErrorMessage`、`Summary`、`TyreAdvice`、`FuelAdvice`、`TrafficAdvice`、`TtsText`
- `AIAnalysisContext`
  - AI 输入上下文
  - 仅包含：
    - `LatestLap`
    - `BestLap`
    - `RecentLaps`
    - `CurrentFuelLapsRemaining`
    - `CurrentFuelInTank`
    - `CurrentErsStoreEnergy`
    - `CurrentTyre`
    - `CurrentTyreAgeLaps`
    - `GapToFrontInMs`
    - `GapToBehindInMs`
    - `RecentEvents`（仅最近 8 条）
- `AISettings`
  - 配置对象
  - 字段：`ApiKey`、`BaseUrl`、`Model`、`AiEnabled`、`RequestTimeoutSeconds`
- `IAIAnalysisService`
  - 提供一次分析入口
- `DeepSeekClient`
  - 只负责 HTTP 请求/响应
  - 不依赖 UI
- `PromptBuilder`
  - 负责把 `AIAnalysisContext` 变成 system/user messages
  - 强制要求返回固定 JSON
- `AISettingsStore`
  - 负责本地 JSON 配置读写
  - 文件不存在或损坏时返回默认配置

### `F1Telemetry.App`

- `DashboardViewModel`
  - 持有当前 AI 设置
  - 持有 `IAIAnalysisService`
  - 轮询闭圈结果，当 `LastLap` 变化时触发一次分析
  - 记录最近已分析闭圈，确保同一闭圈只触发一次
  - 所有 AI 调用都走后台任务
  - 只把成功/失败摘要写入 `AiBroadcastLogs`
- `MainWindow.xaml`
  - 在底部 AI 区域上方增加最小设置面板：
    - AI 开关
    - Base URL
    - Model
    - API Key（遮罩）

## 数据流

1. UDP 包进入并完成聚合
2. `LapAnalyzer` 闭圈并更新最近一圈
3. `DashboardViewModel` 轮询发现最近一圈变化
4. 若 `AiEnabled = true` 且配置完整，则构造 `AIAnalysisContext`
   - `RecentEvents` 只取最近 8 条
5. `IAIAnalysisService.AnalyzeAsync` 在后台发起请求
6. `DeepSeekClient` 调用 `POST {BaseUrl}/chat/completions`
7. 返回内容从 `choices[0].message.content` 读取并解析成 `AIAnalysisResult`
8. 结果通过 UI 线程写入 AI 日志区

## 请求结构

使用 DeepSeek OpenAI 兼容接口：

- 路径：`/chat/completions`
- 默认 `BaseUrl`：`https://api.deepseek.com`
- 默认 `Model`：`deepseek-chat`

请求体最小结构：

```json
{
  "model": "deepseek-chat",
  "messages": [
    { "role": "system", "content": "..." },
    { "role": "user", "content": "..." }
  ],
  "response_format": { "type": "json_object" }
}
```

请求头：

- `Authorization: Bearer {ApiKey}`
- `Content-Type: application/json`

`BaseUrl` 在发请求前必须做规范化：

- 去除尾部多余斜杠
- 若调用方已把 `/chat/completions` 写进 `BaseUrl`，需要去重
- 最终统一由客户端只拼接一次 `/chat/completions`

## 响应结构

从 OpenAI 兼容响应中提取：

- `choices[0].message.content`

该字段必须是固定 JSON，反序列化到：

```json
{
  "summary": "string",
  "tyreAdvice": "string",
  "fuelAdvice": "string",
  "trafficAdvice": "string",
  "ttsText": "string"
}
```

如果响应为空、缺字段或不是合法 JSON，则返回失败结果并记录简短日志，不抛到 UI。

结果模型要求：

- 成功时：`IsSuccess = true`，`ErrorMessage = null`
- 失败时：`IsSuccess = false`，`ErrorMessage` 包含简洁错误信息

## Prompt 规则

`PromptBuilder` 需要做到：

- 明确告诉模型“只能返回 JSON，不要额外文字”
- 明确 JSON 字段名和含义
- 提供输入摘要而不是原始 UDP 或高频样本
- 限制模型以“赛道工程师”风格输出简洁、可执行建议

Prompt 中不得出现：

- 原始 UDP packet body
- 高频逐样本 telemetry 流
- API Key 或敏感配置值

## 配置持久化

存储方式：

- 本地 JSON 配置文件，路径为 `%LocalAppData%/F1Telemetry/settings.json`

行为要求：

- 启动时读取
- 修改后立即保存
- 文件不存在时返回默认值
- 文件损坏时自动回退默认值
- 读取/保存异常只写日志，不中断程序

默认配置：

- `ApiKey = ""`
- `BaseUrl = "https://api.deepseek.com"`
- `Model = "deepseek-chat"`
- `AiEnabled = false`
- `RequestTimeoutSeconds = 10`

## 失败处理

- 配置缺失：
  - 当 `AiEnabled = true` 但 `ApiKey` 为空时，不发请求，直接返回失败结果
- 网络失败：
  - 返回失败结果并写 AI 日志
- JSON 解析失败：
  - 返回失败结果并写 AI 日志
- 配置文件损坏：
  - 自动回退默认值并写系统日志

日志规则：

- 不记录完整 `ApiKey`
- 只记录“已配置/未配置”或脱敏后的状态
- 普通日志中禁止输出完整 Key

## 测试设计

至少补齐以下测试：

- `PromptBuilderTests`
  - 验证 prompt 包含固定 JSON 字段要求
  - 验证 prompt 只引用圈摘要、趋势、事件和当前状态
- `DeepSeekClient` 或 `AIAnalysisService` 测试
  - 验证 JSON 结果能正确解析
  - 验证配置缺失时返回失败结果
  - 验证 `BaseUrl` 规范化后请求地址正确
- `AISettingsStoreTests`
  - 验证文件不存在时回退默认值
  - 验证损坏 JSON 时回退默认值
  - 验证默认超时与配置覆盖行为

本次不做真实联网测试，使用可替换 `HttpMessageHandler` 的单元测试覆盖请求与解析逻辑。

## 待确认项

- AI 日志区本次先展示文本摘要，后续如需单独卡片样式可在下一里程碑扩展
