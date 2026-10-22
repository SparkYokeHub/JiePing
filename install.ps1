param([switch]$NoLaunch)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root "dist\JiePing.exe"
if (-not (Test-Path $source)) {
    throw "JiePing.exe was not found. Run build.ps1 first."
}

$destDir = Join-Path $env:LOCALAPPDATA "Programs\JiePing"
$destExe = Join-Path $destDir "JiePing.exe"
New-Item -ItemType Directory -Force -Path $destDir | Out-Null

# Close a previous instance if present, without starting a new one.
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
    Start-Sleep -Milliseconds 900
}

for ($attempt = 0; $attempt -lt 12; $attempt++) {
    try {
        Copy-Item -LiteralPath $source -Destination $destExe -Force
        break
    } catch {
        if ($attempt -eq 11) {
            throw
        }
        Start-Sleep -Milliseconds 500
    }
}

# Desktop shortcut name is built from Unicode code points so this script
# stays compatible with Windows PowerShell's default ANSI file decoding.
$shortcutName = (-join ([char[]](0x622A, 0x5C4F))) + ".lnk"
$desktop = [Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktop $shortcutName
$wsh = New-Object -ComObject WScript.Shell
$shortcut = $wsh.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $destExe
$shortcut.WorkingDirectory = $destDir
$shortcut.IconLocation = "$destExe,0"
$shortcut.Description = "JiePing - Ctrl+Alt"
$shortcut.WindowStyle = 1
$shortcut.Save()

# Start with Windows, hidden in the tray and delayed inside the app.
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$runValue = '"' + $destExe + '" --startup'
Set-ItemProperty -Path $runKey -Name "JiePing" -Value $runValue -Type String

try {
    $ie4uinit = Join-Path $env:WINDIR "System32\ie4uinit.exe"
    if (Test-Path $ie4uinit) {
        & $ie4uinit -show
    }
} catch {
}

Write-Host ("Installed: " + $destExe)
Write-Host ("Shortcut:  " + $shortcutPath)
Write-Host ("Startup:   " + $runValue)

if (-not $NoLaunch) {
    Start-Process -FilePath $destExe -WorkingDirectory $destDir
}
