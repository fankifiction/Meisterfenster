param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "publish"
$project = Join-Path $root "Wochenplanung.Presentation\Wochenplanung.Presentation.csproj"
$iss = Join-Path $PSScriptRoot "Meisterfenster.iss"
$anleitungSrc = Join-Path $root "Anleitung_Meisterfenster.txt"

Write-Host "Publish (self-contained, single-file) ..."
dotnet publish $project -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

[xml]$propsEarly = Get-Content (Join-Path $root "Directory.Build.props")
$versionEarly = $propsEarly.Project.PropertyGroup.MeisterfensterVersion
if (-not $versionEarly) { $versionEarly = "1.4.2" }
Copy-Item $anleitungSrc (Join-Path $publishDir "Anleitung_Meisterfenster.txt") -Force
@(
    "Meisterfenster Wochenplanung"
    "Version: $versionEarly"
    "Build: $(Get-Date -Format 'yyyy-MM-dd')"
    ""
    "Start: Wochenplanung.Presentation.exe"
    "Auf einer Freigabe: Meisterfenster-Starten.bat (nicht die EXE)"
    "Plattform: Windows 10/11 (64-Bit)"
    ""
    "Siehe Anleitung_Meisterfenster.txt fuer die Bedienung."
) | Set-Content -Path (Join-Path $publishDir "VERSION.txt") -Encoding UTF8

$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    Write-Warning "Inno Setup nicht gefunden. Installiere Inno Setup 6 und starte das Skript erneut."
    exit 1
}

[xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
$version = $props.Project.PropertyGroup.MeisterfensterVersion
if (-not $version) { $version = "1.4.2" }

Write-Host "Build Setup $version ..."
& $iscc "/DMyAppVersion=$version" $iss
Write-Host "Fertig. Setup liegt unter installer\output"
