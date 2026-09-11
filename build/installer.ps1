param(
    [string]$Configuration = 'Release',
    [string]$CompilerPath = '',
    [switch]$SkipPublish
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!$SkipPublish) { & "$PSScriptRoot\publish.ps1" -Configuration $Configuration -OutputDir 'artifacts\app' }
if (!$CompilerPath) {
    $CompilerPath = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path $repo 'artifacts\tooling\inno\ISCC.exe')
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$CompilerPath) { throw 'Install Inno Setup 6, or pass -CompilerPath with the path to ISCC.exe.' }
$publishDir = Join-Path $repo 'artifacts\app'
if (!(Test-Path -LiteralPath "$publishDir\Ledge.exe")) { throw 'Published Ledge.exe is missing.' }
$appProject = [xml](Get-Content -LiteralPath "$repo\src\Ledge.App\Ledge.App.csproj")
$appVersion = [string]($appProject.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne "v$appVersion") { throw 'Release tag does not match the app version.' }
& $CompilerPath "/DPublishDir=$publishDir" "/DAppVersion=$appVersion" "$PSScriptRoot\Ledge.iss"
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed: $LASTEXITCODE" }
$installer = Join-Path $repo 'artifacts\installer\Ledge-Setup.exe'
(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash | Set-Content -LiteralPath "$installer.sha256"
Write-Host "Installer ready: $installer"
