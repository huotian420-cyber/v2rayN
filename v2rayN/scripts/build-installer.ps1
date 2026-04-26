param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$workRoot = Split-Path $repoRoot -Parent
$artifactsRoot = Join-Path $workRoot "artifacts\v2rayN"
$publishDir = Join-Path $artifactsRoot "publish\$Runtime"
$installerDir = Join-Path $artifactsRoot "installer"
$v2rayNCsproj = Join-Path $repoRoot "v2rayN\v2rayN.csproj"
$amazToolCsproj = Join-Path $repoRoot "AmazTool\AmazTool.csproj"
$issPath = Join-Path $repoRoot "packaging\windows\v2rayN.iss"
$iconPath = Join-Path $repoRoot "v2rayN\Resources\v2rayN.ico"

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue)?.Source
if (-not $dotnet) {
    $defaultDotnet = "C:\Program Files\dotnet\dotnet.exe"
    if (Test-Path $defaultDotnet) {
        $dotnet = $defaultDotnet
    } else {
        throw "dotnet not found"
    }
}

$isccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue)?.Source
}
if (-not $iscc) {
    throw "ISCC.exe not found"
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $installerDir | Out-Null

& $dotnet publish $v2rayNCsproj -c $Configuration -r $Runtime --self-contained true -o $publishDir
& $dotnet publish $amazToolCsproj -c $Configuration -r $Runtime --self-contained true -o (Join-Path $publishDir "AmazTool")

$appExe = Join-Path $publishDir "v2rayN.exe"
if (-not (Test-Path $appExe)) {
    throw "Published v2rayN.exe not found at $appExe"
}

$version = [version]([System.Diagnostics.FileVersionInfo]::GetVersionInfo($appExe).FileVersion)
$appVersion = $version.ToString(3)

& $iscc "/DSourceDir=$publishDir" "/DOutputDir=$installerDir" "/DAppVersion=$appVersion" "/DIconFile=$iconPath" $issPath
