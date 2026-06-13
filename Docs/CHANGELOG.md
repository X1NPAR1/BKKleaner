# Changelog

## v3.7.0 — 2026-06-13

A big features-and-polish release.

### Added
- **Notifications everywhere** — every operation (RAM/temp clean, backup, optimization, profile apply, updates, auto-clean, cooling) now raises both an in-app toast (top-right) and a Windows tray notification. Toasts auto-dismiss after 5 seconds or can be closed with ✕.
- **Cool & Optimize** — new Dashboard buttons (Cool CPU/GPU, Optimize CPU/GPU) that safely lower temperature and background load **without disturbing the foreground app/game**, so there is no in-game performance loss. They trim background working sets, deprioritize background processes and purge the standby list — never touching the focused app or protected system processes.
- **Live heat coloring + escalating alerts** — CPU/GPU temperature and CPU/GPU/RAM usage now turn orange (80–90), red (90–100) and dark red (100+). Crossing the thresholds raises a throttled Windows + in-app warning.
- **Profile preview panel** — Preview now opens a dedicated overlay panel listing the exact changes, instead of squashing the card.
- **Custom profiles** — create your own profiles (name + chosen optimizations) and delete user-created ones. Built-in profiles can still be edited and reset.
- **Start with Windows** — a Settings option to launch BKKleaner at login, optionally minimized to the tray.

### Changed
- **Power plans are mutually exclusive** — selecting one plan (e.g. Ultimate) automatically turns the others off, in both the engine and the UI toggles.
- **Readable benchmark scores** — CPU single/multi-thread scores are shown in a compact, human-friendly form (e.g. `25.4M`) instead of raw digit strings.
- **Full localization pass** — benchmark labels, boost buttons, profile dialogs, settings, interval units and all remaining UI strings are now translated in every language.

## v3.6.1 — 2026-06-13

### Changed
- **Recovery page** now groups recovery points by their backup **session date-time**. Each backup run is a separate, clearly-labelled group (calendar header + item count), and even backups made on the same day are separated because their time differs. Within a group, each item shows its localized type and exact time.

## v3.6.0 — 2026-06-13

A design, localization and update-engine release.

### Added
- **Live tray tooltip** — hovering the tray icon shows current CPU temperature, CPU/GPU/RAM usage.
- **Real update engine** — robust winget scan (`--include-unknown`) of all upgradable apps, with **Update all**, **Update selected** (checkboxes) and per-item update; a clear warning when winget is unavailable; OS-managed components (DirectX) are shown as informational and route to Windows Update instead of failing.

### Changed
- **Every page redesigned** for a cleaner, more professional, user-friendly look:
  - Monitoring: section icons, large primary metric, tidy key/value stat rows, per-core load chips.
  - Optimization: grouped by category with icons, a real on/off **toggle switch** per tweak, an applied-count badge, inline preview.
  - Temp Cleaner, Updates, Recovery, Benchmark: consistent cards, icons, empty-state placeholders and busy overlays.
- **Themed context menu** — the tray right-click menu now follows the active theme (no more white-on-white text).
- **Localization fixes** — previously English-only labels (clean modes, temp categories, update details, recovery kinds, interval units) are now translated in all five languages.

### Fixed
- Sensor values reading `NaN`/`Infinity` now display as `0` (e.g. CPU frequency).

## v3.5.2 — 2026-06-13

A major UX, design and feature release.

### Added
- **System tray integration** — minimize and/or close to the tray (configurable), with a tray menu (Open, Clean RAM now, Exit) and double-click to restore.
- **Automatic RAM cleaning** scheduler with selectable intervals (5 / 10 / 15 / 25 / 30 / 45 / 60 / 120 minutes), running safely in the background.
- **Interface animations** (page transitions, button micro-interactions, animated busy/loading overlays with a ring spinner) — toggleable in Settings.
- **Editable gaming profiles** — customize which optimization actions each profile applies, with per-profile save and reset.
- **Two new profiles**: *Ultimate Performance* (everything, including the hidden Ultimate Performance power plan — ideal as a maximum-performance preset) and *Battery Saver* (laptop/low-power oriented).
- **Seven new optimization actions**: Ultimate Performance & Power Saver plans, performance visual effects, remove menu show delay, disable transparency, disable Nagle's algorithm, reduce telemetry.
- **Dashboard quick actions** (Clean RAM, Clean Temp, Optimize, Profiles), a **System Information** card (machine, OS, CPU, GPU, RAM, uptime) and a **Top processes by memory** list.
- **First-run language** is detected from the installer choice (registry) or the OS culture.
- Professional installers: **MSI (WiX)** and **EXE (Inno Setup)** with license/privacy acceptance, install-language selection, custom install location, shortcuts, repair, update and uninstall.

### Changed
- **Complete visual overhaul**: fully theme-aware control templates (ComboBox + dropdown, CheckBox, TextBox, ScrollBar, ToolTip, list selection) so every control respects the active theme — no more white control interiors in dark themes.
- Sidebar redesigned with Segoe MDL2 icons, active-item indicator and brand header.
- New `Brush.OnAccent`, `Brush.ControlBackground`, `Brush.Overlay` and related tokens added to all six themes.

### Fixed
- `Run.Text` two-way binding crash on the read-only localization indexer.

## v1.0.0 — 2026-06-13

Initial release.

### Added
- Dashboard with live CPU/GPU/RAM metrics, disk health, FPS estimate, health & optimization scores
- Real-time hardware monitoring (LibreHardwareMonitor): CPU, GPU, RAM, SMART storage, motherboard — CSV export
- Safe-only Windows optimization engine with preview, undo, registry backup and one-click rollback
- 6 gaming profiles (Competitive FPS, Maximum FPS, Balanced, Streaming, Low-End PC, Laptop) with benchmark comparison
- RAM cleaner: working-set trim, standby list purge, file-cache flush
- Temp cleaner with Smart/Deep/Preview modes and quarantine-based restore
- Recovery system: restore points, registry/config backups, snapshots, one-click restore
- Micro-benchmark suite with before/after Markdown reports
- Update center (winget, VC++/.NET checks, Windows Update driver path, self-update check)
- 5 languages (TR/EN/DE/NL/RU) with runtime switching and validation
- 6 themes (Light/Dark/Gaming/RGB Neon/Minimal/Custom JSON engine) with runtime switching
- webhook.txt startup automation with strict whitelist
- Serilog logging with in-app viewer and export
- Inno Setup installer, portable zip, CI/CD pipelines (build/test/release)
- 68 automated tests (unit, integration, UI logic, performance)
