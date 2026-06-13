<div align="center">

# 🧹 BKKleaner

**Premium Windows optimization, monitoring & cleaning suite**

[![Build](https://github.com/X1NPAR1/BKKleaner/actions/workflows/build.yml/badge.svg)](https://github.com/X1NPAR1/BKKleaner/actions/workflows/build.yml)
[![Test](https://github.com/X1NPAR1/BKKleaner/actions/workflows/test.yml/badge.svg)](https://github.com/X1NPAR1/BKKleaner/actions/workflows/test.yml)
[![Release](https://img.shields.io/github/v/release/X1NPAR1/BKKleaner?include_prereleases)](https://github.com/X1NPAR1/BKKleaner/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)

*WPF · MVVM · Clean Architecture · 5 languages · 6 themes*

</div>

---

## ✨ Features

| Module | What it does |
|---|---|
| 📊 **Dashboard** | CPU/GPU temperature, RAM usage & MHz, disk health, FPS estimate, health & optimization scores, live graphs, threshold alerts |
| 📡 **Monitoring** | Real-time CPU (temp/usage/frequency/voltage/per-core), GPU (temp/usage/VRAM/fan/watt), RAM, SMART storage, motherboard sensors & fans — with CSV export |
| ⚡ **Optimization** | Safe-only Windows tweaks: power plan, Game Mode, background apps, startup manager, CPU scheduling, latency improvements, hardware GPU scheduling — every action has **preview, undo and automatic registry backup** |
| 🎮 **Profiles** | 6 gaming profiles (Competitive FPS, Maximum FPS, Balanced, Streaming, Low-End PC, Laptop) with preview, rollback and before/after benchmark comparison |
| 🧠 **RAM Cleaner** | Working-set trimming, standby list purge, file-cache flush — protected system processes are never touched |
| 🗑️ **Temp Cleaner** | Windows temp, browser caches, DirectX shader caches, logs, crash dumps — Smart / Deep / Preview modes with **quarantine-based restore** |
| ⏪ **Recovery** | Automatic restore point + registry backup + config backup + snapshot before every optimization; one-click restore |
| 🏁 **Benchmark** | CPU single/multi-thread, memory bandwidth, timer latency, FPS estimate — comparison reports in Markdown |
| 🔄 **Updates** | winget-based app updates, VC++/.NET runtime checks, safe driver path via Windows Update, self-update check |
| 📜 **Logging** | Serilog file logs + live in-app viewer with filtering and export |
| 🤖 **webhook.txt** | Validated startup automation with a strict whitelist — dangerous tasks are rejected |

## 🌍 Languages & 🎨 Themes

**Languages:** Türkçe · English · Deutsch · Nederlands · Русский — runtime switching, automatic English fallback, missing-key detection, translation validation.

**Themes:** Light · Dark · Gaming · RGB Neon · Minimal · **Custom JSON theme engine** — switch at runtime, no restart.

## 📥 Installation

### Installer (recommended)
Download **BKKleaner-Setup-x.x.x.exe** from [Releases](https://github.com/X1NPAR1/BKKleaner/releases) and run it.
Supports silent install (`/VERYSILENT`), custom install location, repair, update and uninstall.
Default location: `C:\Program Files\BKKleaner`.

### Portable
Download **BKKleaner-win-x64.zip**, extract anywhere, run `BKKleaner.exe`.
Self-contained — no .NET installation required.

> BKKleaner requests administrator rights: hardware sensors, registry optimizations and restore points require elevation.

## 🤖 webhook.txt automation

Place a `webhook.txt` next to `BKKleaner.exe` (see `Config/webhook.sample.txt`):

```json
{"type":"clean_temp"}
{"type":"ram_clean"}
{"type":"optimize_gaming"}
{"type":"switch_theme","value":"dark"}
```

Only whitelisted tasks run (`clean_temp`, `ram_clean`, `optimize_gaming`, `switch_theme`, `switch_language`, `benchmark`).
Everything else is **rejected and logged**. The file is archived after processing so it never runs twice.

## 🛠️ Building from source

```powershell
git clone https://github.com/X1NPAR1/BKKleaner.git
cd BKKleaner
.\Build\build.ps1          # clean → restore → compile → test → publish → package → installer
```

Requirements: .NET SDK 9/10, Windows 10/11. Inno Setup 6 (optional, for the installer).

## 🧪 Tests

```powershell
dotnet test BKKleaner.slnx
```

68 tests: unit (services, parsers, safety guards), integration (temp cleaner quarantine/restore, recovery),
UI logic (navigation, theme switching, localization) and performance guards.

## 🏗️ Architecture

```
BKKleaner/
├── App/            Entry point, DI container, global exception handling
├── Views/          XAML pages (11) + MainWindow
├── ViewModels/     MVVM Toolkit view models (12)
├── Models/         Domain models
├── Services/       Settings, themes, cleaners, updates, webhook automation
├── Monitoring/     LibreHardwareMonitor-based sensor engine
├── Optimization/   Safe registry/power actions + gaming profiles
├── Recovery/       Restore points, registry/config backups, snapshots
├── Security/       Permission model + safe execution layer
├── Benchmark/      Micro-benchmark suite + comparison reports
├── Localization/   JSON dictionaries (tr/en/de/nl/ru)
├── Themes/         Theme dictionaries + custom theme engine
├── Tests/          xUnit test project
├── Installer/      Inno Setup script
├── Build/          Build pipeline script
└── Docs/           Architecture, usage and security docs
```

Principles: SOLID, DRY, KISS, separation of concerns, service layer pattern, interfaces everywhere, `ILogger<T>`, cancellation tokens, no blocked UI thread.

## 🔒 Safety model

- **Safe-only modifications** — no risky registry hacks
- Registry **.reg export before every change** + value-level undo store
- System restore point before optimization batches (configurable)
- Temp cleaning moves files to **quarantine** first — fully restorable
- Strict path whitelist: system/user-profile roots can never be deleted
- Admin permission model with a safe execution layer around every privileged call

## 📄 License

[MIT](LICENSE) © 2026 [X1NPAR1](https://github.com/X1NPAR1)
