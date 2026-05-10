# F1Telemetry

F1Telemetry 是一个 **F1 25 Windows 遥测助手**。它接收游戏 UDP 遥测数据，把实时比赛状态整理成中文概览、AI 分析播报、单圈历史、对手信息、事件日志，并可通过 DeepSeek AI 和 Windows TTS 给出短提示。

当前开发进度：`2.0.0-beta1`，V2 比赛工程台基础功能已完成，进入 beta 验证阶段。

## 当前主要功能

- UDP 接收：监听 F1 25 遥测端口并解析主要比赛数据。
- 实时概览：显示赛道、赛制、轮胎、燃油、ERS、前后车差距和关键事件摘要。
- AI 分析播报：将当前圈速度、油门/刹车、多圈燃油和四轮磨损趋势压缩进 AI 短结论，并通过 TTS 播报关键提醒。
- 单圈历史：记录当前实时会话最近圈速、有效圈、燃油消耗和轮胎磨损摘要。
- 历史会话浏览：异步加载 SQLite 中的历史 session，并查看选中 session 的单圈列表。
- 赛后复盘：从历史 laps / events / ai_reports 加载会话摘要、趋势图、事件时间线和 AI 每圈点评。
- 多会话对比：按同赛道筛选并对比 2-4 个历史 session 的圈速、燃油和 ERS 趋势。
- 复盘报告导出：从赛后复盘页导出 Markdown / JSON 报告，不包含 API Key 或原始高频 UDP。
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
5. 进入赛道后查看实时概览、AI 分析播报、单圈历史和对手列表。
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
- 实时趋势数据转入 AI 分析播报，不再要求驾驶中查看实时图表。
- Overview 事件摘要压缩。
- LogsView 日志分类。
- 赛制专用提示：练习赛、排位赛、冲刺排位、冲刺赛、正赛、时间试跑分别使用不同关注点。
- AI/TTS 真实比赛联调：Prompt 更短、AI 错误更清晰、AI 播报更克制。

## 已知限制

- `actual dry compound` 仍需更多 F1 25 UDP 实测包或 tyre-set allocation 数据校准，未确认前不会硬猜红胎、黄胎、白胎。
- UDP raw log 记录计划放到 V1.1.1，用于完整正赛数据采集和离线分析。
- 弯角级分析属于 V3 目标，不在当前版本范围内。

## V1.2 / V1.5 / V2 / V3 后续路线图

### 总体说明

当前 F1Telemetry 已完成 V1 主链路，并通过 V1.0.2 / V1.1 / V1.1.1 逐步补齐 UI 可用性、显示语义、真实数据验收和 Raw Log 分析能力。

后续版本不应直接跳到复杂策略模拟或弯角级分析，而应按以下顺序推进：

```text
V1.2：正赛数据分析专项版本
V1.5：赛车部件损坏识别与 AI/TTS 工程师播报
V2：比赛工程台
V3：策略分析与弯角级驾驶分析工具
```

### V1.2：正赛数据分析专项版本

#### V1.2 目标

V1.2 的目标是专门针对完整正赛数据进行分析打磨。

V1.1 / V1.1.1 主要解决练习赛、排位赛、短样本 log 和基础数据可读性问题。V1.2 则要求使用一场或多场完整正赛 Raw Log，对以下能力进行验证和增强：

- 正赛长时间数据稳定性。
- 进站与换胎识别。
- Stint 分段。
- 胎龄、胎磨、圈速衰退趋势。
- 油耗趋势。
- ERS 使用趋势。
- 前后车差距变化。
- 安全车、黄旗、红旗等事件对比赛的影响。
- AI/TTS 在正赛场景下的策略提示准确性。
- 赛后正赛摘要。

V1.2 不是 V2，也不是 V3。V1.2 不做完整历史会话系统，不做弯角级分析，不做复杂策略模拟。V1.2 的重点是：基于真实正赛数据，把现有主链路分析得更准、更稳、更适合比赛使用。

#### V1.2 输入数据

V1.2 建议至少准备以下 Raw Log：

1. 一场完整短正赛，例如 25% 正赛。
2. 一场中等长度正赛，例如 35% 或 50% 正赛。
3. 至少一场包含进站的数据。
4. 至少一场包含安全车 / 黄旗 / 红旗 / 虚拟安全车的数据，如果可获得。
5. 至少一场包含明显胎衰或策略失误的数据。

如果暂时没有所有样本，V1.2 可以先从一场完整正赛开始。

Raw Log 必须能覆盖：

- Session
- LapData
- CarTelemetry
- CarStatus
- CarDamage
- SessionHistory
- TyreSets
- Event
- FinalClassification

如果某些包缺失，分析结果必须明确标记“数据不足”，不能输出假结论。

#### V1.2 核心范围

##### 1. 正赛 Raw Log 离线分析增强

现有 UDP Raw Log Analyzer 需要支持更适合正赛的分析输出。

建议输出：

```text
RaceAnalysisReport
├─ SessionSummary
├─ PlayerRaceSummary
├─ LapSummaries
├─ StintSummaries
├─ PitStopSummary
├─ TyreUsageSummary
├─ FuelTrendSummary
├─ ErsTrendSummary
├─ GapTrendSummary
├─ RaceEventTimeline
├─ AiInputPreview
└─ DataQualityWarnings
```

要求：

- 支持从 JSONL Raw Log 流式读取。
- 复用现有 UDP parser / dispatcher / analytics。
- 不复制一套独立分析逻辑。
- 能输出 Markdown 或 JSON 报告。
- 报告文件不要写入源码目录，默认写入 logs / reports / artifacts 之类的运行目录。
- 大文件读取必须流式处理，不能一次性全部加载进内存。

##### 2. 正赛 Session Summary

输出完整正赛摘要：

```text
SessionSummary
- SessionUid
- TrackId
- TrackName
- SessionType
- TotalLaps
- CompletedLaps
- RaceDistance
- Weather
- SafetyCarCount
- VscCount
- RedFlagCount
- PlayerFinalPosition
- PlayerStartPosition
- PositionGainLoss
- BestLap
- AverageLap
- ValidLapCount
- InvalidLapCount
```

要求：

- 赛道名使用中文显示。
- 赛制必须识别为正赛。
- 如果 SessionType 不是正赛，报告中明确提示“非正赛样本”。
- 不要把练习赛 / 排位赛套用正赛分析逻辑。

##### 3. Lap 级正赛分析

每圈输出：

```text
LapRaceAnalysis
- LapNumber
- LapTime
- Sector1 / Sector2 / Sector3
- IsValid
- Position
- TyreCompound
- TyreAge
- TyreWearAverage
- TyreWearFL / FR / RL / RR
- FuelRemaining
- FuelUsed
- ErsStart
- ErsEnd
- ErsUsed
- GapToFront
- GapToBehind
- PitStatus
- KeyEvents
- Notes
```

需要重点识别：

- 明显慢圈。
- 进站圈。
- 出站圈。
- 安全车影响圈。
- 黄旗影响圈。
- 红旗前后圈。
- 胎衰明显圈。
- 油耗异常圈。
- ERS 过度消耗圈。

##### 4. Stint 分段分析

V1.2 必须预留并尽量实现 Stint 概念。

Stint 划分规则：

- 换胎后开启新 stint。
- 进站后开启新 stint。
- 红旗换胎如果能识别，也开启新 stint。
- 如果无法确认换胎，只能标记“可能的新 stint”，不能强行确定。

StintSummary 字段：

```text
StintSummary
- StintIndex
- StartLap
- EndLap
- LapCount
- Compound
- StartTyreAge
- EndTyreAge
- StartWearAverage
- EndWearAverage
- WearDeltaAverage
- BestLap
- AverageLap
- MedianLap
- LapTimeDegradation
- FuelUsedTotal
- Notes
```

Stint 分析重点：

- 每段轮胎是否衰退明显。
- 新胎是否带来圈速提升。
- 旧胎是否明显撑太久。
- 进站前后圈速变化。
- 胎磨是否集中在某个轮胎。
- 胎龄和磨损是否匹配。

##### 5. 进站与换胎识别

正赛分析必须重点增强进站识别。

识别来源：

- PitStatus
- NumPitStops
- TyreCompoundActual
- TyreCompoundVisual
- TyreAgeLaps
- LapDistance 异常
- Event packet
- Lap time 明显变慢
- Pit lane 状态

输出：

```text
PitStopSummary
- PitLap
- EntryLapTime
- ExitLapTime
- CompoundBefore
- CompoundAfter
- TyreAgeBefore
- TyreAgeAfter
- PositionBefore
- PositionAfter
- PositionLost
- EstimatedPitLoss
- Confidence
- Notes
```

Confidence 示例：

- High：PitStatus + NumPitStops + TyreChanged 均匹配。
- Medium：PitStatus 或 NumPitStops 匹配，但 TyreChanged 不明确。
- Low：仅从圈速异常或位置变化推断。

要求：

- 不确定时必须输出 confidence。
- 禁止把低置信推断当成事实。
- 如果红旗导致换胎，需要和常规进站区分。

##### 6. 轮胎使用与胎衰分析

输出：

```text
TyreUsageSummary
- Compound
- TotalLaps
- BestLap
- AverageLap
- WearStart
- WearEnd
- WearDelta
- DegradationTrend
- OverheatWarnings
- Notes
```

分析重点：

- 红 / 黄 / 白 / 半雨 / 全雨各跑了多少圈。
- 每种胎平均圈速。
- 胎磨趋势。
- 轮胎是否用太久。
- 是否出现 70% 以上高磨损。
- 是否某个轮胎磨损异常偏高。
- 新胎上车后是否立刻变快。
- 旧胎末段是否明显掉速。

要求：

- visual compound 优先用于 UI 和报告显示。
- actual compound 编码可放 debug / tooltip / raw 字段。
- 16-22 这类 actual dry compound 不要硬猜成红黄白，除非项目已有可靠映射。
- Telemetry restricted 时必须标记“遥测受限”。

##### 7. 燃油趋势分析

输出：

```text
FuelTrendSummary
- StartFuel
- EndFuel
- FuelUsedTotal
- FuelUsedPerLapAverage
- FuelRemainingLapsAtFinish
- LowFuelWarnings
- FuelSavingPhaseDetected
- Notes
```

分析重点：

- 平均每圈油耗。
- 是否低油。
- 是否燃油偏保守。
- 是否需要省油。
- 安全车阶段是否明显降低油耗。
- 比赛结束时剩余油量是否过多。

单位要求：

- 必须继续遵守既有燃油字段语义。
- litres 和 kg 禁止混用。
- 字段名必须带单位。
- 如果数据来源无法确认单位，报告中必须说明。

##### 8. ERS 趋势分析

输出：

```text
ErsTrendSummary
- AverageErsStart
- AverageErsEnd
- LowErsLapCount
- HighUsageLaps
- RecoveryLaps
- AttackUsageDetected
- DefenseUsageDetected
- Notes
```

分析重点：

- 是否经常低电。
- 哪些圈 ERS 消耗过大。
- 是否在攻防窗口前没有存电。
- 安全车后是否有充电恢复。
- 排位逻辑和正赛逻辑必须区分。

##### 9. 前后车差距趋势

输出：

```text
GapTrendSummary
- GapFrontStart
- GapFrontEnd
- GapBehindStart
- GapBehindEnd
- AttackWindowLaps
- DefenseWindowLaps
- TrafficAffectedLaps
- ClearAirLaps
- Notes
```

分析重点：

- 哪些圈进入攻击窗口。
- 哪些圈进入防守窗口。
- 是否被慢车影响。
- 是否被前车 DRS 拖住。
- 是否被后车持续压迫。
- 进站前后 gap 是否变化明显。

初步规则：

- 攻击窗口：gapFront <= 1.0s。
- 防守窗口：gapBehind <= 1.0s。
- 交通风险：前车慢于自己且 gapFront <= 2.0s。

规则后续可配置。V1.2 只做基础版本。

##### 10. 正赛事件时间线

输出：

```text
RaceEventTimeline
- Lap
- Timestamp
- EventType
- Severity
- Source
- Message
- RelatedVehicle
- Confidence
```

事件类型：

- Start
- PitStop
- TyreChange
- YellowFlag
- SafetyCar
- VirtualSafetyCar
- RedFlag
- Overtake
- PositionLost
- InvalidLap
- LowFuel
- HighTyreWear
- LowErs
- AiAdvice
- TtsMessage
- FinalClassification

要求：

- Overview 仍然只显示关键摘要。
- LogsView / Report 保留完整事件。
- BUTN / SPTP / SEND 等原始 UDP 不进入正赛关键时间线。
- 原始 UDP 可保留在 UDP 分类或 debug 输出。

##### 11. 正赛 AI 输入摘要

V1.2 需要生成更适合正赛的 AI 输入摘要。

不要发送原始高频 UDP。只发送压缩后的正赛摘要。

AI 输入建议结构：

```json
{
  "session": {
    "track": "澳洲",
    "sessionType": "正赛",
    "totalLaps": 29,
    "completedLaps": 29,
    "startPosition": 4,
    "finishPosition": 10
  },
  "raceSummary": {
    "bestLapMs": 91234,
    "averageLapMs": 93500,
    "positionGainLoss": -6,
    "pitStops": 1,
    "safetyCarCount": 0,
    "redFlagCount": 1
  },
  "stints": [
    {
      "compound": "黄胎",
      "startLap": 1,
      "endLap": 23,
      "averageLapMs": 93400,
      "wearStart": 5,
      "wearEnd": 64
    },
    {
      "compound": "白胎",
      "startLap": 24,
      "endLap": 29,
      "averageLapMs": 94500,
      "wearStart": 0,
      "wearEnd": 18
    }
  ],
  "keyEvents": [
    "第24圈红旗后换白胎，其他车辆多使用红胎",
    "末段 ERS 偏低，防守能力下降"
  ],
  "questions": [
    "本场策略是否亏损？",
    "轮胎选择是否保守？",
    "下次类似红旗应如何选择轮胎？"
  ]
}
```

AI 输出要求：

- 短结论。
- 明确关键原因。
- 给下次正赛建议。
- 不要长篇泛泛分析。
- 不要编造没有数据支持的判断。
- 如果数据不足，明确说明。

##### 12. 正赛 TTS 策略

V1.2 需要预留正赛 TTS 规则优化。

正赛 TTS 优先级：

1. 安全车 / 黄旗 / 红旗。
2. 低油 / 高胎磨。
3. 前后车进入攻防窗口。
4. 进站窗口 / 换胎风险。
5. ERS 过低。
6. AI 单圈点评。

规则：

- AI 点评不能覆盖安全事件。
- 每圈 AI 最多播报 1 条。
- 同类事件需要冷却。
- 正赛中 TTS 要短，不打扰驾驶。
- 赛后报告可以详细，实时播报必须克制。

##### 13. 正赛数据质量检查

输出：

```text
DataQualityWarnings
- MissingPacketTypes
- TelemetryRestrictedCars
- IncompleteLaps
- SessionUidChanged
- ParserWarnings
- StorageWriteFailures
- AiFailures
```

要求：

- 如果某些包缺失，报告要明确。
- 如果 session uid 变化，要分段处理。
- 如果 lap 不连续，要提示。
- 如果部分对手遥测受限，不要误判。
- 如果 TyreSets 不足，不要强行判断换胎。

#### V1.2 建议里程碑

##### V1.2-M1：正赛 Raw Log 分析报告骨架

建议分支：

```text
feat/v1-2-race-log-analysis-report
```

内容：

- 扩展 RawLogAnalyzer。
- 支持输出 RaceAnalysisReport。
- 包含 SessionSummary / LapSummaries / DataQualityWarnings。
- 先输出 Markdown 或 JSON。

验收：

- 能读取完整正赛 JSONL。
- 能输出基础正赛报告。
- 不改实时 UI 主链路。

##### V1.2-M2：Stint / 进站 / 换胎识别

建议分支：

```text
feat/v1-2-stint-pit-tyre-analysis
```

内容：

- 新增 StintSummary。
- 新增 PitStopSummary。
- 根据 PitStatus / NumPitStops / TyreChanged / lap time 推断进站。
- 输出 confidence。

验收：

- 能识别至少一次常规进站。
- 不确定换胎时显示低置信，不假装确定。

##### V1.2-M3：胎衰 / 油耗 / ERS 趋势

建议分支：

```text
feat/v1-2-race-trend-analysis
```

内容：

- TyreUsageSummary。
- FuelTrendSummary。
- ErsTrendSummary。
- 识别明显胎衰、低油、低电。

验收：

- 报告能看出每段轮胎表现。
- 能看出正赛末段是否油量/电量不足。

##### V1.2-M4：前后车差距与攻防窗口

建议分支：

```text
feat/v1-2-gap-attack-defense-analysis
```

内容：

- GapTrendSummary。
- 攻击窗口、防守窗口、交通影响圈。
- 输出相关事件。

验收：

- 能标记 gapFront <= 1.0s 的攻击窗口。
- 能标记 gapBehind <= 1.0s 的防守窗口。

##### V1.2-M5：正赛事件时间线

建议分支：

```text
feat/v1-2-race-event-timeline
```

内容：

- RaceEventTimeline。
- 过滤原始 UDP 噪音。
- 保留关键比赛事件。
- 支持按 lap 排序输出。

验收：

- 正赛报告中能看到清晰事件线。
- BUTN / SPTP / SEND 不进入关键事件线。

##### V1.2-M6：正赛 AI 摘要与赛后建议

建议分支：

```text
feat/v1-2-race-ai-summary
```

内容：

- 生成正赛 AI 输入摘要。
- 支持赛后正赛建议。
- AI 输出不进入实时高频刷屏。
- 失败时有明确提示。

验收：

- AI 能基于整场正赛给短结论。
- 不发送原始高频 UDP。
- 数据不足时明确说明。

##### V1.2-M7：正赛 TTS 规则收敛

建议分支：

```text
feat/v1-2-race-tts-priority
```

内容：

- 正赛 TTS 优先级调整。
- AI 每圈最多播报 1 条。
- 安全事件优先。
- 同类事件冷却。

验收：

- 正赛中 TTS 不刷屏。
- 安全车、黄旗、低油、高胎磨优先级正确。

#### V1.2 非目标

- 不做 V2 EventBus 全面重构。
- 不做历史会话浏览 UI。
- 不做多会话对比。
- 不做弯角级分析。
- 不做赛道地图热力图。
- 不做复杂策略模拟。
- 不做自动进站策略计算器。
- 不做云同步。
- 不做 Web 多端。

### V1.5：赛车部件损坏识别与 AI/TTS 工程师播报

#### V1.5 目标

V1.5 的目标是在比赛中实时读取玩家车辆部件损坏情况，并通过 AI/TTS 给出简短、可执行的工程师提醒。

这个版本重点解决：

- 玩家是否能及时知道车辆哪里损坏。
- 损坏是否已经影响驾驶。
- 是否需要进站维修。
- 是否可以继续坚持。
- AI/TTS 是否能像工程师一样用短句提醒，而不是长篇分析。

V1.5 不做完整策略模拟，不做弯角级分析，不做 V2 事件总线重构。V1.5 只围绕 **CarDamage 数据读取、损伤事件识别、AI 判断、TTS 播报、UI/日志展示** 做增强。

#### 数据来源

主要数据来源：

```text
PacketCarDamageData / CarDamage
```

需要关注玩家车：

```text
header.playerCarIndex
```

优先读取玩家车损伤，后续可扩展对手车辆损伤，但 V1.5 默认只做玩家车。

#### 需要读取的损坏字段

根据 F1 25 UDP CarDamage 包结构，优先规划以下字段：

##### 1. 轮胎磨损与损伤

```text
TyresWear[4]
TyresDamage[4]
TyresBlisters[4]
TyresSurfaceTemperature
TyresInnerTemperature
TyresPressure
```

用途：

- 判断轮胎是否过度磨损。
- 判断是否有爆胎风险。
- 判断某一侧轮胎是否异常损伤。
- 结合现有 TyreWear 逻辑，增强高磨损提醒。

##### 2. 刹车损伤

```text
BrakesDamage[4]
```

用途：

- 判断刹车是否受损。
- 提醒刹车距离变长。
- 提醒避免强攻或晚刹。
- 高损伤时建议进站或保守驾驶。

##### 3. 前翼损伤

```text
FrontLeftWingDamage
FrontRightWingDamage
```

用途：

- 判断前翼左侧 / 右侧损伤。
- 识别轻微、中度、严重前翼损伤。
- 提醒转向不足、入弯推头、前端下压力下降。
- 严重时建议进站换前翼。

##### 4. 后翼损伤

```text
RearWingDamage
```

用途：

- 判断后翼损伤。
- 提醒高速稳定性下降。
- 提醒避免高速激进防守。
- 严重时建议进站。

##### 5. 底板与扩散器损伤

```text
FloorDamage
DiffuserDamage
SidepodDamage
```

用途：

- 判断底板 / 扩散器 / 侧箱损伤。
- 提醒整体下压力下降。
- 提醒高速弯、长弯稳定性变差。
- 中高损伤时建议保守驾驶。

##### 6. DRS / ERS / Gearbox / Engine 损伤

```text
DRSFault
ERSFault
GearBoxDamage
EngineDamage
EngineMGUHWear
EngineESWear
EngineCEWear
EngineICEWear
EngineMGUKWear
EngineTCWear
```

用途：

- 判断 DRS 是否故障。
- 判断 ERS 是否故障。
- 判断变速箱损伤。
- 判断引擎部件磨损。
- 生成系统级风险提醒。

#### 损伤等级规则

V1.5 需要建立统一损伤等级：

- 0%：无损伤。
- 1-9%：轻微损伤。
- 10-24%：轻度损伤。
- 25-49%：中度损伤。
- 50-74%：严重损伤。
- 75-100%：极严重损伤。

建议枚举：

```text
DamageSeverity
- None
- Minor
- Light
- Moderate
- Severe
- Critical
```

不同部件可以有不同阈值，但 V1.5 初期先使用统一阈值，后续再细化。

#### 损伤事件识别

新增损伤事件类型：

```text
CarDamageEvent
- Component
- Severity
- DamagePercent
- PreviousDamagePercent
- DeltaPercent
- LapNumber
- Timestamp
- RecommendedAction
- Confidence
```

需要识别：

- FrontWingDamageDetected
- RearWingDamageDetected
- FloorDamageDetected
- DiffuserDamageDetected
- SidepodDamageDetected
- BrakeDamageDetected
- TyreDamageDetected
- DrsFaultDetected
- ErsFaultDetected
- GearboxDamageDetected
- EngineDamageDetected
- CriticalDamageDetected

触发条件：

- 损伤从 0 增加到非 0。
- 损伤等级跨级，例如 Minor -> Moderate。
- 单次损伤增量过大，例如 +10%。
- 达到严重阈值，例如 >= 50%。
- 达到极严重阈值，例如 >= 75%。
- DRS / ERS 故障从 false -> true。

#### 推荐动作规则

根据损伤等级输出推荐动作：

轻微 / 轻度损伤：

- 继续驾驶，注意车辆变化。
- 示例：前翼轻微受损，注意入弯推头。

中度损伤：

- 保守驾驶，避免强攻，观察圈速损失。
- 示例：前翼中度受损，入弯会推头，先保守跑。

严重损伤：

- 建议考虑进站维修，避免继续攻防。
- 示例：前翼严重受损，建议进站换翼。

极严重损伤：

- 强烈建议进站或结束激进驾驶。
- 示例：车辆严重损坏，建议立即进站维修。

#### AI 输入摘要

不要把原始 CarDamage 包直接发送给 AI。

AI 只接收结构化摘要：

```json
{
  "session": {
    "track": "澳洲",
    "sessionType": "正赛",
    "lap": 12,
    "position": 6
  },
  "damage": {
    "frontLeftWing": 32,
    "frontRightWing": 5,
    "rearWing": 0,
    "floor": 12,
    "diffuser": 0,
    "sidepod": 0,
    "brakes": [0, 0, 0, 0],
    "tyres": [18, 20, 12, 13],
    "drsFault": false,
    "ersFault": false,
    "gearbox": 3,
    "engine": 2
  },
  "recentChange": {
    "component": "frontLeftWing",
    "previous": 0,
    "current": 32,
    "delta": 32
  },
  "raceContext": {
    "gapFrontMs": 900,
    "gapBehindMs": 700,
    "fuelLapsRemaining": 8.5,
    "tyreAge": 11
  },
  "question": "请判断损伤影响和是否需要进站，用一句中文工程师播报。"
}
```

#### AI 输出要求

AI 输出必须短，不要长篇分析。

建议输出结构：

```json
{
  "severity": "Moderate",
  "summary": "左前翼中度受损",
  "driverAdvice": "入弯会推头，先保守驾驶",
  "shouldPit": false,
  "tts": "左前翼受损，入弯会推头，先保守。"
}
```

规则：

- tts 最长 35 个中文字符。
- 不允许输出长篇解释。
- 不允许没有数据时编造损伤。
- 不确定时明确说明“数据不足”。
- AI 建议不能覆盖黄旗、安全车、低油、高胎磨等更高优先级事件。

#### TTS 播报规则

损伤播报优先级建议：

1. 极严重损伤 / 关键故障。
2. 严重前翼 / 刹车 / 轮胎 / 后翼损伤。
3. 中度前翼 / 底板 / 扩散器损伤。
4. 轻度损伤。
5. AI 普通单圈点评。

需要避免刷屏：

- 同一部件同一损伤等级 20 秒内不重复播报。
- 同一部件损伤未跨等级不重复播报。
- 轻微损伤只记录日志，不一定播报。
- 严重以上损伤必须播报。
- DRS / ERS 故障必须播报。

示例播报：

- 左前翼受损，入弯会推头。
- 前翼严重受损，建议进站换翼。
- 刹车受损，刹车点要提前。
- 底板受损，高速弯保守一点。
- DRS 故障，直道超车会困难。
- ERS 故障，暂停主动进攻。
- 轮胎损伤偏高，避免路肩。
- 车辆严重损坏，建议立即进站。

#### UI 展示建议

在 Overview 或实时概览中增加轻量损伤摘要：

```text
车辆损伤：
前翼：左 32% / 右 5%
底板：12%
扩散器：0%
侧箱：0%
刹车：0 / 0 / 0 / 0%
轮胎损伤：18 / 20 / 12 / 13%
DRS：正常
ERS：正常
```

也可以新增或扩展一个页面：

```text
DamageView
├─ 前翼 / 后翼
├─ 底板 / 扩散器 / 侧箱
├─ 四轮刹车损伤
├─ 四轮轮胎损伤
├─ DRS / ERS / 变速箱 / 引擎状态
└─ 损伤事件日志
```

V1.5 初期不强制新页面。如果 UI 改动成本高，先在 Overview + LogsView 中展示。

#### 日志分类

新增日志分类或事件类型：

```text
RaceEvent / Damage
```

日志示例：

```text
[Damage] 第 12 圈：左前翼从 0% 增加到 32%，中度损伤。
[Damage] 第 15 圈：DRS 故障。
[Damage] 第 18 圈：左前刹车损伤达到 52%，严重。
```

要求：

- 原始 UDP 不刷屏。
- Damage 事件进入 LogsView。
- Overview 只显示最近关键损伤。
- 轻微损伤可只进 Logs，不进 Overview。

#### 存储建议

如果现有 events 表足够，可以先将损伤事件写入 events。

不建议 V1.5 初期新增复杂表。

如确实需要，可以预留：

- car_damage_snapshots
- car_damage_events

但 V1.5 初期优先复用现有存储结构，避免提前扩大范围。

#### V1.5 建议里程碑

##### V1.5-M1：CarDamage 状态接入规划

建议分支：

```text
feat/v1-5-car-damage-state
```

内容：

- 确认现有 CarDamage parser 是否已解析所需字段。
- 将玩家车损伤写入状态仓库或现有 Snapshot。
- 新增 DamageSnapshot / DamageState 模型。
- 不做 AI/TTS。

验收：

- 能读取玩家车主要部件损伤。
- UI 或日志能看到基础损伤数值。
- build / test 通过。

##### V1.5-M2：损伤事件检测

建议分支：

```text
feat/v1-5-damage-event-detection
```

内容：

- 新增损伤等级判断。
- 新增损伤事件检测。
- 识别损伤新增、跨级、严重损伤、DRS/ERS 故障。
- 输出 RaceEvent / DamageEvent。

验收：

- 前翼从 0 到 30% 能生成中度损伤事件。
- 严重损伤只在跨级或首次达到时触发。
- 不重复刷屏。

##### V1.5-M3：AI 损伤摘要

建议分支：

```text
feat/v1-5-damage-ai-summary
```

内容：

- 构建 Damage AI 输入摘要。
- 不发送原始高频 UDP。
- AI 输出短 JSON 或短文本。
- 失败时有可见提示。

验收：

- AI 能判断损伤影响。
- 输出包含 tts 字段。
- tts 不超过 35 个中文字符。
- 数据不足时明确说明。

##### V1.5-M4：TTS 损伤播报

建议分支：

```text
feat/v1-5-damage-tts-alerts
```

内容：

- 将严重损伤、关键故障接入 TTS。
- 增加损伤播报优先级。
- 增加去重和冷却。
- 避免覆盖安全车 / 黄旗 / 低油等更高优先级事件。

验收：

- 严重前翼损伤能播报。
- DRS / ERS 故障能播报。
- 同一损伤不会连续重复播报。
- 安全事件优先级高于损伤 AI 点评。

##### V1.5-M5：UI 与日志展示

建议分支：

```text
feat/v1-5-damage-ui-logs
```

内容：

- Overview 显示关键损伤摘要。
- LogsView 显示 Damage 事件。
- 轻微损伤不打扰主界面。
- 如成本可控，新增 DamageView。

验收：

- 用户能看到车辆主要部件损伤。
- 关键损伤进入 Overview。
- 完整损伤记录进入 LogsView。
- 空数据时显示“等待 CarDamage 包”。

#### V1.5 非目标

- 不做 V2 EventBus 全面重构。
- 不做历史会话浏览 UI。
- 不做多会话对比。
- 不做弯角级分析。
- 不做复杂策略模拟。
- 不做自动进站策略计算器。
- 不做对手车辆损伤分析。
- 不做云同步。
- 不做 Web 多端。
- 不发送原始高频 UDP 给 AI。

#### V1.5 与其他版本边界

##### 与 V1.2 的关系

V1.2 负责完整正赛 Raw Log 与正赛报告分析。V1.5 在此基础上增加比赛中的损伤识别和工程师提醒。

##### 与 V2 的关系

V2 才负责 EventBus、历史会话、复盘工程台、多会话对比。V1.5 不提前做这些。

##### 与 V3 的关系

V3 才负责弯角级驾驶分析和复杂策略模拟。V1.5 只根据损伤数据给驾驶和进站建议，不做弯角级判断。

#### V1.5 Git 提交建议

本次只更新规划文档，建议分支：

```text
docs/add-v1-5-car-damage-ai-tts-roadmap
```

提交信息：

```text
docs: add V1.5 car damage AI TTS roadmap
```

验收标准：

- 规划文件中新增 V1.5 章节。
- V1.5 位于 V1.2 和 V2 之间。
- V1.5 明确规划 CarDamage 损伤读取、损伤事件检测、AI 判断、TTS 播报、UI/日志展示。
- 没有修改业务代码。
- 没有删除已有 V1.2 / V2 / V3 内容。
- Markdown 格式正确。

### V2：比赛工程台

#### V2 当前进度

V2-M1 到 V2-M7 已完成并进入 `2.0.0-beta1`：

- V2-M1：EventBus 基础设施完成，`RaceEvent` 已接入兼容式同步 EventBus。
- V2-M2：TTS 播报和 AI 最近关键事件缓存已从 `IEventBus<RaceEvent>` 消费。
- V2-M3：历史 session 列表和历史单圈列表已接入 SQLite，加载为异步流程。
- V2-M4：独立“赛后复盘”页已接入历史 laps / events / ai_reports。
- V2-M5：多会话对比页已支持同赛道 2-4 个历史 session 的基础趋势对比。
- V2-M6：实时规则引擎已增加正赛 / 排位赛边界明确的建议事件，并带冷却和去重。
- V2-M7：赛后复盘 Markdown / JSON 导出完成，版本号已提升到 `2.0.0-beta1`。

V2 beta 阶段剩余工作主要是实车数据验证、UI 细节打磨和发布验收；弯角级分析、完整策略模拟、赛后 AI 长报告和自动发布仍属于 V2 非目标或 V3 后续目标。

#### V2 目标

V2 的目标是把软件从“实时比赛助手”升级为“可复盘、可扩展的比赛工程台”。

V2 建立在 V1.2 正赛数据分析稳定之后，重点解决：

- 事件流解耦。
- 历史会话管理。
- 赛后复盘 UI。
- 多会话对比。
- 更系统的规则引擎。
- 为 V3 弯角级分析和策略模拟预留结构。

V2 不应该直接做弯角级分析。V2 也不应该一开始就做复杂策略模拟。V2 的核心是“工程台”和“复盘基础设施”。

#### V2 核心方向

- 引入统一 EventBus。
- 将 UI / TTS / AI / Storage 从 ViewModel 编排中逐步解耦。
- 建立历史会话浏览能力。
- 从 SQLite 读取 session / lap / events / ai_reports 做赛后复盘。
- 增强图表、趋势、对比和规则引擎。
- 为 V3 弯角级数据和策略模型预留数据结构。

#### V2 范围

##### 1. EventBus 事件总线

目标链路：

```text
Analytics / UDP / Storage
        ↓
RaceEvent / TelemetryEvent
        ↓
EventBus
   ├─ UI
   ├─ TTS
   ├─ AI
   └─ Storage
```

要求：

- 新增统一事件发布/订阅接口。
- RaceEvent 不再只服务 UI 日志。
- TTS、AI、UI、Storage 逐步从统一事件流消费。
- 保留兼容层，避免一次性重构 ViewModel。
- 先从 RaceEvent 接入，不急着把所有高频遥测数据都放进 EventBus。
- 高频遥测数据仍应走状态仓库或采样模型，不要滥用 EventBus。

##### 2. TTS / AI / UI 解耦

当前问题：

- 部分播报和 AI 输入仍依赖 ViewModel 或 UI 投影。
- 后续规则复杂后，ViewModel 容易变成调度中心。

V2 目标：

- TTS 只关心可播报事件。
- AI 只关心结构化摘要和最近关键事件。
- UI 只负责显示状态和事件。
- ViewModel 不再承担核心业务编排。
- Analytics 层输出结构化事件。
- Storage 层负责保存事件和报告。
- AI/TTS 消费事件，但不反向依赖 UI。

##### 3. 历史会话浏览

新增历史会话页。

页面建议：

```text
HistoryView
├─ Session 列表
├─ Session 概览
├─ 单圈表
├─ 事件时间线
└─ AI 报告列表
```

Session 列表字段：

- 日期
- 赛道
- 赛制
- 总圈数
- 完成圈数
- 最好圈
- 平均圈
- 起步名次
- 最终名次
- 进站次数
- 是否有 AI 报告

要求：

- 从 SQLite 加载历史 session。
- 支持选择 session 查看详情。
- 不依赖实时 UDP。
- 大 session 加载时避免 UI 卡顿。
- lap_samples 默认不一次性全部加载。

##### 4. 赛后复盘页

复盘内容：

- 圈速趋势。
- 分段趋势。
- 燃油趋势。
- 胎况 / stint 摘要；现有历史单圈未保存四轮胎磨明细时显示不可用状态。
- ERS 趋势。
- 关键事件时间线。
- AI 每圈点评。
- 进站、黄旗、安全车、圈无效等关键节点。
- Stint 摘要。
- 正赛总结。

页面建议：

```text
ReviewView
├─ Session Summary
├─ Stint Summary
├─ Lap Trend Charts
├─ Tyre / Fuel / ERS Charts
├─ Race Event Timeline
├─ AI Reports
└─ Export Report
```

要求：

- 先做基础复盘，不做复杂策略模拟。
- 优先复用现有 SQLite 数据和 V1.2 报告结构。
- 不依赖实时 UDP。
- 支持空数据状态。
- 支持导出 Markdown 或 JSON 报告。

##### 5. 多会话对比

支持同赛道不同 session 对比：

- 最佳圈速对比。
- 平均圈速对比。
- 胎磨对比不可用状态；现有历史单圈未保存四轮胎磨明细，不伪造趋势。
- 油耗趋势对比。
- ERS 趋势对比。
- 同一赛道排位赛 / 正赛表现对比。
- 不同车辆 / 不同 AI 难度 / 不同天气的基础对比，如果数据里有。

限制：

- V2 只做基础趋势对比。
- 不做弯角级损失分析。
- 不做复杂轮胎模型拟合。
- 不做策略模拟推荐。

页面建议：

```text
ComparisonView
├─ Track / Session Filter
├─ Selected Sessions
├─ Lap Time Comparison
├─ Tyre Wear Unavailable State
├─ Fuel Usage Comparison
├─ ERS Usage Comparison
└─ Summary Differences
```

##### 6. 规则引擎增强

新增或增强规则：

- 攻击窗口。
- 防守窗口。
- 前车旧胎风险。
- 后车新胎压力。
- 交通风险。
- 低油风险。
- 高胎磨风险。
- ERS 不足风险。
- 排位赛放空窗口。
- 正赛进站窗口初步提示。
- 安全车后重启提示。
- 红旗后换胎提示。

要求：

- 规则输出统一为 RaceEvent，沿用现有 EventBus 消费链路。
- 每条规则有明确冷却时间。
- 避免刷屏。
- AI/TTS 只消费筛选后的关键建议。
- 规则必须带适用赛制。
- 排位赛不得输出正赛进站策略。
- 正赛不得输出无意义的排位放空窗口提示。

##### 7. 存储层整理

优化方向：

- 根据历史会话和复盘查询路径补索引。
- 明确 session / lap / event / ai_report 的加载边界。
- 避免一次性加载大量 lap_samples。
- 为 V3 弯角级数据预留扩展表，但 V2 不实现弯角级分析。

可能新增：

- session_summaries
- stint_summaries
- event_index
- race_advice
- race_reports

是否新增表由实际实现决定。不要为了规划强行建表。如果新增表，必须提供迁移路径或兼容初始化逻辑。

##### 8. 报告导出

V2 可支持基础报告导出：

- Markdown
- JSON

报告类型：

- Session Summary Report
- Race Review Report
- AI Engineer Report Snapshot

要求：

- 不导出 API Key。
- 不导出原始高频 UDP。
- 默认导出摘要数据。

#### V2 建议里程碑

##### V2-M1：EventBus 基础设施

状态：已完成。

建议分支：

```text
feat/v2-eventbus-foundation
```

内容：

- 新增 EventBus 接口与基础实现。
- RaceEvent 接入 EventBus。
- UI / LogsView 保持现有表现。
- 增加单元测试。

验收：

- 现有 RaceEvent 可发布到 EventBus。
- 现有 UI 行为不破坏。
- build / test 通过。

##### V2-M2：TTS / AI 事件流解耦

状态：已完成。

建议分支：

```text
feat/v2-ai-tts-eventbus-integration
```

内容：

- TTS 从 EventBus 消费可播报事件。
- AI 从分析层事件缓存或 EventBus 消费关键事件。
- ViewModel 不再直接编排核心播报逻辑。
- 保留兼容逻辑，避免大范围破坏。

验收：

- TTS 播报仍正常。
- AI 输入仍包含关键事件。
- ViewModel 职责下降。

##### V2-M3：历史会话列表

状态：已完成。

建议分支：

```text
feat/v2-history-session-browser
```

内容：

- 新增 HistoryView。
- 从 SQLite 读取历史 session。
- 显示基础 session 摘要。
- 支持选择 session 查看单圈列表。

验收：

- 可查看过去 session。
- 不启动 UDP 也能打开历史数据。
- 空数据库时有明确空状态。

##### V2-M4：赛后复盘页

状态：已完成。

建议分支：

```text
feat/v2-post-race-review
```

内容：

- 新增 ReviewView 或扩展 HistoryView。
- 展示圈速、燃油、胎磨、ERS、事件时间线。
- 显示 AI 报告列表。
- 不依赖实时 UDP。

验收：

- 能打开一场历史正赛。
- 能查看关键趋势和事件线。
- UI 不明显卡顿。

##### V2-M5：多会话对比

状态：已完成。

建议分支：

```text
feat/v2-session-comparison
```

内容：

- 同赛道 session 对比。
- 圈速趋势、胎磨趋势、油耗趋势、ERS 趋势。
- 基础筛选和加载性能优化。

验收：

- 能选择至少两个 session 对比。
- 趋势图能正常显示。
- 空数据有提示。

##### V2-M6：规则引擎增强

状态：已完成。

建议分支：

```text
feat/v2-race-advice-rules
```

内容：

- 攻击窗口、防守窗口、交通风险、ERS 风险等规则。
- 输出统一 RaceAdvice / RaceEvent。
- 加冷却、优先级、去重。

验收：

- 规则输出可被 UI / TTS / AI 使用。
- 不刷屏。
- 排位赛 / 正赛规则不串场。

##### V2-M7：报告导出

状态：已完成。

建议分支：

```text
feat/v2-report-export
```

内容：

- 导出 Markdown / JSON 复盘报告。
- 支持 session summary / race review。
- 不导出敏感信息。

验收：

- 能从历史 session 导出报告。
- 报告内容可读。
- 不包含 API Key。

#### V2 非目标

- 不做弯角级分析。
- 不做赛道地图热力图。
- 不做完整策略模拟。
- 不做自动换胎策略计算器。
- 不做 Web 多端同步。
- 不做云同步。
- 不推翻现有 WPF 架构。
- 不一次性重构所有 ViewModel。
- 不把高频遥测全部塞进 EventBus。

### V3：策略分析与弯角级驾驶分析工具

#### V3 目标

V3 的目标是把软件从“比赛工程台”升级为“策略分析与弯角级驾驶分析工具”。

V3 基于 V2 的历史会话、事件流、复盘能力和 V1.2 的正赛数据分析能力，进一步分析每个弯角的驾驶表现，并提供更高级的进站策略、undercut / overcut 判断和赛后 AI 工程师报告。

V3 是长期目标，不应该在 V1.2 或 V2 之前提前实现。

#### V3 核心方向

- 建立赛道分段模型。
- 基于 lapDistance / 坐标拆分每个弯角。
- 提取弯角级驾驶指标。
- 生成弯角损失分析。
- 建立策略模拟基础模型。
- 输出结构化赛后报告。
- 增强可视化：赛道图、热力图、策略时间线。

#### V3 范围

##### 1. 赛道分段模型

建立每条赛道的 corner_map。

基础字段建议：

- TrackId
- TrackName
- SegmentId
- SegmentName
- SegmentType
- StartLapDistance
- EndLapDistance
- MainCornerNumber
- Notes

SegmentType 示例：

- Straight
- BrakingZone
- CornerEntry
- Apex
- CornerExit
- DRSZone
- PitEntry
- PitExit

要求：

- 初期可先手工维护重点赛道。
- 不要求一次性覆盖全部赛道。
- 先支持常用赛道：澳洲、上海、铃鹿、巴林、加拿大、匈牙利、奥地利、银石、斯帕、蒙扎。
- 支持未知赛道 fallback，不影响 V1/V2 功能。
- 赛道分段数据必须可维护，不要硬编码在 UI。

##### 2. 弯角级指标提取

每个弯角提取：

- 入弯速度。
- 最低速度。
- 出弯速度。
- 最大刹车。
- 刹车起点。
- 松刹点。
- 油门重踩点。
- 最小油门点。
- 方向角峰值。
- 出弯打滑迹象。
- ERS 使用。
- 弯角区段耗时。
- 相比最佳圈损失时间。

需要注意：

- 先使用现有 LapSample。
- 如果现有采样密度不足，先标记精度限制。
- 不要制造假精度。
- 不要在没有足够数据时输出确定性结论。
- 弯角级分析必须有 DataQuality / Confidence 标记。

CornerSummary 建议字段：

```text
CornerSummary
- SessionUid
- LapNumber
- TrackId
- SegmentId
- CornerName
- EntrySpeedKph
- MinimumSpeedKph
- ExitSpeedKph
- MaxBrake
- BrakeStartDistance
- ThrottleReapplyDistance
- MaxSteering
- SegmentTimeMs
- TimeLossToBestMs
- Confidence
- Notes
```

##### 3. 弯角对比视图

支持：

- 当前圈 vs 最佳圈。
- 当前圈 vs 上一圈。
- 同一 session 不同轮胎阶段。
- 同一赛道不同 session。
- 排位赛最佳圈 vs 正赛长距离圈。

页面建议：

```text
CornerAnalysisView
├─ 赛道 / Session / Lap 选择
├─ 弯角列表
├─ 每弯损失时间
├─ 速度 / 油门 / 刹车曲线
├─ 最佳圈对比
└─ AI 弯角建议
```

要求：

- 默认先显示弯角列表和损失时间。
- 曲线对比作为详情。
- 未完成 corner_map 的赛道显示“暂未支持弯角分析”。
- 数据不足时显示原因，不显示假图表。

##### 4. 策略模拟

策略分析内容：

- 轮胎衰退趋势。
- 油耗趋势。
- 前后车差距变化。
- 进站损失时间估算。
- undercut 风险。
- overcut 风险。
- 安全车窗口。
- 不同轮胎阶段收益判断。
- 红旗换胎收益判断。
- 出站交通风险。

V3 初期只做半经验模型：

- 基于历史圈速趋势。
- 基于胎龄和胎磨。
- 基于前后车差距。
- 基于进站次数。
- 基于 V1.2 正赛报告。
- 不做复杂物理仿真。

策略输出建议：

```text
StrategyAdvice
- AdviceType
- LapNumber
- Summary
- Reason
- Confidence
- RiskLevel
- RequiredData
- MissingData
```

要求：

- 所有策略建议必须带 confidence。
- 不确定时输出“建议观察”，不要输出强指令。
- 不要承诺策略一定正确。
- undercut / overcut 只能作为辅助判断。

##### 5. Stint 深度分析

基于 V1.2 StintSummary 继续扩展：

- 每段轮胎衰退曲线。
- 每段燃油修正后圈速。
- 新旧胎差距。
- 交通影响剔除。
- 安全车影响圈剔除。
- 进站前后收益。
- 红旗前后策略变化。

要求：

- V3 可以做更复杂分析，但必须保留原始指标和修正指标区分。
- 不要把修正后的推断当作原始事实。

##### 6. 赛后 AI 工程师报告

报告输入：

- session summary。
- lap summaries。
- stint summaries。
- tyre/fuel/ERS trends。
- key events。
- strategy timeline。
- corner analysis。
- data quality warnings。

报告输出：

- 比赛总结。
- 最好圈与关键圈。
- 最大时间损失来源。
- 轮胎使用评价。
- 油耗 / ERS 使用评价。
- 进站策略评价。
- 弯角级驾驶建议。
- 下一场练习重点。

要求：

- 报告以结构化 JSON 或 Markdown 保存。
- 存入 SQLite 或独立报告文件。
- UI 可查看历史报告。
- 不发送原始高频 UDP，只发送压缩摘要。
- AI 结论必须区分“数据支持”和“推断”。

##### 7. 高级可视化

可能包含：

- 赛道地图。
- 弯角损失热力图。
- 策略时间线。
- stint 图。
- 轮胎衰退图。
- 前后车差距变化图。
- ERS 使用分布图。
- 进站窗口图。
- undercut / overcut 风险图。

要求：

- 先做数据正确性，再做视觉复杂度。
- 不为了好看牺牲可维护性。
- 可视化组件继续优先使用现有图表技术栈。
- 地图和热力图如果数据不足，可以先做表格和普通折线图。

#### V3 建议里程碑

##### V3-M1：赛道分段模型

建议分支：

```text
feat/v3-corner-map-model
```

内容：

- 新增 corner_map 模型。
- 支持按 trackId 加载赛道分段。
- 先覆盖 2-3 条常用赛道。
- 未覆盖赛道有 fallback。

验收：

- 已支持至少 2 条赛道。
- 未支持赛道不影响软件运行。
- 有基础测试。

##### V3-M2：弯角指标提取

建议分支：

```text
feat/v3-corner-metrics-extraction
```

内容：

- 从 LapSample 提取每个弯角指标。
- 生成 CornerSummary。
- 增加测试样本。
- 标记采样精度限制。

验收：

- 能对一条已建模赛道生成 CornerSummary。
- 数据不足时有 warning。
- 不输出假精度。

##### V3-M3：弯角对比视图

建议分支：

```text
feat/v3-corner-analysis-view
```

内容：

- 新增 CornerAnalysisView。
- 支持选择 session / lap / corner。
- 显示当前圈 vs 最佳圈。
- 展示损失时间和关键输入差异。

验收：

- 能查看每个弯角损失。
- 能看到速度 / 油门 / 刹车差异。
- 未支持赛道有空状态。

##### V3-M4：Stint 深度分析与策略时间线

建议分支：

```text
feat/v3-stint-strategy-timeline
```

内容：

- 识别 stint。
- 展示每段轮胎、胎龄、圈速、磨损。
- 显示进站点和关键事件。
- 初步估算进站窗口。

验收：

- 正赛复盘中能看到 stint 时间线。
- 进站点和换胎信息清晰。
- 安全车 / 红旗影响被标记。

##### V3-M5：Undercut / Overcut 风险分析

建议分支：

```text
feat/v3-undercut-overcut-analysis
```

内容：

- 根据前后车差距、轮胎状态、进站损失估算风险。
- 输出结构化 StrategyAdvice。
- 只作为辅助提示，不做绝对判断。

验收：

- 建议带 confidence。
- 缺少关键数据时明确说明。
- 不输出绝对化结论。

##### V3-M6：赛后 AI 工程师报告

建议分支：

```text
feat/v3-post-race-ai-report
```

内容：

- 汇总 session / laps / events / stint / corner 数据。
- 调用 AI 生成赛后工程师报告。
- 保存报告。
- UI 支持查看历史报告。

验收：

- 报告结构清晰。
- 不发送原始高频 UDP。
- 数据不足时报告能说明限制。

##### V3-M7：高级可视化

建议分支：

```text
feat/v3-advanced-visualizations
```

内容：

- 赛道地图基础显示。
- 弯角损失热力图。
- 策略时间线图。
- ERS / 胎耗 / 燃油分布可视化。

验收：

- 至少一种高级可视化可用。
- 数据来源清晰。
- 空数据状态明确。

#### V3 非目标

- 不做真实 F1 物理级仿真。
- 不保证策略模拟 100% 准确。
- 不做自动驾驶。
- 不做游戏输入控制。
- 不做联网多人数据共享。
- 不做商业车队级复杂策略平台。
- 不上传敏感 API Key 或原始日志到云端。
- 不在数据不足时生成确定性驾驶结论。

### 阶段边界

#### V1.2 边界

V1.2 只围绕完整正赛 Raw Log 和正赛摘要分析：

- 可以增强 Raw Log Analyzer。
- 可以增强正赛报告。
- 可以增强 Stint / 进站 / 胎衰 / 油耗 / ERS / gap 分析。
- 可以增强正赛 AI 摘要。
- 可以调整正赛 TTS 优先级。

V1.2 不做：

- EventBus 全面重构。
- 历史会话 UI。
- 多会话对比。
- 弯角级分析。
- 策略模拟器。

#### V1.5 边界

V1.5 只围绕比赛中的 CarDamage 损伤识别和工程师提醒：

- 可以规划玩家车部件损伤读取。
- 可以规划损伤等级、损伤事件和推荐动作。
- 可以规划 AI 损伤摘要和短句工程师判断。
- 可以规划 TTS 损伤播报优先级、去重和冷却。
- 可以规划 Overview / LogsView / DamageView 的轻量展示。

V1.5 不做：

- V2 EventBus 全面重构。
- 历史会话 UI。
- 多会话对比。
- 对手车辆损伤分析。
- 弯角级分析。
- 复杂策略模拟。

#### V2 边界

V2 重点是工程台和复盘基础设施：

- EventBus。
- UI / AI / TTS / Storage 解耦。
- 历史会话。
- 赛后复盘。
- 多会话对比。
- 规则引擎增强。
- 报告导出。

V2 不做：

- 弯角级指标。
- 赛道地图热力图。
- undercut / overcut 高级模拟。
- AI 长报告深度驾驶分析。

#### V3 边界

V3 才开始做：

- 赛道分段模型。
- 弯角级分析。
- 策略模拟。
- undercut / overcut 风险。
- 高级可视化。
- 赛后 AI 工程师报告。

V3 必须建立在 V1.2 正赛分析和 V2 历史复盘能力之上。

### 非目标

- 不实现任何业务代码。
- 不修改 csproj、测试、安装包配置或现有运行逻辑。
- 不删除已有规划内容。
- 不把 V2/V3 当成当前开发任务。
- 不把 V1.5 扩大成 V2 复盘工程台或 V3 弯角分析。
- 不跳过 V1.2 直接做 V3。
- 不发送原始高频 UDP 给 AI。
- 不在数据不足时生成确定性驾驶结论。
- 不为了新功能重构无关模块。
- 不一次性大改 ViewModel。

### 推荐执行顺序

推荐顺序：

1. 完成 V1.1.1 Raw Log 与真实数据小修。
2. 收集至少一场完整正赛 Raw Log。
3. 执行 V1.2 正赛数据分析专项。
4. 等 V1.2 正赛报告稳定后，执行 V1.5 赛车部件损坏识别与 AI/TTS 工程师播报。
5. V1.5 损伤读取、损伤事件、AI 短判断、TTS 播报和 UI/日志展示稳定后，再进入 V2。
6. V2 完成历史复盘和事件解耦后，再进入 V3。

不要跳过 V1.2 直接做 V3。也不要把 V1.5 扩大成历史复盘、对手损伤或弯角级影响分析；因为没有完整正赛数据分析能力，V3 的策略模拟和弯角级报告容易变成假分析。

### 全局技术原则

- 不发送原始高频 UDP 给 AI。
- AI 只接收压缩摘要。
- 所有推断必须区分 confidence。
- 数据不足时必须显示 DataQualityWarnings。
- 所有燃油字段必须带单位。
- litres 和 kg 禁止混用。
- 现有 fuel_used_litres 语义不能破坏。
- Raw Log 分析必须流式读取。
- 大 session 查询必须避免 UI 卡顿。
- 空数据状态必须明确显示原因。
- 不要为了新功能重构无关模块。
- 不要一次性大改 ViewModel。
- 每个版本只解决当前阶段目标。

### Git 提交建议

本次只更新规划文档，建议分支：

```text
docs/add-v1-5-car-damage-ai-tts-roadmap
```

提交信息：

```text
docs: add V1.5 car damage AI TTS roadmap
```

本次验收标准：

- 项目规划文件中新增 V1.5 章节。
- V1.5 位于 V1.2 和 V2 之间。
- V1.5 明确规划 CarDamage 损伤读取、损伤事件检测、AI 判断、TTS 播报、UI/日志展示。
- 没有修改业务代码。
- 没有删除已有 V1.2 / V2 / V3 内容。
- Markdown 格式正确。
