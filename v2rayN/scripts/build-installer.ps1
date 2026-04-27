param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-CoreBundleName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Runtime
    )

    switch ($Runtime) {
        "win-x64" { return "v2rayN-windows-64" }
        "win-arm64" { return "v2rayN-windows-arm64" }
        default { throw "Unsupported runtime '$Runtime' for official v2rayN core bundle." }
    }
}

function Invoke-DownloadFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [Parameter(Mandatory = $true)]
        [string]$Destination,
        [long]$MinimumBytes = 1MB,
        [int]$Attempts = 3
    )

    $parent = Split-Path -Parent $Destination
    if ($parent) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    if (Test-Path $Destination) {
        $existing = Get-Item -LiteralPath $Destination -ErrorAction SilentlyContinue
        if ($existing -and $existing.Length -ge $MinimumBytes) {
            try {
                [IO.Compression.ZipFile]::OpenRead($Destination).Dispose()
                return
            } catch {
                Remove-Item -LiteralPath $Destination -Force -ErrorAction SilentlyContinue
            }
        }
    }

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        if (Test-Path $Destination) {
            Remove-Item -LiteralPath $Destination -Force
        }

        try {
            Invoke-WebRequest -Uri $Url -OutFile $Destination
            $file = Get-Item -LiteralPath $Destination -ErrorAction Stop
            if ($file.Length -lt $MinimumBytes) {
                throw "Downloaded file is unexpectedly small ($($file.Length) bytes)."
            }

            [IO.Compression.ZipFile]::OpenRead($Destination).Dispose()
            return
        } catch {
            if ($attempt -ge $Attempts) {
                throw
            }

            Start-Sleep -Seconds ([Math]::Min(5 * $attempt, 15))
        }
    }
}

function Copy-CoreBundleIntoPublishDir {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Runtime,
        [Parameter(Mandatory = $true)]
        [string]$PublishDir,
        [Parameter(Mandatory = $true)]
        [string]$ArtifactsRoot
    )

    $bundleName = Get-CoreBundleName -Runtime $Runtime
    $downloadUrl = "https://raw.githubusercontent.com/2dust/v2rayN-core-bin/master/$bundleName.zip"
    $cacheDir = Join-Path $ArtifactsRoot "core-bundle"
    $zipPath = Join-Path $cacheDir "$bundleName.zip"
    $extractDir = Join-Path $cacheDir $bundleName

    if (Test-Path $extractDir) {
        Remove-Item -LiteralPath $extractDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    Invoke-DownloadFile -Url $downloadUrl -Destination $zipPath -MinimumBytes 10MB
    Expand-Archive -LiteralPath $zipPath -DestinationPath $cacheDir -Force

    $coreRootCandidates = @(
        $extractDir,
        (Join-Path $extractDir $bundleName)
    ) | Where-Object { Test-Path $_ }

    $coreRoot = $coreRootCandidates |
        Where-Object { Test-Path (Join-Path $_ "bin") } |
        Select-Object -First 1

    if (-not $coreRoot) {
        throw "Unable to locate 'bin' directory inside $bundleName.zip"
    }

    Get-ChildItem -LiteralPath $coreRoot -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $PublishDir -Recurse -Force
    }
}

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

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $installerDir | Out-Null
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Copy-CoreBundleIntoPublishDir -Runtime $Runtime -PublishDir $publishDir -ArtifactsRoot $artifactsRoot

& $dotnet publish $v2rayNCsproj -c $Configuration -r $Runtime --self-contained true -o $publishDir
& $dotnet publish $amazToolCsproj -c $Configuration -r $Runtime --self-contained true -o (Join-Path $publishDir "AmazTool")

$appExe = Join-Path $publishDir "v2rayN.exe"
if (-not (Test-Path $appExe)) {
    throw "Published v2rayN.exe not found at $appExe"
}

$version = [version]([System.Diagnostics.FileVersionInfo]::GetVersionInfo($appExe).FileVersion)
$appVersion = $version.ToString(3)

& $iscc "/DSourceDir=$publishDir" "/DOutputDir=$installerDir" "/DAppVersion=$appVersion" "/DIconFile=$iconPath" $issPath
