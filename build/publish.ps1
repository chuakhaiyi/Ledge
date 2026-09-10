param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "artifacts"
)

$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot "..\src\Ledge.App\Ledge.App.csproj"
$outputPath = Join-Path $PSScriptRoot "..\$OutputDir"

Write-Host "Publishing Ledge.App..." -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Runtime: $Runtime" -ForegroundColor Gray
Write-Host "Output: $outputPath" -ForegroundColor Gray

# WPF doesn't support trimming with single-file publishing
# https://aka.ms/dotnet-illink/wpf
dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $outputPath

if ($LASTEXITCODE -eq 0) {
    $exePath = Join-Path $outputPath "Ledge.App.exe"
    $finalPath = Join-Path $outputPath "Ledge.exe"
    if (Test-Path $exePath) {
        Move-Item $exePath $finalPath -Force
        Write-Host "Build successful! Output: $finalPath" -ForegroundColor Green

        # Generate SHA256
        $hash = Get-FileHash $finalPath -Algorithm SHA256
        $hashPath = $finalPath + ".sha256"
        Set-Content -Path $hashPath -Value $hash.Hash
        Write-Host "SHA256: $($hash.Hash)" -ForegroundColor Gray
    }
} else {
    Write-Host "Build failed!" -ForegroundColor Red
    throw "Publish failed with exit code $LASTEXITCODE"
}
