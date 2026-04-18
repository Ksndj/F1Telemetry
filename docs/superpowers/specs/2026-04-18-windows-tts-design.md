# 里程碑 8 设计：Windows TTS 播报

## 目标

在现有 `F1Telemetry` 项目中增加最小可用的 Windows TTS 播报能力。TTS 只消费两类上游结果：

- `EventDetectionService` 产生的 `RaceEvent`
- `AIAnalysisResult.TtsText`

系统需要支持后台排队播报、去重、优先级和冷却时间控制，并将最近播报记录显示到主界面的日志区域。实现必须保持 WPF + MVVM 清晰，不阻塞 UI，也不能因为 TTS 异常导致主程序崩溃。

本次只覆盖：

- `F1Telemetry.TTS` 中的最小播报模型、队列、服务与规则
- 统一本地配置文件中的 `tts` 配置块
- `F1Telemetry.App` 中的播报接线与日志展示
- 基础单元测试

本次不覆盖：

- SQLite
- 新增更多事件类型
- 更复杂的播报策略或动态插话控制

## 约束

- 在 `F1Telemetry.TTS` 中实现：
  - `ITtsService` 或等价接口
  - `TtsQueue`
  - `TtsMessage`
  - `TtsOptions`
  - `WindowsSpeechService`
- `WindowsSpeechService` 基于 `System.Speech.Synthesis.SpeechSynthesizer`
- 播报来源只接：
  - `RaceEvent`
  - `AIAnalysisResult.TtsText`
- 最小播报规则必须支持：
  - 排队
  - 去重
  - 优先级
  - 冷却时间
- 同类消息在冷却时间内不重复播报
- TTS 设置加入本地统一配置，至少包含：
  - `TtsEnabled`
  - `VoiceName`
  - `Volume`
  - `Rate`
- 所有播报操作都在后台线程执行，不能阻塞 UI
- 异常不能导致主程序崩溃
- 日志区先统一为 `AI / TTS 日志`
- 日志来源至少区分：
  - `AI`
  - `TTS`
  - `System`

## 统一配置规则

里程碑 8 将本地配置统一为同一个文件：

- 路径：`%LocalAppData%/F1Telemetry/settings.json`

顶层结构建议为：

```json
{
  "ai": { },
  "tts": { }
}
```

约束如下：

- 读不到某个块时，只回退该块默认值，不影响另一块
- 所有保存统一走一个配置存储服务
- 每次保存都使用：
  - 读取现有文件
  - 更新对应块
  - 原子写回
- AI 和 TTS 不允许各自直接覆写整个 `settings.json`

## 方案对比

### 方案 1：底层语音服务 + 独立 TTS 队列 + App 只入队（推荐）

- `WindowsSpeechService` 只负责播放
- `TtsQueue` 负责去重、优先级、冷却和后台消费
- `App` 只把 `RaceEvent` 和 `AIAnalysisResult.TtsText` 映射成 `TtsMessage`
- 优点：
  - 规则清晰
  - 易于测试
  - UI 不承载业务逻辑
- 缺点：
  - 需要补一层最小队列抽象

### 方案 2：把规则直接塞进 `WindowsSpeechService`

- 优点：文件数更少
- 缺点：
  - 播放与调度耦合
  - 测试困难
  - 文件会快速膨胀

### 方案 3：由 `DashboardViewModel` 直接做播报调度

- 优点：实现路径最短
- 缺点：
  - 违反“不要把业务逻辑写进 ViewModel”
  - 不利于后续复用

结论：采用方案 1。

## 模块设计

### `F1Telemetry.TTS`

新增或替换以下类型：

- `TtsMessage`
  - 字段：
    - `Text`
    - `Priority`
    - `DedupKey`
    - `Cooldown`
    - `Source`
    - `CreatedAt`
- `TtsPriority`
  - 枚举：
    - `High`
    - `Normal`
    - `Low`
- `TtsOptions`
  - 字段：
    - `TtsEnabled`
    - `VoiceName`
    - `Volume`
    - `Rate`
- `ITtsService`
  - 保持底层“说一句话”职责即可
- `WindowsSpeechService`
  - 基于 `SpeechSynthesizer`
  - 只负责实际播报一条文本
  - 应用 `VoiceName / Volume / Rate`
- `TtsQueue`
  - 负责消息入队、后台消费、优先级、去重、冷却和最近播报记录
  - 对外只暴露最小入队/读取记录接口

### `F1Telemetry.App`

- `DashboardViewModel`
  - 继续做 orchestrator，不做 TTS 规则本身
  - 接收 `RaceEvent` 后入队一条 `TtsMessage`
  - 接收 `AIAnalysisResult.TtsText` 后入队一条 `TtsMessage`
  - 从 `TtsQueue` 读取最近播报记录并显示到日志区域
- `MainWindow.xaml`
  - 右下角统一为 `AI / TTS 日志`
  - 如需设置项展示，可在现有 AI 设置区并排增加最小 TTS 设置项

## 播报来源映射

### `RaceEvent -> TtsMessage`

- `Warning` 类事件优先映射为 `High`
- 普通事件映射为 `Normal`
- `DedupKey` 优先复用事件自身语义键，或以 `EventType + VehicleIdx + LapNumber` 组成稳定键

### `AIAnalysisResult.TtsText -> TtsMessage`

- 仅当 `IsSuccess = true` 且 `TtsText` 非空时入队
- 默认优先级使用 `Low`
- `DedupKey` 可由 `lapNumber + ttsText` 组成稳定键

## TtsQueue 行为

队列只维护两类状态：

- 成功入队状态
- 成功播报状态

规则如下：

- 队列内去重只看 `DedupKey`
- 冷却只看“最近一次成功播报时间”
- 若同 `DedupKey` 的消息已经在队列中，新的重复消息直接丢弃
- 若同 `DedupKey` 在冷却窗口内已经成功播报，则新的消息不入队

## 优先级规则

- `High > Normal > Low`
- 同优先级严格 FIFO
- 高优先级可以排到低优先级前面
- 但不打断当前正在播报的句子

这意味着调度是“非抢占式优先级队列”。

## 数据流

1. `EventDetectionService` 检测到新事件
2. `DashboardViewModel` 继续更新事件日志
3. 同时将符合规则的 `RaceEvent` 映射为 `TtsMessage` 并提交到 `TtsQueue`
4. 玩家闭圈后 AI 分析成功
5. `DashboardViewModel` 将 `AIAnalysisResult.TtsText` 映射为 `TtsMessage` 并提交到 `TtsQueue`
6. `TtsQueue` 在后台线程取队列头部消息
7. `WindowsSpeechService` 执行 `SpeechSynthesizer` 播报
8. 成功播报后更新冷却时间和最近播报记录
9. UI 定时读取最近播报记录，显示到 `AI / TTS` 日志区域

## 配置设计

统一配置文件顶层结构：

```json
{
  "ai": {
    "apiKey": "",
    "baseUrl": "https://api.deepseek.com",
    "model": "deepseek-chat",
    "aiEnabled": false,
    "requestTimeoutSeconds": 10
  },
  "tts": {
    "ttsEnabled": false,
    "voiceName": "",
    "volume": 100,
    "rate": 0
  }
}
```

默认值：

- `tts.ttsEnabled = false`
- `tts.voiceName = ""`
- `tts.volume = 100`
- `tts.rate = 0`

行为要求：

- 缺少 `tts` 块时，只回退 `tts` 默认值
- 缺少 `ai` 块时，只回退 `ai` 默认值
- 写回时保留另一块现有内容

## 失败处理

- `SpeechSynthesizer` 初始化失败：
  - TTS 停用
  - 写一条 `System` 日志
  - 主程序继续运行
- 单条播报异常：
  - 跳过该条
  - 写一条 `TTS` 或 `System` 日志
  - 队列继续工作
- 配置文件损坏：
  - 仅损坏块回退默认值
  - 不导致主程序崩溃

## 测试设计

至少补齐以下测试：

- `TtsQueueTests`
  - 验证队列内去重
  - 验证冷却时间
  - 验证优先级顺序
- 若需要最小辅助测试：
  - 验证高优先级不会打断当前正在播报的句子，而是影响后续出队顺序

本次不做真实语音设备集成测试，`WindowsSpeechService` 以构建级验证为主。

## 待确认项

- 右下角日志区是否继续保留现有两列布局，还是最终合并成更明确的统一日志面板；本次先以“统一命名和来源标识”为主，不做大改布局
