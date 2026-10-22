param([switch]$RemoveScreenshots)

$ErrorActionPreference = "Stop"
$destDir = Join-Path $env:LOCALAPPDATA "Programs\JiePing"
$destExe = Join-Path $destDir "JiePing.exe"

$running = @(Get-Process -Name "JiePing" -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    if (Test-Path $destExe) {
        try {
            Start-Process -FilePath $destExe -ArgumentList "--exit" -WindowStyle Hidden | Out-Null
        } catch {
        }
        Start-Sleep -Milliseconds 900
    }
    Get-Process -Name "JiePing" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 700
}

$shortcutName = (-join ([char[]](0x622A, 0x5C4F))) + ".lnk"
$shortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) $shortcutName
if (Test-Path $shortcutPath) {
    Remove-Item -LiteralPath $shortcutPath -Force
}

$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
Remove-ItemProperty -Path $runKey -Name "JiePing" -ErrorAction SilentlyContinue

if (Test-Path $destDir) {
    Remove-Item -LiteralPath $destDir -Recurse -Force
}

if ($RemoveScreenshots) {
    $dataDir = Join-Path $env:LOCALAPPDATA "JiePing"
    if (Test-Path $dataDir) {
        Remove-Item -LiteralPath $dataDir -Recurse -Force
    }
}

Write-Host "JiePing removed."
