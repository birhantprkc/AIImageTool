<#
    publish.ps1 — Publish script for ZVision (Dual Mode: Full & Lite)
    Adheres to AgentOption .NET Publish Release standard & ZeroUniverse rules.
#>
[CmdletBinding()]
param(
    [ValidateSet('Lite', 'Full', 'All')]
    [string]$Mode = 'Lite',
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$HostProj = Join-Path $Root "ZVision.Host\ZVision.Host.csproj"
$FaceProj = Join-Path $Root "ZVision.Plugins.FaceRestorer\ZVision.Plugins.FaceRestorer.csproj"
$UpscaleProj = Join-Path $Root "ZVision.Plugins.Upscaler\ZVision.Plugins.Upscaler.csproj"
$TaggerProj = Join-Path $Root "ZVision.Plugins.VisionTagger\ZVision.Plugins.VisionTagger.csproj"
$Dist = Join-Path $Root "Publish"

if (Test-Path $Dist) {
    Remove-Item -Recurse -Force "$Dist\*" -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Force -Path $Dist | Out-Null
}

$solDir = (Get-Item $Root).FullName + "\"

# Build plugins first
Write-Host ">>> Building ZVision plugins..." -ForegroundColor Cyan
dotnet build $FaceProj -c $Configuration -p:SolutionDir=$solDir
dotnet build $UpscaleProj -c $Configuration -p:SolutionDir=$solDir
dotnet build $TaggerProj -c $Configuration -p:SolutionDir=$solDir

if ($Mode -eq 'Full' -or $Mode -eq 'All') {
    Write-Host ">>> Publishing ZVision FULL (Self-Contained Single File)..." -ForegroundColor Cyan
    $outFull = Join-Path $Dist "Full"
    dotnet publish $HostProj -c $Configuration -r $Runtime --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o $outFull
        
    Copy-Item -Path "$Root\ZVision.Host\bin\$Configuration\net8.0-windows\$Runtime\Plugins" -Destination "$outFull\Plugins" -Recurse -Force
    if (Test-Path "$Root\native") {
        Copy-Item -Path "$Root\native\*.dll" -Destination $outFull -Force
    }
    if (Test-Path "$Root\lensfun") {
        Copy-Item -Path "$Root\lensfun" -Destination "$outFull\lensfun" -Recurse -Force
    }
    
    if (Test-Path "$outFull\ZVision.Host.exe") {
        Move-Item "$outFull\ZVision.Host.exe" -Destination "$outFull\ZVision.exe" -Force
    }
    Write-Host "  [OK] Full build generated at: $outFull\ZVision.exe" -ForegroundColor Green
}

if ($Mode -eq 'Lite' -or $Mode -eq 'All') {
    Write-Host ">>> Publishing ZVision LITE [Framework-Dependent Single File]..." -ForegroundColor Cyan
    $outLite = Join-Path $Dist "Lite"
    dotnet publish $HostProj -c $Configuration -r $Runtime --self-contained false `
        -p:PublishSingleFile=true `
        -o $outLite
        
    Copy-Item -Path "$Root\ZVision.Host\bin\$Configuration\net8.0-windows\$Runtime\Plugins" -Destination "$outLite\Plugins" -Recurse -Force
    if (Test-Path "$Root\native") {
        Copy-Item -Path "$Root\native\*.dll" -Destination $outLite -Force
    }
    if (Test-Path "$Root\lensfun") {
        Copy-Item -Path "$Root\lensfun" -Destination "$outLite\lensfun" -Recurse -Force
    }
    
    if (Test-Path "$outLite\ZVision.Host.exe") {
        Move-Item "$outLite\ZVision.Host.exe" -Destination "$outLite\ZVision.exe" -Force
    }
    Write-Host "  [OK] Lite build generated at: $outLite\ZVision.exe" -ForegroundColor Green
}

Write-Host ">>> ZVision publish completed successfully!" -ForegroundColor Green
