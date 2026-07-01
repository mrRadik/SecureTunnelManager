#Requires -Version 5.1
<#
.SYNOPSIS
    Builds publish + MSI installer. Upgrades preserve tunnels in LocalAppData.

.EXAMPLE
    .\scripts\build-installer.ps1
    .\scripts\build-installer.ps1 -ProductVersion 1.1.0
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    [ValidateSet('win-x64')]
    [string] $Runtime = 'win-x64',

    [string] $ProductVersion = '',

    [switch] $SkipInstaller
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = Resolve-Path (Join-Path $PSScriptRoot '..')
$Project = Join-Path $Root 'SecureTunnelManager.UI\SecureTunnelManager.UI.csproj'
$InstallerProject = Join-Path $Root 'installer\SecureTunnelManager.Installer.wixproj'
$PublishDir = Join-Path $Root "publish\$Runtime"
$OutputDir = Join-Path $Root 'installer\output'
$GenerateIconScript = Join-Path $Root 'scripts\generate-app-icon.ps1'

function Get-ProjectVersion {
    param([string] $ProjectPath)
    [xml] $xml = Get-Content $ProjectPath
    $version = $xml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($version)) { return '1.0.0' }
    return $version
}

function ConvertTo-MsiVersion {
    param([string] $Version)
    $parts = $Version.Split('.')
    while ($parts.Count -lt 4) { $parts += '0' }
    return ($parts[0..3] -join '.')
}

Write-Host '==> Generating application icon...' -ForegroundColor Cyan
& $GenerateIconScript

if ([string]::IsNullOrWhiteSpace($ProductVersion)) {
    $ProductVersion = Get-ProjectVersion -ProjectPath $Project
}

$MsiVersion = ConvertTo-MsiVersion -Version $ProductVersion

Write-Host "==> Publishing $Runtime ($Configuration, v$ProductVersion, self-contained)..." -ForegroundColor Cyan

if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

dotnet publish $Project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Version=$ProductVersion `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -o $PublishDir

if (-not (Test-Path (Join-Path $PublishDir 'SecureTunnelManager.exe'))) {
    throw "Publish failed: SecureTunnelManager.exe not found in $PublishDir"
}

$iconInPublish = Join-Path $PublishDir 'Assets\app.ico'
if (-not (Test-Path $iconInPublish)) {
    throw "app.ico was not copied to publish output."
}

Write-Host "    Published to: $PublishDir" -ForegroundColor Green

if ($SkipInstaller) {
    Write-Host 'SkipInstaller set — publish only.' -ForegroundColor Yellow
    exit 0
}

Write-Host "==> Building MSI installer v$MsiVersion..." -ForegroundColor Cyan

dotnet build $InstallerProject -c $Configuration -p:Platform=x64 -p:ProductVersion=$MsiVersion

$msiFile = Join-Path $OutputDir 'SecureTunnelManager-Setup.msi'
if (-not (Test-Path $msiFile)) {
    throw "Installer build failed: $msiFile was not created."
}

$versionedMsi = Join-Path $OutputDir "SecureTunnelManager-Setup-$ProductVersion.msi"
Copy-Item $msiFile $versionedMsi -Force

$sizeMb = [math]::Round((Get-Item $msiFile).Length / 1MB, 1)
Write-Host ''
Write-Host 'Done!' -ForegroundColor Green
Write-Host "  Installer: $msiFile ($sizeMb MB)" -ForegroundColor Green
Write-Host "  Versioned: $versionedMsi" -ForegroundColor Green
Write-Host ''
Write-Host 'Upgrade notes:' -ForegroundColor Cyan
Write-Host '  - Run the new MSI on top of the existing installation.' -ForegroundColor Gray
Write-Host '  - Tunnels and vault data stay in %LocalAppData%\SecureTunnelManager\' -ForegroundColor Gray
Write-Host '  - The installer closes the app automatically if it is running.' -ForegroundColor Gray
