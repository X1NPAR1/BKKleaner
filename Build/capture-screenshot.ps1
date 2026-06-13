# Captures a screenshot of the running BKKleaner window for the README.
param(
    [string]$ExePath,
    [string]$OutPng,
    [int]$WaitSeconds = 8
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Capture {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

$proc = Start-Process -FilePath $ExePath -PassThru
try {
    Start-Sleep -Seconds $WaitSeconds
    $proc.Refresh()
    $hwnd = $proc.MainWindowHandle
    if ($hwnd -eq [IntPtr]::Zero) { throw "Main window not found" }

    [Win32Capture]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Milliseconds 800

    $rect = New-Object Win32Capture+RECT
    [Win32Capture]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) { throw "Invalid window rect" }

    $bmp = New-Object System.Drawing.Bitmap($width, $height)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $bmp.Save($OutPng, [System.Drawing.Imaging.ImageFormat]::Png)
    $gfx.Dispose(); $bmp.Dispose()
    Write-Host "Saved: $OutPng ($width x $height)"
}
finally {
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
}
