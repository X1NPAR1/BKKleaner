# BKKleaner full build pipeline: clean -> restore -> compile -> test -> publish -> package -> installer
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "==> [1/7] Clean" -ForegroundColor Cyan
dotnet clean BKKleaner.slnx -c $Configuration -v q --nologo
if ($LASTEXITCODE -ne 0) { exit 1 }
Remove-Item -Recurse -Force "$root\publish", "$root\artifacts" -ErrorAction SilentlyContinue

Write-Host "==> [2/7] Restore" -ForegroundColor Cyan
dotnet restore BKKleaner.slnx
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "==> [3/7] Compile" -ForegroundColor Cyan
dotnet build BKKleaner.slnx -c $Configuration --no-restore -v q --nologo
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "==> [4/7] Test" -ForegroundColor Cyan
dotnet test BKKleaner.slnx -c $Configuration --no-build -v q --nologo
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "==> [5/7] Publish (self-contained $Runtime)" -ForegroundColor Cyan
dotnet publish BKKleaner.csproj -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=false -o "$root\publish\$Runtime"
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "==> [6/7] Validate publish output" -ForegroundColor Cyan
$exe = "$root\publish\$Runtime\BKKleaner.exe"
if (-not (Test-Path $exe)) { Write-Error "BKKleaner.exe missing from publish output"; exit 1 }
foreach ($required in @("Localization\en.json", "Localization\tr.json", "logo.ico")) {
    if (-not (Test-Path "$root\publish\$Runtime\$required")) {
        Write-Error "Missing dependency in publish output: $required"; exit 1
    }
}
Write-Host "Publish output validated: $((Get-ChildItem "$root\publish\$Runtime" -Recurse -File).Count) files"

Write-Host "==> [7/7] Package" -ForegroundColor Cyan
New-Item -ItemType Directory -Force "$root\artifacts" | Out-Null
Compress-Archive -Path "$root\publish\$Runtime\*" -DestinationPath "$root\artifacts\BKKleaner-$Runtime.zip" -Force

if (-not $SkipInstaller) {
    $iscc = Get-Command iscc -ErrorAction SilentlyContinue
    if ($null -eq $iscc) {
        $candidates = @("$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe")
        $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    } else {
        $iscc = $iscc.Source
    }
    if ($iscc) {
        Write-Host "==> Installer (Inno Setup)" -ForegroundColor Cyan
        & $iscc "$root\Installer\BKKleaner.iss"
        if ($LASTEXITCODE -ne 0) { exit 1 }
    } else {
        Write-Warning "Inno Setup (iscc) not found - installer skipped. The release CI pipeline builds it."
    }
}

Write-Host "`nBuild pipeline finished. Artifacts:" -ForegroundColor Green
Get-ChildItem "$root\artifacts" | ForEach-Object { Write-Host "  $($_.Name) ($([math]::Round($_.Length / 1MB, 1)) MB)" }
