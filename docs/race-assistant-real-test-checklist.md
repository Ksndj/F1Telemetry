# RaceAssistant Real-Race Test Checklist

## Before The Race

- Enable UDP Raw Log in Settings.
- Enable App File Log in Settings.
- Enable RaceAssistant Audit Log in Settings.
- Confirm the Settings page shows the App log directory and RaceAssistant log directory.
- Confirm no API Key or Authorization value appears in any visible log.

## Test Session

- Run a 25% or 35% race.
- Ask the fixed questions during representative race phases:
  - 现在要不要进站？
  - 轮胎还能撑几圈？
  - ERS 怎么用？
  - 后车能守住吗？
  - 前车追得上吗？
  - 安全车现在要进吗？
  - 现在整体情况怎么样？

## After The Race

- Collect the UDP Raw Log JSONL.
- Collect the App Log from `%APPDATA%\F1Telemetry\.logs\app`.
- Collect the RaceAssistant JSONL from `%APPDATA%\F1Telemetry\.logs\race-assistant`.
- Keep the three files from the same `runId` together.
- Use `questionId` to match VoiceAI / RaceAssistant App log rows to RaceAssistant JSONL rows.

## Offline Comparison

- Check whether `missingData` was reasonable for the exact race phase.
- Compare strategy advice with tyre wear, gap, tyre inventory, remaining laps, and safety-car state.
- Confirm `udpRawLogFile` points to the expected raw log file name without copying UDP content.
- Confirm TTS did not抢播 critical race events.
- Confirm AI failure cases still produced a conservative fallback answer.
- Confirm every JSONL row has `schemaVersion`, `runId`, `questionId`, and an ISO 8601 timestamp with offset.
