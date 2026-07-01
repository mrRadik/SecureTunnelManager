# Secure Tunnel Manager — Build & Publish

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "$PSScriptRoot\..\publish"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\SecureTunnelManager.UI\SecureTunnelManager.UI.csproj"

Write-Host "Publishing Secure Tunnel Manager ($Configuration)..."

dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -o $OutputDir `
    /p:PublishSingleFile=false

Write-Host "Published to: $OutputDir"
Write-Host "Run: $OutputDir\SecureTunnelManager.exe"
