# F1Telemetry

F1Telemetry 是一个 **F1 25 Windows 遥测助手**。它接收游戏 UDP 遥测数据，把实时比赛状态整理成中文概览、AI 分析播报、单圈历史、对手信息、事件日志，并可通过 DeepSeek AI 和 Windows TTS 给出短提示。

当前开发进度：`2.0.0-beta4`，V2 比赛工程台基础功能已完成，第四版 beta 聚焦胎温 / 胎压实时接入和动态胎温 TTS 提醒。V3 功能正在 Draft PR 分支实现，不改变发布版本号。

## 当前主要功能

- UDP 接收：监听 F1 25 遥测端口并解析主要比赛数据。
- 实时概览：显示赛道、赛制、轮胎、燃油、ERS、前后车差距和关键事件摘要。
- AI 分析播报：将当前圈速度、油门/刹车、多圈燃油和四轮磨损趋势压缩进 AI 短结论，并通过 TTS 播报关键提醒。
- 单圈历史：记录当前实时会话最近圈速、有效圈、燃油消耗和轮胎磨损摘要。
- 历史会话浏览：异步加载 SQLite 中的历史 session，并用分页查看/删除历史 session 与选中 session 的单圈列表。
- 赛后复盘：从历史 laps / events / ai_reports 加载会话摘要、深色趋势图、事件时间线和 AI 每圈点评。
- V3 弯角分析：基于已持久化 lap samples 和赛道分段估算弯角速度、损失、置信度与数据质量限制。
- V3 策略时间线：从历史圈、轮胎阶段和关键事件生成 stint / 安全车 / 红旗影响时间线，作为赛后复盘补充。
- V3 工程师报告基础：使用压缩摘要生成可保存的赛后工程师报告输入，不发送原始高频 UDP、API Key 或 Header。
- 多会话对比：按同赛道筛选并分页选择 2-4 个历史 session，对比圈速、燃油和 ERS 趋势。
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
- Raw Log 与离线分析已用于实测验证，后续仍需要更多完整正赛样本校准边界场景。
- V3 弯角级分析首版使用 Estimated 赛道分段；未覆盖赛道会 fallback 并显示数据质量限制。
- V3 策略建议仍是半经验模型，缺少 pit-loss、参考圈或样本密度不足时只输出观察建议，不输出确定性指令。
- V3 Draft 当前已具备分析、仓储和报告构建基础能力；UI 首版只接入弯角分析和赛后复盘策略时间线，`StrategyAdvice` / `RaceEngineerReport` 的完整端到端入口属于后续收口项。
- V3 新表当前不做破坏性唯一约束或复杂迁移；后续需要为 `lap_samples` / `corner_summaries` 明确去重键，并为 `strategy_advices` / `race_engineer_reports` 设计保留、upsert 或清理策略。

## 版本路线图

### 总体说明

当前 F1Telemetry 已完成 V1 主链路，并在 `2.0.0-beta4` 进入 V2 比赛工程台 beta 阶段。旧版本详细规划不再展开保留，README 只记录当前仍有参考价值的边界：

```text
V1.2：正赛数据分析专项版本
V1.5：赛车部件损坏识别与 AI/TTS 工程师播报
V2：比赛工程台
V3：策略分析与弯角级驾驶分析工具
```

### V1.2：正赛数据分析专项版本（摘要）

V1.2 的重点是用完整正赛 Raw Log 打磨现有实时分析链路，验证进站、stint、胎衰、燃油、ERS、前后车差距、安全车 / 黄旗 / 红旗和 AI/TTS 提示的准确性。

V1.2 保持边界：不做完整历史会话系统、不做弯角级分析、不做复杂策略模拟。缺数据时必须显示数据不足，不能生成假结论。

### V1.5：赛车部件损坏识别与 AI/TTS 工程师播报（摘要）

V1.5 的重点是接入 F1 25 damage 数据，识别轮胎、刹车、前翼、后翼、底板、扩散器、DRS / ERS / Gearbox / Engine 等损伤，并把结果用于中文 AI/TTS 工程师提示、UI 状态和日志展示。

V1.5 保持边界：不做维修策略模拟、不做复杂概率模型、不提前实现 V2 EventBus / 历史复盘 / 多会话对比，也不做 V3 弯角级驾驶分析。

### V2：比赛工程台

#### V2 当前进度

V2-M1 到 V2-M7 已完成并进入 `2.0.0-beta4`；beta4 继续接入胎温 / 胎压实时摘要和基于赛道温度的动态胎温 TTS 提醒：

- V2-M1：EventBus 基础设施完成，`RaceEvent` 已接入兼容式同步 EventBus。
- V2-M2：TTS 播报和 AI 最近关键事件缓存已从 `IEventBus<RaceEvent>` 消费。
- V2-M3：历史 session 列表和历史单圈列表已接入 SQLite，加载为异步流程。
- V2-M4：独立“赛后复盘”页已接入历史 laps / events / ai_reports。
- V2-M5：多会话对比页已支持同赛道 2-4 个历史 session 的基础趋势对比。
- V2-M6：实时规则引擎已增加正赛 / 排位赛边界明确的建议事件，并带冷却和去重。
- V2-M7：赛后复盘 Markdown / JSON 导出完成，版本号已提升到 `2.0.0-beta3`。
- V2-M8：胎温 / 胎压实时摘要和动态胎温 TTS 提醒接入，版本号提升到 `2.0.0-beta4`。

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

V3 当前在 Draft PR 分支中实现基础能力：赛道分段、弯角指标、历史弯角分析页、stint 策略时间线、undercut / overcut 风险模型、压缩 AI 工程师报告和 SQLite 扩展表。该分支不改变发布版本号，也不包含安装包发布。

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
