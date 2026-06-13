# Launches BKKleaner, clicks a sidebar nav item by its (localized) name via UI Automation, screenshots.
param(
    [string]$ExePath,
    [string]$NavName,
    [string]$OutPng,
    [int]$WaitSeconds = 9
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Cap {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }
}
"@

$proc = Start-Process -FilePath $ExePath -PassThru
try {
    Start-Sleep -Seconds $WaitSeconds
    $proc.Refresh()
    $hwnd = $proc.MainWindowHandle
    if ($hwnd -eq [IntPtr]::Zero) { throw "No main window" }

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $NavName)
    $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if ($el -ne $null) {
        $pt = $el.GetClickablePoint()
        [Win32Cap]::SetForegroundWindow($hwnd) | Out-Null
        Start-Sleep -Milliseconds 300
        [Win32Cap]::Click([int]$pt.X, [int]$pt.Y)
        Start-Sleep -Milliseconds 1300
    } else {
        Write-Warning "Nav item '$NavName' not found"
    }

    [Win32Cap]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Milliseconds 600
    $rect = New-Object Win32Cap+RECT
    [Win32Cap]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $bmp.Save($OutPng, [System.Drawing.Imaging.ImageFormat]::Png)
    $gfx.Dispose(); $bmp.Dispose()
    Write-Host "Saved: $OutPng ($w x $h)"
}
finally {
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
}
