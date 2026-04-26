# F1Telemetry UDP Raw Log 离线索引

## 用途

这份文档是 `f1telemetry-udp-20260426-123424-session-unknown.jsonl` 的离线索引，用来指导后续 V1.1.1 校准时从原始 JSONL 定向读取数据。

本文件只保存统计、字段范围和查询入口，不保存原始 `payloadBase64`。如果某个字段没有出现在本索引里，应回到原始 JSONL 按 `sessionUid`、`packetId`、`timestampUtc`、`frameIdentifier` 过滤读取。

## 原始样本

- 原始文件：`C:\Users\10670\AppData\Roaming\F1Telemetry\.logs\udp\f1telemetry-udp-20260426-123424-session-unknown.jsonl`
- 文件大小：1,596,574,478 bytes
- JSONL 行数：955,942
- JSON 解析失败：0
- `payloadBase64` 解码长度匹配 `length`：955,942
- `payloadBase64` 解码异常或长度不匹配：0
- source endpoint：`127.0.0.1:52102`
- packetFormat：2025
- gameYear：25
- packetVersion：1

## 重要结论

- 这不是完整正赛样本，而是两段有效练习赛 session。
- 赛道为 `trackId 17`，按当前项目映射应为奥地利。
- 两段有效 session 的 `sessionType` 分别为 `1` 和 `2`，都解析为 `Practice`。
- 文件中包含 `FinalClassification` 1 包，出现在第一段 session 结束处。
- `TyreSets`、`CarStatus`、`CarDamage`、`LapData`、`CarTelemetry`、`Event` 包量充足，可用于后续定向校准。
- 由于不是正赛，本样本不能最终验证正赛进站窗口、undercut / overcut、正赛 AI/TTS 策略提示。

## Session 索引

| sessionUid | 类型 | trackId | 时间范围 UTC | sessionTime 范围 | frameIdentifier 范围 | 包数 | playerCarIndex |
| --- | --- | ---: | --- | --- | --- | ---: | --- |
| `15880635741706431815` | Practice (`sessionType=1`) | 17 | 2026-04-26T12:36:48.5186241Z 至 2026-04-26T13:09:20.3600725Z | 0.0 至 3601.26 | 0 至 86468 | 518,779 | 19 |
| `10166511407230582708` | Practice (`sessionType=2`) | 17 | 2026-04-26T13:09:29.7166550Z 至 2026-04-26T13:33:12.6730131Z | 0.0 至 1485.264 | 0 至 72812 | 436,883 | 19 |
| `0` | 初始/过渡数据 | - | 2026-04-26T12:34:24.1491265Z 至 2026-04-26T13:33:12.6713801Z | 0.0 至 3601.26 | 0 至 86468 | 280 | 0 / 19 |

## Session 1：`15880635741706431815`

### Session 包抽样

| 字段 | 起始值 | 末尾值 |
| --- | ---: | ---: |
| sessionType | 1 | 1 |
| sessionMode | Practice | Practice |
| trackId | 17 | 17 |
| totalLaps | 1 | 12 |
| trackLength | 4323 | 4323 |
| weather | 0 | 0 |
| trackTemperature | 25 | 32 |
| airTemperature | 19 | 22 |
| sessionDuration | 3600 | 3600 |
| sessionTimeLeft | 3600 | 0 |
| pitSpeedLimit | 80 | 80 |
| pitStopWindowIdealLap | 0 | 0 |
| pitStopWindowLatestLap | 0 | 0 |
| safetyCarStatus | 0 | 0 |
| numSafetyCarPeriods | 0 | 0 |
| numVirtualSafetyCarPeriods | 0 | 0 |
| numRedFlagPeriods | 0 | 0 |

### Packet 分布

| packetId | 名称 | 包数 |
| ---: | --- | ---: |
| 0 | Motion | 86,400 |
| 1 | Session | 3,095 |
| 2 | LapData | 86,400 |
| 3 | Event | 1,454 |
| 4 | Participants | 313 |
| 5 | CarSetups | 3,095 |
| 6 | CarTelemetry | 86,400 |
| 7 | CarStatus | 86,400 |
| 8 | FinalClassification | 1 |
| 10 | CarDamage | 15,456 |
| 11 | SessionHistory | 30,907 |
| 12 | TyreSets | 30,909 |
| 13 | MotionEx | 86,400 |
| 15 | LapPositions | 1,549 |

### 可用于后续校准的数据

- 圈数：`LapData` 中玩家最大 `currentLapNumber=12`。
- 完成圈时间：玩家 `lastLapTimeInMs` 最大值 77,361。
- 进站状态：`pitStatus` 计数为 `0=65072`、`1=21326`、`2=2`；`numPitStopsMax=0`。
- 无效圈：`invalidSamples=0`。
- 速度/输入：`CarTelemetry` 最大速度 327；油门非零样本 55,530；刹车非零样本 11,109；DRS 样本 17,679。
- 胎龄/燃油：`CarStatus` 胎龄 0 至 6；燃油 10.227 至 20.0；剩余燃油圈 7.033 至 13.819。
- 轮胎 compound：`CarStatus` actual/visual 组合主要为 `18` 和 `16`；`TyreSets` fitted pairs 包含 `17/17`、`18/18`、`16/16`。
- 胎磨：`CarDamage` 玩家四轮磨损最大约 `[38.993, 38.199, 27.886, 33.147]`。
- 事件：`BUTN=910`、`SPTP=336`、`OVTK=201`、`FTLP=5`、`SSTA=1`、`SEND=1`。

## Session 2：`10166511407230582708`

### Session 包抽样

| 字段 | 起始值 | 末尾值 |
| --- | ---: | ---: |
| sessionType | 2 | 2 |
| sessionMode | Practice | Practice |
| trackId | 17 | 17 |
| totalLaps | 1 | 1 |
| trackLength | 4323 | 4323 |
| weather | 1 | 1 |
| trackTemperature | 32 | 31 |
| airTemperature | 22 | 21 |
| sessionDuration | 3600 | 3600 |
| sessionTimeLeft | 3600 | 2114 |
| pitSpeedLimit | 80 | 80 |
| pitStopWindowIdealLap | 0 | 0 |
| pitStopWindowLatestLap | 0 | 0 |
| safetyCarStatus | 0 | 0 |
| numSafetyCarPeriods | 0 | 0 |
| numVirtualSafetyCarPeriods | 0 | 0 |
| numRedFlagPeriods | 0 | 0 |

### Packet 分布

| packetId | 名称 | 包数 |
| ---: | --- | ---: |
| 0 | Motion | 72,767 |
| 1 | Session | 2,616 |
| 2 | LapData | 72,767 |
| 3 | Event | 970 |
| 4 | Participants | 264 |
| 5 | CarSetups | 2,616 |
| 6 | CarTelemetry | 72,767 |
| 7 | CarStatus | 72,767 |
| 10 | CarDamage | 13,061 |
| 11 | SessionHistory | 26,094 |
| 12 | TyreSets | 26,116 |
| 13 | MotionEx | 72,767 |
| 15 | LapPositions | 1,311 |

### 可用于后续校准的数据

- 圈数：`LapData` 中玩家最大 `currentLapNumber=10`。
- 完成圈时间：玩家 `lastLapTimeInMs` 最大值 74,717。
- 进站状态：`pitStatus` 计数为 `0=54186`、`1=18579`、`2=2`；`numPitStopsMax=0`。
- 无效圈：`invalidSamples=0`。
- 速度/输入：`CarTelemetry` 最大速度 323；油门非零样本 46,284；刹车非零样本 9,714；DRS 样本 14,165。
- 胎龄/燃油：`CarStatus` 胎龄 0 至 9；燃油 7.495 至 20.0；剩余燃油圈 5.136 至 13.819。
- 轮胎 compound：`CarStatus` actual/visual 组合主要为 `17` 和 `16`；`TyreSets` fitted pairs 包含 `16/16`、`17/17`、`18/18`。
- 胎磨：`CarDamage` 玩家四轮磨损最大约 `[44.856, 49.055, 29.199, 37.228]`。
- 事件：`BUTN=657`、`OVTK=191`、`SPTP=114`、`FTLP=4`、`COLL=2`、`SSTA=1`、`SEND=1`。

## PacketId 到 V1.1.1 任务的查询表

| 后续任务 | 优先 packetId | 说明 |
| --- | --- | --- |
| 赛道 / 赛制 / 天气 / 总时长 | 1 `Session` | 解码 `trackId`、`sessionType`、weather、sessionTimeLeft、pit window 字段。 |
| 当前圈速度图 | 6 `CarTelemetry` + 2 `LapData` | `CarTelemetry.Speed` 配合 `LapData.CurrentLapTimeInMs`。 |
| 当前圈油门/刹车图 | 6 `CarTelemetry` + 2 `LapData` | `Throttle`、`Brake` 有大量非零样本。 |
| 多圈燃油趋势 | 7 `CarStatus` + 11 `SessionHistory` | `FuelInTank`、`FuelRemainingLaps` 与历史圈数据联动。 |
| 四轮胎磨趋势 | 10 `CarDamage` + 11 `SessionHistory` | 玩家四轮 `TyreWear` 有非零变化。 |
| 轮胎映射校准 | 12 `TyreSets` + 7 `CarStatus` | 用 `visualCompound` 优先校准，actual dry `16-22` 仍不要硬猜。 |
| 胎龄 | 7 `CarStatus` | `TyresAgeLaps` 已覆盖 0 至 9。 |
| 进站状态 | 2 `LapData` | `PitStatus` 有 0/1/2，`NumPitStops` 本样本为 0。 |
| 事件摘要降噪 | 3 `Event` | `BUTN` 和 `SPTP` 数量大，适合验证原始事件降噪；`OVTK`、`FTLP`、`COLL` 可用于真实事件文本。 |
| 完整 session 结束 | 8 `FinalClassification` | 仅第一段 session 有 1 包。 |

## 限制

- 样本是练习赛，不是完整正赛；不能替代正赛完整采集。
- 样本没有实际进站次数增加，不能最终验证“已进站 x 次”。
- 样本没有黄旗、安全车、红旗等高优先级事件，不能最终验证安全事件 TTS 优先级。
- 样本中 `actualCompound` 与 `visualCompound` 大量相同，但这不代表 F1 25 所有赛制和轮胎分配下都稳定相同；actual dry compound 仍需保持安全兜底。
- `weather`、`pitStatus`、`resultStatus` 等原始枚举值在本索引中暂未语义化，后续任务需要结合 parser 和 UI formatter 再判断。

## 建议下一步

- 做图表验收时，先用两段 `Practice` session 分别验证“刚进赛道出点”“完成圈后趋势出现”“重开 session 不残留”。
- 做轮胎映射时，按 `sessionUid + packetId=12/7` 定向解码，不扫描其它包。
- 做事件摘要时，按 `packetId=3` 提取事件代码时间线，重点检查 `BUTN`、`SPTP` 是否继续降噪。
- 做正赛策略和 AI/TTS 验收前，仍需要再采一份完整正赛 raw log。
