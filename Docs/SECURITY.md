# BKKleaner — Security Model

## Principles

1. **Safe-only modifications.** Only well-documented, reversible Windows settings are
   touched. No undocumented registry hacks, no service deletion, no file forcing.
2. **Backup before change.** Every optimization batch triggers restore point (optional),
   registry `.reg` export, config backup and a state snapshot.
3. **Everything reversible.** Value-level undo store, quarantine-based deletion,
   profile rollback.
4. **Least privilege checks.** `AppPermission` model; admin-only operations are refused
   (not attempted) when the process is not elevated.
5. **Never trust input.** webhook.txt is parsed with a strict whitelist; any unknown task
   type, malformed JSON or extra payload is rejected and logged.

## Privileged operations inventory

| Operation | Mechanism | Mitigation |
|---|---|---|
| Registry optimization | `Registry.SetValue` on documented keys | Previous values stored; `.reg` export; undo |
| Power plan switch | `powercfg /setactive` | Previous scheme GUID recorded |
| Restore point | WMI `SystemRestore.CreateRestorePoint` | Read-only failure handling, OS rate limits respected |
| Standby list purge | `NtSetSystemInformation(SystemMemoryListInformation)` | Requires `SeProfileSingleProcessPrivilege`; no-op without it |
| Working set trim | `EmptyWorkingSet` | Protected process list; access-denied silently skipped |
| File cleaning | `File.Move` to quarantine | Path allow-list (`IsPathSafeToClean`), no force-delete, locked files skipped |
| App updates | `winget upgrade --silent` | Exit-code validation; never runs arbitrary commands |

## Protected process list

`System, Idle, Registry, Memory Compression, smss, csrss, wininit, winlogon, services,
lsass, svchost, dwm, fontdrvhost, audiodg` — never trimmed.

## Path safety

`TempCleanerService.IsPathSafeToClean` only allows paths under: user temp, `%WINDIR%\Temp`,
`%WINDIR%\Logs`, `%LOCALAPPDATA%`. It explicitly rejects the Windows root, user profile
root, Program Files and System32 — including traversal attempts (`..`), because paths are
fully resolved before checking.

## Exception safety

The safe execution layer (`ISecurityService.ExecuteSafeAsync`) wraps privileged calls:
permission check → log → execute → capture. It never lets an exception escape to the UI.
Three global handlers catch anything that slips through elsewhere.

## Reporting a vulnerability

Open a private security advisory on GitHub or an issue at
https://github.com/X1NPAR1/BKKleaner/issues.
