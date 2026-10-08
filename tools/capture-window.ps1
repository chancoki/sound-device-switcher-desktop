# tools/capture-window.ps1 — screenshot a window by process name, for visual checks.
#
#   powershell -File tools/capture-window.ps1 -ProcessName AudioSwitch -Out shot.png
#
# Uses PrintWindow first (works even when the window is occluded or the desktop
# is not focused) and falls back to a screen grab of the window rectangle.

param(
    [Parameter(Mandatory = $true)][string]$ProcessName,
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$WaitSeconds = 5
)

Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;

public struct RECT { public int Left, Top, Right, Bottom; }

public class WinApi
{
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint flags);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int cmd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    // PowerShell itself is DPI-unaware, so the window manager silently virtualises
    // every rectangle it asks for: a 975px-wide window on a 125% display reports
    // as 780px, and a capture sized from that is clipped. Measure inside a
    // per-monitor-aware thread context so the numbers are real pixels.
    [DllImport("user32.dll")]
    public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    public static IntPtr EnterPerMonitorAware()
    {
        return SetThreadDpiAwarenessContext(new IntPtr(-4)); // PER_MONITOR_AWARE_V2
    }

    public static void RestoreAwareness(IntPtr previous)
    {
        SetThreadDpiAwarenessContext(previous);
    }
}
'@

$deadline = (Get-Date).AddSeconds($WaitSeconds)
$handle = [IntPtr]::Zero
while ((Get-Date) -lt $deadline) {
    $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue |
            Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } |
            Select-Object -First 1
    if ($proc) { $handle = $proc.MainWindowHandle; break }
    Start-Sleep -Milliseconds 300
}

if ($handle -eq [IntPtr]::Zero) { throw "未找到 $ProcessName 的窗口。" }

# Restore + raise so a plain screen grab would also work.
[void][WinApi]::ShowWindow($handle, 9)   # SW_RESTORE
[void][WinApi]::SetForegroundWindow($handle)
Start-Sleep -Milliseconds 900

$rect = New-Object RECT
$previousAwareness = [WinApi]::EnterPerMonitorAware()
[void][WinApi]::GetWindowRect($handle, [ref]$rect)
[WinApi]::RestoreAwareness($previousAwareness)
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
Write-Host "窗口位置 ($($rect.Left),$($rect.Top)) 尺寸 ${width}x${height}"

$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()

# 2 = PW_RENDERFULLCONTENT (captures DWM-composited content)
$ok = [WinApi]::PrintWindow($handle, $hdc, 2)
$graphics.ReleaseHdc($hdc)
$graphics.Dispose()

$captured = $ok -and $bitmap.Width -gt 0

if (-not $captured) {
    Write-Host 'PrintWindow 失败，回退到屏幕抓取。'
    $bitmap.Dispose()
    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($width, $height)))
    $graphics.Dispose()
}

$full = [System.IO.Path]::GetFullPath($Out)
$bitmap.Save($full, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
Write-Host "已保存 $full"
