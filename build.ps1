$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) {
    throw "Cannot find the .NET Framework 4.x C# compiler."
}

$src = @(Get-ChildItem (Join-Path $root "src\*.cs") | ForEach-Object { $_.FullName })
$outDir = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = Join-Path $outDir "JiePing.exe"
$icon = Join-Path $root "assets\app.ico"
$brandPng = Join-Path $root "assets\app_512.png"
$rewardAlipay = Join-Path $root "assets\reward_alipay.jpg"
$rewardWechat = Join-Path $root "assets\reward_wechat.png"
$manifest = Join-Path $root "src\app.manifest"

$compilerArgs = @(
    "/nologo",
    "/target:winexe",
    "/platform:anycpu",
    "/optimize+",
    "/codepage:65001",
    "/win32icon:$icon",
    "/win32manifest:$manifest",
    "/resource:$icon,app.ico",
    "/resource:$brandPng,app_512.png",
    "/resource:$rewardAlipay,reward_alipay.jpg",
    "/resource:$rewardWechat,reward_wechat.png",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/out:$out"
) + $src

& $csc @compilerArgs
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

$item = Get-Item $out
Write-Host ("Built {0}  ({1:N0} bytes)" -f $item.FullName, $item.Length)
