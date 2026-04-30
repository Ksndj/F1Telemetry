# F1Telemetry UDP Raw Log 正赛离线索引

## 用途

这份文档是 `f1telemetry-udp-20260430-062029-session-unknown.jsonl` 的项目内索引，用来指导后续 V1.2 正赛数据分析时从原始 JSONL 定向读取数据。

本文件只保存统计、字段范围和查询入口，不保存原始 `payloadBase64`。如果某个字段没有出现在本索引里，应回到原始 JSONL 按 `sessionUid`、`packetId`、`timestampUtc`、`frameIdentifier` 过滤读取。

## 原始样本

- 原始文件：`C:\Users\10670\AppData\Roaming\F1Telemetry\.logs\udp\f1telemetry-udp-20260430-062029-session-unknown.jsonl`
- 文件大小：3,764,518,538 bytes
- JSONL 行数：2,250,838
- JSON 解析失败：0
- `payloadBase64` 解码异常或长度不匹配：0
- Header 解析失败：0
- 未知 `packetId`：0
- 当前 analyzer 不支持但已识别的 known packet：`CarSetups(packetId=5)` 共 13,022 包
- packetFormat：2025
- gameYear：25
- packetVersion：1

## 重要结论

- 这是有效正赛样本，不是练习赛 / 排位赛样本。
- 有效正赛 session 为 `17170771817128374369`，`sessionType=15`，按当前项目映射为 `Race / 正赛`。
- 赛道为 `trackId=9`，按当前项目映射为匈牙利。
- 正赛总圈数为 70 圈，玩家 `playerCarIndex=20`。
- 文件中包含 `FinalClassification` 1 包，可用于赛后摘要、完赛结果、进站次数和轮胎 stint 校准。
- `LapData`、`CarTelemetry`、`CarStatus`、`CarDamage`、`TyreSets`、`SessionHistory`、`Event` 包量充足，适合做 V1.2 正赛长样本校准。

## Session 索引

| sessionUid | 类型 | 时间范围 UTC | sessionTime 范围 | 包数 | playerCarIndex |
| --- | --- | --- | --- | ---: | --- |
| `17170771817128374369` | Race (`sessionType=15`) | 2026-04-30T06:21:13.4236697Z 至 2026-04-30T08:17:39.6610299Z | 0.0 至 6190.537 | 2,250,539 | 20 |
| `0` | 初始/过渡数据 | 2026-04-30T06:20:29.5625805Z 至 2026-04-30T08:21:30.3877941Z | 0.0 至 6190.537 | 299 | 20 / 0 |

`sessionUid=0` 主要是菜单 / 过渡期事件包，不应纳入正赛趋势、圈级分析或 AI/TTS 策略判断。

## 正赛 Session：`17170771817128374369`

### Session 包抽样

| 字段 | 起始值 | 末尾值 |
| --- | ---: | ---: |
| sessionType | 15 | 15 |
| sessionMode | Race | Race |
| trackId | 9 | 9 |
| totalLaps | 70 | 70 |
| trackLength | 4378 | 4378 |
| weather | 0 | 3 |
| trackTemperature | 41 | 30 |
| airTemperature | 27 | 24 |
| sessionDuration | 7200 | 7200 |
| sessionTimeLeft | 7200 | 100 |
| pitSpeedLimit | 80 | 80 |
| pitStopWindowIdealLap | 0 | 0 |
| pitStopWindowLatestLap | 0 | 0 |
| pitStopRejoinPosition | 0 | 21 |
| safetyCarStatus | 0 | 0 |
| numSafetyCarPeriods | 0 | 0 |
| numVirtualSafetyCarPeriods | 0 | 1 |
| numRedFlagPeriods | 0 | 0 |

### Packet 分布

| packetId | 名称 | 包数 |
| ---: | --- | ---: |
| 0 | Motion | 376,843 |
| 1 | Session | 13,022 |
| 2 | LapData | 376,843 |
| 3 | Event | 5,727 |
| 4 | Participants | 1,311 |
| 5 | CarSetups | 13,022 |
| 6 | CarTelemetry | 376,843 |
| 7 | CarStatus | 376,843 |
| 8 | FinalClassification | 1 |
| 10 | CarDamage | 65,060 |
| 11 | SessionHistory | 129,705 |
| 12 | TyreSets | 130,101 |
| 13 | MotionEx | 376,843 |
| 15 | LapPositions | 8,375 |

### 可用于后续校准的数据

- 圈数：`LapData` 中玩家最大 `currentLapNumber=68`；`FinalClassification` 中玩家完成 `numLaps=67`。
- 完赛结果：玩家结算 `position=21`，发车 `gridPosition=8`，`points=0`。
- 最快圈：玩家 `bestLapTimeInMs=84289`，即 84.289 秒。
- 进站：玩家结算 `numPitStops=4`；`LapData` 中 `numPitStops` 覆盖 0 至 4。
- 罚时：玩家结算 `penaltiesTime=40`，`numPenalties=4`；`LapData` 中最大 `penalties=40`、最大 `totalWarnings=8`。
- 轮胎 stint：玩家 `numTyreStints=5`，actual/visual 为 `17,7,17,18,7`，结束圈为 `16,19,42,60,255`。
- 胎龄 / 燃油：`CarStatus` 中玩家胎龄最大 23；燃油范围约 7.191 至 107.581。
- 胎磨：`CarDamage` / `TyreSets` 观察到玩家最大胎磨约 31.16。
- 天气：`weather` 覆盖 0、1、2、3；这份样本可用于验证正赛天气变化下 UI 和 AI 文案稳定性。
- 安全状态：`numVirtualSafetyCarPeriods=1`，`safetyCarStatus` 曾出现 2 和 3；适合后续验证 VSC / SC 类提示是否被正确归类。
- 事件：`Event` 中真实赛事事件较多，包含 `OVTK=722`、`COLL=51`、`PENA=23`、`FTLP=9`、`SCAR=5`、`RTMT=2` 等；同时 `BUTN=3322`、`SPTP=1558` 仍需降噪。

## PacketId 到 V1.2 任务的查询表

| 后续任务 | 优先 packetId | 说明 |
| --- | --- | --- |
| 正赛识别 / 赛道 / 总圈数 / 天气 | 1 `Session` | 解码 `sessionType=15`、`trackId=9`、`totalLaps=70`、weather、SC/VSC/红旗字段。 |
| 圈级正赛分析 | 2 `LapData` + 11 `SessionHistory` | 用玩家 `currentLapNumber`、`lastLapTimeInMs`、`position`、`resultStatus` 和历史圈数据建立 lap summary。 |
| 速度 / 油门 / 刹车图表 | 6 `CarTelemetry` + 2 `LapData` | `CarTelemetry` 与 `LapData` 包量一致，适合验证长时间图表稳定性。 |
| 燃油趋势 | 7 `CarStatus` + 11 `SessionHistory` | 用 `FuelInTank`、`FuelRemainingLaps` 和 lap 历史验证正赛长距离燃油走势。 |
| 胎磨 / 胎龄趋势 | 10 `CarDamage` + 7 `CarStatus` + 12 `TyreSets` | 覆盖 5 段 stint，可用于验证进站后胎龄和胎磨重置 / 变化。 |
| 进站识别 | 2 `LapData` + 8 `FinalClassification` + 12 `TyreSets` | 以 `NumPitStops=4` 和 stint 结束圈做交叉验证。 |
| 正赛结算摘要 | 8 `FinalClassification` | 提供玩家名次、完赛圈数、罚时、进站次数、轮胎 stint 和前排结果。 |
| 事件时间线 | 3 `Event` | `OVTK`、`COLL`、`PENA`、`FTLP`、`SCAR`、`RTMT` 应进入正赛关键时间线；`BUTN`、`SPTP`、`SEND` 继续作为原始噪声降权。 |
| 车阵位置 / 前后车差距 | 2 `LapData` + 15 `LapPositions` | `LapPositions` 有 8,375 包，可用于补充每圈位置变化和前后车趋势。 |

## 限制

- 文件名仍为 `session-unknown`，不能按文件名判断赛制，必须以 `Session` 包中的 `sessionType=15` 为准。
- 当前 analyzer 还没有完整 V1.2 `RaceAnalysisReport`，本索引只提供离线定位和已观察到的字段范围。
- `CarSetups(packetId=5)` 当前 analyzer 只计数，不做 typed parse。
- `sessionUid=0` 的 299 包属于过渡数据，后续统计时应过滤。
- 玩家在结算中为 P21 / 67 圈，并非 70 圈完赛冠军样本；做“完整正赛稳定性”可以用，做“胜利 / 领奖台 / 全程无落后圈”体验还需要额外样本。

## 建议下一步

- 先基于本索引实现 V1.2 的正赛报告结构，输入过滤条件固定为 `sessionUid=17170771817128374369`。
- 正赛摘要优先落地：赛道、赛制、总圈数、玩家结果、进站次数、罚时、轮胎 stint、天气变化、VSC/SC 状态。
- 事件时间线优先做降噪：排除 `BUTN` / `SPTP` / `SEND`，突出 `OVTK` / `COLL` / `PENA` / `FTLP` / `SCAR` / `RTMT`。
- 进站识别优先用 `LapData.NumPitStops`、`TyreSets` fitted stint 和 `FinalClassification` 互相校验。
