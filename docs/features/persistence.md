---
# Base — required
feature: "Persistence"
slug: persistence
status: deployed
priority: p1
area: persistence

# Lifecycle dates
date_drafted: 2026-07-04
date_approved: 2026-07-04
date_last_revised: 2026-07-04

# Attribution
author: Erik
reviewer: Erik

# Optional — related docs
related: [docs/features/profile-management.md, docs/features/settings.md]
---

## Overview

WinAppProfiles persists all durable application state — profiles, profile
items, apply-run history, and app settings — to a single SQLite database at
`%LOCALAPPDATA%\WinAppProfiles\profiles.db`, accessed via Dapper. There is no
migrations framework: schema is created (and incrementally altered) on every
startup by running idempotent `CREATE TABLE IF NOT EXISTS` / best-effort
`ALTER TABLE` statements, not by versioned migration scripts. Two repository
classes own the schema, split along an inconsistent boundary described below.

## Capabilities

- [x] Create, update, list, and fetch profiles with their profile items round-tripped in one call (`SqliteProfileRepository`)
- [x] Delete a profile and its items in a single transaction
- [x] Persist an `ApplyResult` (profile apply run) as an `apply_runs` row plus one `apply_run_items` row per item, inside a transaction
- [x] Create the core schema (`profiles`, `profile_items`, `apply_runs`, `apply_run_items`) on first run via `DbInitializer.InitializeAsync`
- [x] Additively migrate `profile_items` with extended columns (startup delay, battery-only apply, force-minimized, custom icon path, icon index) via best-effort `ALTER TABLE` statements that swallow "duplicate column name" errors
- [x] Persist app settings as flexible key/value rows in a separate `app_settings` table, owned and created by `SqliteAppSettingsRepository` itself
- [x] Round-trip `StatusPollingIntervalSeconds` (previously a known gap; now read/written like every other setting field)

## Requirements

**Must** (required for the feature to be considered complete):
- The system must store the database at `%LOCALAPPDATA%\WinAppProfiles\profiles.db` (`App.xaml.cs` builds `dbPath` from `appData` + `"profiles.db"`)
- The system must create missing core tables on startup via `DbInitializer.InitializeAsync`, called once from `App.xaml.cs` after the DI host starts
- The system must round-trip every `ProfileItem` column that exists in the domain model, including the extended columns added after the base schema (`startup_delay_seconds`, `only_apply_on_battery`, `force_minimized_on_start`, `custom_icon_path`, `icon_index`)
- The system must wrap all multi-statement writes (`CreateProfileAsync`, `UpdateProfileAsync`, `DeleteProfileAsync`, `SaveApplyResultAsync`) in a Dapper/ADO.NET transaction and roll back on any exception
- The system must continue functioning against a database created by an older schema version, via additive `ALTER TABLE` migrations guarded by catching `SqliteException` on "duplicate column name"

**Should** (expected but not blocking):
- The system should keep `IProfileRepository` and `IAppSettingsRepository` as the only Core-facing persistence abstractions, with SQLite as an Infrastructure implementation detail
- The system should unify schema ownership under `DbInitializer` rather than letting individual repositories create their own tables

**May** (optional enhancement):
- The system may introduce a real migrations framework (e.g. versioned scripts with an applied-migrations table) if the additive `ALTER TABLE` + `PRAGMA user_version` approach becomes hard to reason about
- The system may expose the persisted apply-run history (`apply_runs` / `apply_run_items`) through a UI surface — currently write-only from the UI's perspective

## Acceptance Criteria

- [x] AC1: Given a fresh `%LOCALAPPDATA%\WinAppProfiles\profiles.db` does not exist, when the app starts, then `DbInitializer.InitializeAsync` creates `profiles`, `profile_items`, `apply_runs`, and `apply_run_items`, and `SqliteAppSettingsRepository`'s constructor creates `app_settings`
- [x] AC2: Given a profile with items is created via `CreateProfileAsync`, when `GetProfilesAsync` or `GetProfileByIdAsync` is called, then all items round-trip with matching `TargetType`, `DesiredState`, `StartupDelaySeconds`, `OnlyApplyOnBattery`, `ForceMinimizedOnStart`, `CustomIconPath`, and `IconIndex` values
- [x] AC3: Given `UpdateProfileAsync` is called on an existing profile, when the update completes, then all previous `profile_items` rows for that profile are deleted and replaced by the new item set within one transaction
- [x] AC4: Given `SaveApplyResultAsync` is called with a mix of successful and failed items, when it completes, then one `apply_runs` row (`status = SUCCESS` or `PARTIAL_FAILURE`) and one `apply_run_items` row per item are persisted, and an in-transaction read-back verifies the `apply_runs` row exists before inserting item rows
- [x] AC5: Given any exception during `CreateProfileAsync`, `UpdateProfileAsync`, `DeleteProfileAsync`, or `SaveApplyResultAsync`, when the exception is thrown, then the transaction is rolled back and no partial rows are committed
- [x] AC6: Given a database created before the extended `profile_items` columns existed, when `DbInitializer.InitializeAsync` runs against it, then the `ALTER TABLE` migrations add the missing columns without throwing, because "duplicate column name" `SqliteException`s are caught and ignored on subsequent runs
- [x] AC7: Given `StatusPollingIntervalSeconds` is changed and saved via `SettingsViewModel`, when the app restarts and settings are reloaded, then the previously-saved value is returned (fixed; `SqliteAppSettingsRepository.GetSettingsAsync`/`SaveSettingsAsync` both handle this key like every other field)

## Out of Scope

- A dedicated migrations framework with versioned/reversible scripts (current approach is additive `ALTER TABLE` + a two-step `PRAGMA user_version` gate scoped only to `app_settings` seeding)
- Any persistence backend other than SQLite (no cloud sync, no multi-machine sharing)
- A UI surface for browsing `apply_runs` / `apply_run_items` history (data is written, never read back by the UI)
- Validation of persisted values (e.g. `CustomIconPath` existence, `StartupDelaySeconds` range) — writes accept whatever the domain model holds

## Notes

- **Split schema ownership (known debt, from `docs/current-solution-analysis.md`):** `DbInitializer.InitializeAsync` owns `profiles`, `profile_items`, `apply_runs`, and `apply_run_items` (plus the additive column migrations for `profile_items`). `SqliteAppSettingsRepository`'s constructor independently creates `app_settings` and gates a two-step `PRAGMA user_version` seed (v1 seeds `DefaultInterfaceType`, v2 seeds `StateIndicatorStyle`) the first time a `SqliteAppSettingsRepository` instance is constructed. Because `IAppSettingsRepository` is registered `Scoped` in `DependencyInjection.AddWinAppProfilesInfrastructure`, this constructor — and its `PRAGMA user_version` check — runs once per resolved scope, not once per process. Two independent schema-owning code paths (one explicit `DbInitializer.InitializeAsync()` call in `App.xaml.cs`, one implicit constructor side effect) make it harder to reason about ordering and migrations as a whole; consolidating under `DbInitializer` is the recommended follow-up.
- **Dapper transaction usage in `SaveApplyResultAsync` (verified fixed in current source):** the analysis document previously flagged that the `apply_runs` insert and the `apply_run_items` inserts passed `transaction` inside the anonymous parameter object instead of as Dapper's dedicated transaction argument. Reading the current `SqliteProfileRepository.SaveApplyResultAsync` (`src/WinAppProfiles.Infrastructure/Data/SqliteProfileRepository.cs`), all three `ExecuteAsync`/`QuerySingleOrDefaultAsync` calls pass `transaction` as Dapper's explicit trailing argument, not inside the parameter object — so all writes, including the in-transaction read-back verification of the newly inserted `apply_runs.id`, are correctly enlisted in the transaction. This gap is closed; the analysis document's checklist item is marked done.
- **`StatusPollingIntervalSeconds` persistence gap: fixed.** The analysis document originally noted `AppSettings`/`SettingsViewModel` exposed this field but `SqliteAppSettingsRepository.GetSettingsAsync`/`SaveSettingsAsync` did not read/write it, so the slider changed runtime behavior but did not survive restart. Current source (`SqliteAppSettingsRepository.cs` lines 79-82 and 110) shows `StatusPollingIntervalSeconds` handled identically to every other settings field in both the read `switch` and the write list — the gap is closed.
- Identity for reads/writes: profiles and profile items are keyed by `Guid` (stored as `TEXT` via `.ToString()`/`Guid.Parse`); `DesiredState` and `TargetType` are stored as `INTEGER` enum ordinals; boolean columns (`is_default`, `is_reviewed`, `only_apply_on_battery`, `force_minimized_on_start`) are stored as `INTEGER` 0/1.
- Key types: `IProfileRepository` (Core abstraction), `SqliteProfileRepository` (Infrastructure implementation), `IAppSettingsRepository` / `SqliteAppSettingsRepository`, `DbInitializer`, `IDbConnectionFactory` / `SqliteConnectionFactory`.
- Logging: `SqliteProfileRepository` logs at `Information` for every insert/update/delete and at `Error` (with rollback) on failure, via `ILogger<SqliteProfileRepository>`.
