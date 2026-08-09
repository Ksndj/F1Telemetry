# F1 26 Telemetry Hardening and SQLite Dependency Design

## Goal

Complete the F1 25 2026 Season Pack telemetry adaptation by preserving newly parsed protocol data in the application state, correcting protocol semantics, making analyzer capability reporting honest, hardening count boundaries, preserving public model compatibility, and removing the vulnerable SQLite native dependency.

## Scope

This milestone covers only the findings confirmed during the F1 26 review:

1. Preserve `CarTelemetry2` in the real-time state pipeline.
2. Preserve the F1 26 Session fields, ERS harvested limit, and collision severity.
3. Correct Active Aero and DRS zone units to lap fractions.
4. Distinguish deep analyzer support from parse-and-count-only support.
5. Bound effective Active Aero and DRS zone counts.
6. Preserve `CarStatusData` source compatibility while documenting the wire-order difference.
7. Resolve `GHSA-2m69-gcr7-jv3q` by upgrading the SQLitePCLRaw bundle to 2.1.12.

The milestone does not add new WPF screens, persist collision history to SQLite, create new AI or TTS behavior, or refactor unrelated packet types.

## Architecture

The UDP project remains responsible for validating and parsing fixed wire layouts. The Core project owns immutable application-facing snapshots that do not depend on UDP packet types. Analytics projects parsed packet facts into those snapshots, applies privacy clearing, and exposes copy-safe state. The raw log analyzer reports its capability level separately from protocol parsing success.

F1 24 and F1 25 compatibility is represented with nullable F1 26 snapshots. A missing snapshot means that the protocol did not provide the data; zero remains a valid value inside a present F1 26 snapshot.

## Core State Models

Add the following public, documented immutable records under `F1Telemetry.Core/Models`:

- `LapFractionZoneSnapshot`
  - `float StartLapFraction`
  - `float EndLapFraction`
- `ActiveAeroTelemetrySnapshot`
  - the eight fields from `CarTelemetry2Data`
  - activation distance names end in `Metres`
  - a capture timestamp
- `SessionRegulations2026Snapshot`
  - Active Aero track status
  - effective full and partial Active Aero zone lists
  - effective DRS zone list
  - start reaction time
  - anti-lock brake, traction control, high-visibility racing line, colour-blind racing line, and recurring rewind settings
- `CollisionSnapshot`
  - both vehicle indexes
  - raw protocol severity byte
  - event timestamp

Extend `CarSnapshot` with nullable `ActiveAeroTelemetry` and `ErsHarvestedLimitPerLap`. Extend `SessionState` with nullable `Regulations2026` and `LastCollision`.

All list-valued state is copied when stored and when captured so later mutation of UDP arrays cannot change an already published state snapshot.

## Aggregation Flow

`StateAggregator.ApplyPacket` adds a `CarTelemetry2Packet` case. `ApplyCarTelemetry2` iterates over the packet array, follows the existing `HasTelemetryAccess` policy, and stores the complete typed telemetry snapshot for accessible cars.

`ApplyCarStatus` stores `ErsHarvestedLimitPerLap`. `CarStateStore.ClearRestrictedTelemetry` clears both new car fields so restricted opponent data follows the current conservative privacy policy.

`ApplySession` receives the packet format. For format 2026 it projects `SessionPacket` into `SessionRegulations2026Snapshot`; for earlier formats it stores `null`. Effective zone counts are bounded by all three constraints:

- the raw protocol count;
- the actual parsed array length;
- the protocol capacity, 8 for each Active Aero list and 4 for DRS.

The parser continues reading every fixed array slot so wire offsets remain unchanged. Only the state projection exposes the effective entries.

`ApplyEvent` continues storing `LastEventCode`. For `CollisionEventDetail`, it also stores `LastCollision`. A later non-collision event does not erase the most recent collision; `SessionStateStore.Reset` clears it at the session boundary.

## Protocol Semantics and Compatibility

`ActiveAeroZone` and `DRSZone` documentation is corrected to state that start and end values are fractions of lap progress in the inclusive range from 0 to 1. Parser tests use representative fractional values rather than metre-like values.

`CarStatusData.ErsHarvestedLimitPerLap` remains the final optional positional parameter. Moving it into wire order would break existing positional constructors. The parser continues reading the field at its protocol location and passes it by name. A compatibility test constructs the record using the pre-F1-26 positional argument list and verifies that the new field defaults to zero.

## Raw Log Analyzer Capability Reporting

The analyzer separates known typed packets into:

- deeply analyzed packets, whose content affects session analysis;
- count-only packets, which parse successfully and contribute packet counts but are not interpreted further.

`CarTelemetry2` is reported as count-only until the raw log analysis domain has a concrete use for its fields. Existing count-only packet types use the same classification. Truly unrecognized or unregistered packet types retain the unsupported classification.

The analysis result and human-readable report expose count-only packet IDs or counts and include a data-quality limitation. This prevents a successful parser count from being presented as complete semantic support.

## SQLite Dependency Remediation

`F1Telemetry.Storage` remains the owner of SQLite dependencies. Keep `Microsoft.Data.Sqlite` at 10.0.8 and add a direct reference to `SQLitePCLRaw.bundle_e_sqlite3` 2.1.12 without `PrivateAssets=all`.

This forces the bundle, native library, provider, and core components onto the compatible 2.1.12 line for Storage and its App and Tests consumers. It avoids mixing a 2.1.12 native library with 2.1.11 managed components and avoids the broader package changes of SQLitePCLRaw 3.x.

The security basis is:

- GitHub Advisory `GHSA-2m69-gcr7-jv3q` identifies `SQLitePCLRaw.lib.e_sqlite3 <= 2.1.11` as affected.
- NuGet publishes stable 2.1.12 packages outside that affected range.
- `SQLitePCLRaw.bundle_e_sqlite3` 2.1.12 requires the native library and provider at 2.1.12 or later.

## Testing Strategy

Follow test-driven development for each behavior. Every production change starts with a focused failing test and a recorded expected failure.

Run tests in this order, never as a full test suite:

1. `PacketParserFormat2026Tests`
   - lap-fraction test data and public model compatibility.
2. `StateAggregatorTests`
   - PacketId 16 projection for player and public opponents;
   - restricted opponent clearing;
   - ERS limit projection and clearing;
   - F1 26 Session snapshot creation;
   - effective zone count bounding;
   - defensive list copying;
   - collision snapshot lifecycle;
   - F1 25 null compatibility;
   - car index 23 coverage.
3. `RawLogAnalyzerTests`
   - CarTelemetry2 parses and is reported as count-only;
   - unsupported counts remain distinct.
4. Restore and dependency graph checks
   - no `NU1903` warning;
   - Storage, App, and Tests resolve `SQLitePCLRaw.lib.e_sqlite3` 2.1.12;
   - the SQLitePCLRaw component family does not mix 2.1.11 and 2.1.12.
5. Storage-focused tests and Storage build.
6. App build to verify Windows native SQLite asset propagation.

Build and test commands must limit MSBuild concurrency and write verbose output to `.logs/`. No full solution test run is permitted.

## Error Handling and Safety

- Fixed-size UDP validation and complete-buffer consumption remain unchanged.
- Malformed zone counts cannot make the application state expose more entries than the fixed arrays contain.
- Restricted telemetry is cleared rather than retained from an earlier accessible packet.
- Dependency restore output is inspected for vulnerability warnings before any completion claim.
- Only milestone files are staged and committed.

## Delivery

Work is performed on `fix/f1-26-hardening-and-sqlite`. The design, implementation, and verification are committed with clear milestone messages and pushed to the corresponding remote branch. No force push, history rewrite, or unrelated file inclusion is permitted.
