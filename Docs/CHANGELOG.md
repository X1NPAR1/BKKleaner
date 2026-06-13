# Changelog

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
