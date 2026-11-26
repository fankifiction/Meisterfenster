param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "publish"
$project = Join-Path $root "Wochenplanung.Presentation\Wochenplanung.Presentation.csproj"
$zipPath = Join-Path $root "Meisterfenster-Weitergabe.zip"
$anleitungSrc = Join-Path $root "Anleitung_Meisterfenster.txt"
[xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
$version = $props.Project.PropertyGroup.MeisterfensterVersion
if (-not $version) { $version = "1.4.2" }
$buildDate = Get-Date -Format "yyyy-MM-dd"

Write-Host "=== Meisterfenster Release $version ($buildDate) ===" -ForegroundColor Cyan

Write-Host "`n[1/4] Tests ..." -ForegroundColor Yellow
dotnet test (Join-Path $root "Wochenplanung.sln") -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n[2/4] Publish (self-contained, single-file) ..." -ForegroundColor Yellow
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
dotnet publish $project -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`n[3/4] Anleitung und Versionsinfo ..." -ForegroundColor Yellow
Copy-Item $anleitungSrc (Join-Path $publishDir "Anleitung_Meisterfenster.txt") -Force
@(
    "Meisterfenster Wochenplanung"
    "Version: $version"
    "Build: $buildDate"
    ""
    "Start: Wochenplanung.Presentation.exe"
    "Auf einer Freigabe: Meisterfenster-Starten.bat (nicht die EXE)"
    "Plattform: Windows 10/11 (64-Bit)"
    ""
    "Siehe Anleitung_Meisterfenster.txt fuer die Bedienung."
) | Set-Content -Path (Join-Path $publishDir "VERSION.txt") -Encoding UTF8

Write-Host "`n[4/4] ZIP erstellen ..." -ForegroundColor Yellow
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

$exe = Join-Path $publishDir "Wochenplanung.Presentation.exe"
$exeSize = [math]::Round((Get-Item $exe).Length / 1MB, 1)
$zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)

Write-Host "`nFertig!" -ForegroundColor Green
Write-Host "  EXE:  $exe ($exeSize MB)"
Write-Host "  ZIP:  $zipPath ($zipSize MB)"
Write-Host "`nOptional Setup: powershell -File installer\build-setup.ps1" -ForegroundColor DarkGray
