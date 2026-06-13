# GitHub Integration

The actual CI/CD definitions live in [`.github/workflows/`](../.github/workflows/) —
GitHub only reads pipelines from that location. This folder documents them.

## Pipelines

| Workflow | Trigger | What it does |
|---|---|---|
| `build.yml` | push/PR to `main` | Restore → build (Release) → upload build artifact |
| `test.yml` | push/PR to `main` | Full test suite, TRX results uploaded |
| `release.yml` | tag `v*` | Test → self-contained publish → validate output → portable zip → Inno Setup installer → GitHub Release with auto-generated changelog |

## Releasing

```bash
git tag v1.0.1
git push origin v1.0.1
```

The release pipeline produces and attaches:
- `BKKleaner-win-x64.zip` (portable, self-contained)
- `BKKleaner-Setup-<version>.exe` (Inno Setup installer)

Release notes are generated automatically from merged PRs/commits
(`softprops/action-gh-release` with `generate_release_notes`).
