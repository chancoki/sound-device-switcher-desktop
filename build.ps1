# build.ps1 — compile the whole application with the C# compiler that ships
# inside .NET Framework, so building needs no SDK, no NuGet and no network.
#
#   powershell -ExecutionPolicy Bypass -File build.ps1
#
# Output: dist\AudioSwitch.exe  (single self-contained file)

[CmdletBinding()]
param(
    [switch]$SkipIcon,
    [switch]$DebugBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $root

function Get-CscPath {
    $candidates = @(
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) { return $candidate }
    }
    throw '找不到 csc.exe（需要 .NET Framework 4.x，Windows 7 及以上系统自带）。'
}

$csc = Get-CscPath
Write-Host "编译器: $csc" -ForegroundColor DarkGray

# --- 1. application icon -----------------------------------------------------
$iconPath = Join-Path $root 'src\app.ico'
if (-not $SkipIcon -or -not (Test-Path $iconPath)) {
    Write-Host '[1/3] 生成应用图标 src\app.ico ...' -ForegroundColor Cyan
    $iconGenExe = Join-Path $root 'tools\IconGen.exe'
    & $csc /nologo /target:exe "/out:$iconGenExe" `
        /reference:System.dll,System.Drawing.dll `
        (Join-Path $root 'tools\IconGen.cs')
    if ($LASTEXITCODE -ne 0) { throw '图标生成器编译失败。' }

    & $iconGenExe $iconPath
    if ($LASTEXITCODE -ne 0) { throw '图标生成失败。' }
} else {
    Write-Host '[1/3] 复用现有 src\app.ico' -ForegroundColor DarkGray
}

# --- 2. main application -----------------------------------------------------
Write-Host '[2/3] 编译 dist\AudioSwitch.exe ...' -ForegroundColor Cyan

$distDir = Join-Path $root 'dist'
if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir | Out-Null }

$sources = Get-ChildItem -Path (Join-Path $root 'src') -Filter *.cs -Recurse |
    ForEach-Object { $_.FullName }

$outExe = Join-Path $distDir 'AudioSwitch.exe'

$cscArgs = @(
    '/nologo',
    '/target:winexe',              # no console window ever appears
    '/platform:anycpu',
    '/optimize+',
    '/codepage:65001',             # sources are UTF-8 without a BOM
    "/out:$outExe",
    '/reference:System.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    "/win32icon:$iconPath",
    "/win32manifest:$(Join-Path $root 'src\app.manifest')"
)
if ($DebugBuild) { $cscArgs += @('/define:DEBUG', '/debug+') }

$cscArgs += $sources
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }

# Remove the temporary icon-generator build product.
$iconGenExe = Join-Path $root 'tools\IconGen.exe'
if (Test-Path $iconGenExe) { Remove-Item $iconGenExe -Force }

# --- 3. summary --------------------------------------------------------------
$exe = Get-Item $outExe
Write-Host '[3/3] 完成' -ForegroundColor Cyan
Write-Host ("      {0}" -f $exe.FullName) -ForegroundColor Green
Write-Host ("      {0:N0} 字节 · 版本 {1}" -f $exe.Length, $exe.VersionInfo.FileVersion) -ForegroundColor Gray
Write-Host ''
Write-Host '运行方式：' -ForegroundColor Gray
Write-Host '  dist\AudioSwitch.exe                          打开图形界面' -ForegroundColor Gray
Write-Host '  dist\AudioSwitch.exe --list                   列出音频输出设备' -ForegroundColor Gray
