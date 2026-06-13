# BKKleaner — Architecture

## Overview

BKKleaner is a WPF (.NET 9) desktop application built with Clean Architecture principles,
the MVVM pattern (CommunityToolkit.Mvvm) and constructor-based dependency injection
(Microsoft.Extensions.DependencyInjection).

```
┌────────────────────────────────────────────────┐
│ Views (XAML)  — bind only, no logic            │
├────────────────────────────────────────────────┤
│ ViewModels    — UI state + commands            │
├────────────────────────────────────────────────┤
│ Services      — domain logic behind interfaces │
│  Monitoring · Optimization · Recovery ·        │
│  Cleaners · Benchmark · Updates · Webhook ·    │
│  Localization · Themes · Settings · Security   │
├────────────────────────────────────────────────┤
│ Models        — plain data types               │
└────────────────────────────────────────────────┘
```

## Composition root

`App/App.xaml.cs` builds the `ServiceProvider`. Everything is a singleton because the app
is a single-window desktop tool. `App/Program.cs` is the explicit STA entry point.

## Key design decisions

### Safe execution layer
Every privileged operation goes through `ISecurityService.ExecuteSafeAsync`, which performs:
1. a permission check (`AppPermission` — admin-only permissions are denied to non-elevated processes),
2. structured logging (start/finish/failure),
3. full exception capture — the layer never throws; callers receive `SafeResult<T>`.

### Undo-first optimization
`OptimizationService` records the previous value of every registry entry (or the previous
power scheme GUID) into a persisted undo store **before** changing it, and additionally
exports the affected registry keys as `.reg` files via `IRecoveryService`. Undo restores
recorded values; values that did not exist before are deleted again.

### Quarantine-based temp cleaning
`TempCleanerService` never deletes directly. Selected items are **moved** into a
timestamped quarantine snapshot together with a `manifest.json` mapping each item back to
its origin. Restore replays the manifest. `IsPathSafeToClean` rejects everything outside
an explicit allow-list of cleanable roots (defense against path traversal).

### Monitoring engine
`HardwareMonitoringService` wraps LibreHardwareMonitor's `Computer`, polls on a background
task (interval from settings), keeps a capped in-memory history (600 snapshots), raises
`SnapshotUpdated`/`WarningRaised` events and exports CSV. ViewModels marshal updates to
the dispatcher themselves.

### Localization
JSON dictionaries per language under `Localization/`. `Loc` is an `INotifyPropertyChanged`
singleton exposing an indexer so XAML can bind `{Binding [key], Source={x:Static loc:Loc.Instance}}`
and refresh on language change. Fallback chain: current language → English → the key itself
(recorded as a missing key).

### Theming
Theme = one `ResourceDictionary` of `SolidColorBrush` resources. `ThemeService` swaps the
merged dictionary at runtime; all styles use `DynamicResource`. Custom themes are JSON
color maps layered over the Dark base.

### webhook.txt automation
`WebhookTaskParser` (stateless, fully unit-tested) validates one JSON task per line against
a strict whitelist. `WebhookAutomationService` executes the validated tasks with permission
checks and archives the file afterwards so it cannot replay.

## Threading rules

- Services are thread-safe where they are touched from multiple threads (locks around
  history/log buffers).
- ViewModels marshal service events to the UI thread via `Dispatcher.BeginInvoke`.
- All long operations are `async` with `CancellationToken` support; the UI thread is never blocked.

## Error handling

Three global handlers in `App`: `DispatcherUnhandledException` (logged + message box,
marked handled), `AppDomain.UnhandledException` (critical log + flush) and
`TaskScheduler.UnobservedTaskException` (logged + observed).
