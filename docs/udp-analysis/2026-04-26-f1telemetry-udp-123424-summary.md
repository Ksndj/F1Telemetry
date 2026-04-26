# F1Telemetry UDP Raw Log 分析摘要

## 使用原则

- 本文件只保存轻量统计摘要，不保存原始 UDP payload。
- 如果后续校准需要本摘要没有的字段，应回到原始 JSONL 读取。
- 原始数据读取优先按 `sessionUid`、`packetId`、`timestampUtc`、`frameIdentifier` 缩小范围，避免一次性展开全量 1.6GB 文件。
- 需要解码 tyre compound、sessionType、进站状态、事件代码、LapSummary 稳定性时，以原始 raw log 的 `payloadBase64` 为准。

## 样本

- 原始日志：`C:\Users\10670\AppData\Roaming\F1Telemetry\.logs\udp\f1telemetry-udp-20260426-123424-session-unknown.jsonl`
- 文件大小：1,596,574,478 bytes
- JSONL 行数：955,942
- JSON 解析失败行数：0
- 记录时间范围（UTC）：2026-04-26T12:34:24.1491265Z 到 2026-04-26T13:33:12.6730131Z
- 记录时间范围（北京时间）：2026-04-26 20:34:24 到 2026-04-26 21:33:12
- source endpoint：`127.0.0.1:52102`
- packetFormat：2025
- gameYear：25
- packetVersion：1

## Payload 校验

- payloadBase64 可解码且长度匹配 `length`：955,942
- payloadBase64 异常或长度不匹配：0
- UDP 包长度最小值：45
- UDP 包长度最大值：1460
- UDP 包长度平均值：1062.94
- frameIdentifier 范围：0 到 86,468

结论：这份 raw log 的 JSONL 结构和 base64 payload 完整性正常，可作为后续 V1.1.1 离线分析样本。

## Session 分布

| sessionUid | 包数 | 备注 |
| --- | ---: | --- |
| 15880635741706431815 | 518,779 | 主要有效 session 之一 |
| 10166511407230582708 | 436,883 | 主要有效 session 之一 |
| 0 | 280 | 进入有效 session 前的初始/菜单阶段数据 |

初步判断：同一个文件里包含两个有效 `sessionUid`，可能对应一次重启 session、返回菜单后重进、或同一测试过程中产生了两段有效遥测。后续做完整正赛校准时建议按 `sessionUid` 拆分分析。

## playerCarIndex 分布

| playerCarIndex | 包数 | 备注 |
| --- | ---: | --- |
| 19 | 955,708 | 主要玩家车辆索引 |
| 0 | 234 | 初始/过渡阶段 |

## Packet 分布

| packetId | 项目枚举 | 包数 | 首次时间 UTC | 末次时间 UTC |
| ---: | --- | ---: | --- | --- |
| 0 | Motion | 159,167 | 2026-04-26T12:36:48.9134082Z | 2026-04-26T13:33:12.6725352Z |
| 1 | Session | 5,711 | 2026-04-26T12:36:48.8933282Z | 2026-04-26T13:33:12.6713942Z |
| 2 | LapData | 159,167 | 2026-04-26T12:36:48.9094864Z | 2026-04-26T13:33:12.6725209Z |
| 3 | Event | 2,664 | 2026-04-26T12:34:24.1491265Z | 2026-04-26T13:33:12.6730131Z |
| 4 | Participants | 577 | 2026-04-26T12:36:48.8971226Z | 2026-04-26T13:33:12.6718887Z |
| 5 | CarSetups | 5,711 | 2026-04-26T12:36:48.9033857Z | 2026-04-26T13:33:12.6719302Z |
| 6 | CarTelemetry | 159,167 | 2026-04-26T12:36:48.9177218Z | 2026-04-26T13:33:12.6729719Z |
| 7 | CarStatus | 159,167 | 2026-04-26T12:36:48.9193557Z | 2026-04-26T13:33:12.6730006Z |
| 8 | FinalClassification | 1 | 2026-04-26T13:09:20.3600725Z | 2026-04-26T13:09:20.3600725Z |
| 10 | CarDamage | 28,517 | 2026-04-26T12:36:48.9037057Z | 2026-04-26T13:33:12.6719465Z |
| 11 | SessionHistory | 57,041 | 2026-04-26T12:36:50.0036128Z | 2026-04-26T13:33:12.6719888Z |
| 12 | TyreSets | 57,025 | 2026-04-26T12:36:48.9070813Z | 2026-04-26T13:33:12.6720202Z |
| 13 | MotionEx | 159,167 | 2026-04-26T12:36:48.9172995Z | 2026-04-26T13:33:12.6725484Z |
| 15 | LapPositions | 2,860 | 2026-04-26T12:36:48.9091379Z | 2026-04-26T13:33:12.6725010Z |

未出现的 packetId：9 `LobbyInfo`，14 `TimeTrial`。

## 对 V1.1.1 校准的意义

- 图表链路：`LapData`、`CarTelemetry`、`CarStatus`、`CarDamage` 包量充足，可用于验证当前圈速度/输入、燃油趋势、胎磨趋势是否跟随真实数据刷新。
- 轮胎映射：`TyreSets` 共有 57,025 包，后续应优先用它联动 `visualCompound`、`actualCompound`、tyre set allocation、tyre age 做红黄白校准。
- session 拆分：由于存在两个有效 `sessionUid`，离线分析脚本不要只按文件名 `session-unknown` 归为一段，应按 `sessionUid` 拆分。
- 事件校准：`Event` 包 2,664 个，适合继续分析 BUTN / SPTP / SEND 等原始事件与正式比赛事件之间的降噪规则。
- 完整性：base64 解码长度全部匹配，说明 raw log writer 的写入完整性在这次样本中表现正常。

## 待进一步分析

- 解码 `Session` 包，确认两个有效 `sessionUid` 对应的赛道、赛制、天气、总圈数。
- 解码 `TyreSets`、`CarStatus`、`CarDamage`，校准 `visualCompound` 与 `actualCompound` 的真实组合。
- 按 `sessionUid` 拆分后计算每段的圈数、进站次数、胎龄变化和 LapSummary 稳定性。
- 抽取 `Event` 包文本代码，验证 Overview 摘要和 LogsView 分类降噪是否覆盖真实事件。
