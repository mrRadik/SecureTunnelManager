#Requires -Version 5.1
<#
.SYNOPSIS
    Generates SecureTunnelManager.UI/Assets/app.ico (multi-size).
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$Root = Resolve-Path (Join-Path $PSScriptRoot '..')
$AssetsDir = Join-Path $Root 'SecureTunnelManager.UI\Assets'
$IconPath = Join-Path $AssetsDir 'app.ico'

New-Item -ItemType Directory -Force -Path $AssetsDir | Out-Null

function New-TunnelBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::FromArgb(255, 30, 30, 30))

    $scale = $Size / 256.0
    $nodeBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 120, 212))
    $linePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 34, 197, 94)), (6 * $scale)
    $lockPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 255, 255)), (4 * $scale)

    $leftX = 56 * $scale
    $rightX = 200 * $scale
    $centerY = 128 * $scale
    $nodeR = 28 * $scale

    $g.DrawLine($linePen, ($leftX + $nodeR), $centerY, ($rightX - $nodeR), $centerY)

    $g.FillEllipse($nodeBrush, ($leftX - $nodeR), ($centerY - $nodeR), ($nodeR * 2), ($nodeR * 2))
    $g.FillEllipse($nodeBrush, ($rightX - $nodeR), ($centerY - $nodeR), ($nodeR * 2), ($nodeR * 2))

    $lockW = 34 * $scale
    $lockH = 26 * $scale
    $lockX = (128 * $scale) - ($lockW / 2)
    $lockY = (118 * $scale) - ($lockH / 2)
    $g.DrawRectangle($lockPen, $lockX, $lockY, $lockW, $lockH)
    $g.DrawArc($lockPen, ($lockX + (4 * $scale)), ($lockY - (16 * $scale)), ($lockW - (8 * $scale)), (24 * $scale), 180, 180)

    $g.Dispose()
    return $bmp
}

function Save-MultiSizeIcon {
    param(
        [System.Drawing.Bitmap[]]$Bitmaps,
        [string]$Path
    )

    $stream = [System.IO.File]::OpenWrite($Path)
    $writer = New-Object System.IO.BinaryWriter $stream
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$Bitmaps.Length)

        $offset = 6 + (16 * $Bitmaps.Length)
        $imageData = New-Object System.Collections.Generic.List[byte[]]

        foreach ($bmp in $Bitmaps) {
            $ms = New-Object System.IO.MemoryStream
            try {
                $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
                $pngBytes = $ms.ToArray()
            }
            finally {
                $ms.Dispose()
            }
            $imageData.Add($pngBytes)

            $width = [byte][Math]::Min($bmp.Width, 255)
            $height = [byte][Math]::Min($bmp.Height, 255)
            $writer.Write($width)
            $writer.Write($height)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$pngBytes.Length)
            $writer.Write([UInt32]$offset)
            $offset += $pngBytes.Length
        }

        foreach ($data in $imageData) {
            $writer.Write($data)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

$sizes = @(16, 32, 48, 256)
$bitmaps = $sizes | ForEach-Object { New-TunnelBitmap -Size $_ }
Save-MultiSizeIcon -Bitmaps $bitmaps -Path $IconPath

foreach ($bmp in $bitmaps) { $bmp.Dispose() }

Write-Host "Generated: $IconPath"
