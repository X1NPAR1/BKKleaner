# BKKleaner — User Guide

## First start

BKKleaner asks for administrator rights on launch — sensors, registry optimizations,
restore points and RAM cleaning need elevation. The app starts on the **Dashboard** with
live metrics within a second.

## Pages

### Dashboard
Health overview: CPU/GPU temperature cards, RAM usage + MHz, disk health, FPS estimate,
health score (0–100, derived from temps/RAM/disk health) and optimization score
(% of available optimizations applied). Live graphs for CPU/GPU/RAM. A yellow alert bar
appears when a threshold from Settings is exceeded.

### Monitoring
Full sensor detail: per-core CPU load, voltages, package power, VRAM, GPU fan, RAM
timings info, motherboard fans/sensors and SMART storage status with read/write rates.
**Export CSV** writes the in-memory history to your Desktop.

### Optimization
Each tweak card shows **Preview** (exact current → target values) and **Apply/Undo**.
Before anything is applied BKKleaner creates a full backup (restore point if enabled,
registry export, config backup, snapshot). **Undo all** rolls every applied action back.
The startup manager below lists HKCU/HKLM Run entries and toggles them via the
StartupApproved mechanism (the same one Task Manager uses).

### RAM Cleaner
Three independent options. Trimming working sets is always safe: pages are reloaded from
the page file on demand and protected system processes are skipped entirely.

### Temp Cleaner
1. Pick a mode — **Smart** (temp + crash dumps), **Deep** (adds browser/shader caches and
   logs), **Preview** (scan only).
2. **Scan**, review/deselect items, **Clean**.
3. Cleaned items go to quarantine; use the restore buttons at the bottom to bring a whole
   snapshot back.

### Profiles
Six curated profiles. Enable *"Run benchmark before/after"* to get a delta report
(FPS estimate, latency, CPU/RAM) saved as Markdown. Applying a different profile first
rolls back the previous one. **Rollback active profile** undoes everything.

### Benchmark
Run a ~3 second micro-benchmark suite. **Set as baseline**, optimize, run again — the
comparison and a report path appear automatically.

### Recovery
All recovery points (restore points, registry exports, config backups, snapshots) with
one-click restore. **Create full backup** runs every backup type at once.

### Settings
Language (tr/en/de/nl/ru), theme (5 built-in + custom JSON), polling interval, warning
thresholds, safety toggles, translation validation.

### Logs
Live log stream with level filter, export to Desktop, clear.

### Updates
winget-based updates for installed apps and runtimes (silent, validated by exit code).
Driver updates intentionally route to Windows Update — the only safe driver channel.

## Custom themes

Create a JSON file mapping resource keys to hex colors and load it via
Settings → *Load custom...*:

```json
{
  "Brush.WindowBackground": "#101418",
  "Brush.Accent": "#FF6A00",
  "Brush.Text": "#F0F0F0"
}
```

Unspecified keys fall back to the Dark theme.

## webhook.txt

See `Config/webhook.sample.txt`. The file is processed once at startup, then renamed to
`webhook.processed_<timestamp>.txt`. Disable the feature in Settings → Safety.
