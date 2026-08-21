[CmdletBinding()]
param(
    [string]$SourceCover,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$addonRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $addonRoot
if ([string]::IsNullOrWhiteSpace($SourceCover)) {
    $SourceCover = Join-Path $repoRoot 'Workshop\Artwork\PrisonerDiplomacy-cover-en.png'
}

$sourcePath = (Resolve-Path -LiteralPath $SourceCover).Path
$previewPath = Join-Path $addonRoot 'About\Preview.png'
$workshopArtworkDir = Join-Path $addonRoot 'Workshop\Artwork'
$workshopCoverPath = Join-Path $workshopArtworkDir 'PrisonerDiplomacyExampleAddon-cover-en.png'
$textureDir = Join-Path $addonRoot '1.6\Textures\Things\Item'
$sealPath = Join-Path $textureDir 'PDX_DiplomaticSeal.png'
$ledgerPath = Join-Path $textureDir 'PDX_EncryptedDiplomaticLedger.png'

foreach ($directory in @((Split-Path $previewPath), $workshopArtworkDir, $textureDir)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

if (-not $Force -and (Test-Path $previewPath) -and (Test-Path $sealPath) -and (Test-Path $ledgerPath)) {
    Write-Host '[Example Add-on Artwork] Existing generated assets are complete. Use -Force to regenerate.'
    return
}

Add-Type -AssemblyName System.Drawing

function New-RoundedRectanglePath {
    param(
        [System.Drawing.RectangleF]$Rect,
        [float]$Radius
    )
    $diameter = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($Rect.X, $Rect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rect.X, $Rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Save-Png {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path
    )
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Force
    }
    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function New-Preview {
    param([string]$InputPath, [string]$OutputPath)

    $source = [System.Drawing.Bitmap]::FromFile($InputPath)
    try {
        $canvas = New-Object System.Drawing.Bitmap 860, 480, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $graphics = [System.Drawing.Graphics]::FromImage($canvas)
        try {
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
            $graphics.DrawImage($source, 0, 0, 860, 480)

            $band = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(238, 8, 15, 18))
            $edge = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(235, 67, 205, 195)), 3
            $accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 83, 221, 211))
            $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 238, 244, 245))
            $muted = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 170, 190, 194))
            $titleFont = New-Object System.Drawing.Font 'Segoe UI', 30, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
            $subFont = New-Object System.Drawing.Font 'Segoe UI', 15, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
            try {
                $graphics.FillRectangle($band, 0, 356, 860, 124)
                $graphics.FillRectangle($accent, 0, 356, 860, 4)
                $graphics.DrawRectangle($edge, 4, 4, 851, 471)
                $titleRect = New-Object System.Drawing.RectangleF 42, 367, 776, 44
                $subRect = New-Object System.Drawing.RectangleF 44, 419, 772, 28
                $format = New-Object System.Drawing.StringFormat
                $format.Alignment = [System.Drawing.StringAlignment]::Center
                $format.LineAlignment = [System.Drawing.StringAlignment]::Center
                try {
                    $graphics.DrawString('EXAMPLE ADD-ON', $titleFont, $white, $titleRect, $format)
                    $graphics.DrawString('PUBLIC API 1.2  |  PLAYABLE SDK  |  SOURCE INCLUDED', $subFont, $muted, $subRect, $format)
                }
                finally {
                    $format.Dispose()
                }
            }
            finally {
                $titleFont.Dispose()
                $subFont.Dispose()
                $muted.Dispose()
                $white.Dispose()
                $accent.Dispose()
                $edge.Dispose()
                $band.Dispose()
            }
            Save-Png -Bitmap $canvas -Path $OutputPath
        }
        finally {
            $graphics.Dispose()
            $canvas.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}

function New-SealIcon {
    param([string]$OutputPath)

    $bitmap = New-Object System.Drawing.Bitmap 128, 128, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $shadow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(90, 0, 0, 0))
        $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 193, 145, 62))
        $goldLight = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 241, 204, 118))
        $teal = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 37, 139, 139))
        $dark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 21, 38, 40))
        $outline = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 8, 17, 19)), 6
        $ring = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 93, 222, 210)), 5
        $link = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 239, 217, 156)), 7
        try {
            $graphics.FillEllipse($shadow, 20, 25, 94, 94)
            $graphics.FillEllipse($gold, 12, 10, 100, 100)
            $graphics.DrawEllipse($outline, 12, 10, 100, 100)
            $graphics.FillEllipse($goldLight, 21, 19, 82, 82)
            $graphics.FillEllipse($dark, 29, 27, 66, 66)
            $graphics.DrawEllipse($ring, 30, 28, 64, 64)
            $graphics.DrawArc($link, 38, 45, 34, 28, 35, 285)
            $graphics.DrawArc($link, 57, 45, 34, 28, 215, 285)
            $graphics.FillPolygon($teal, @(
                (New-Object System.Drawing.Point 22, 92),
                (New-Object System.Drawing.Point 39, 101),
                (New-Object System.Drawing.Point 34, 119),
                (New-Object System.Drawing.Point 54, 106),
                (New-Object System.Drawing.Point 64, 113),
                (New-Object System.Drawing.Point 75, 106),
                (New-Object System.Drawing.Point 95, 119),
                (New-Object System.Drawing.Point 90, 101),
                (New-Object System.Drawing.Point 106, 92),
                (New-Object System.Drawing.Point 82, 84),
                (New-Object System.Drawing.Point 47, 84)))
        }
        finally {
            $link.Dispose(); $ring.Dispose(); $outline.Dispose(); $dark.Dispose()
            $teal.Dispose(); $goldLight.Dispose(); $gold.Dispose(); $shadow.Dispose()
        }
        Save-Png -Bitmap $bitmap -Path $OutputPath
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function New-LedgerIcon {
    param([string]$OutputPath)

    $bitmap = New-Object System.Drawing.Bitmap 128, 128, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $shadow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(95, 0, 0, 0))
        $body = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 28, 43, 48))
        $panel = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 42, 69, 73))
        $accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 57, 190, 184))
        $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 211, 166, 76))
        $outline = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 7, 14, 16)), 6
        $line = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 124, 224, 214)), 4
        $bodyPath = New-RoundedRectanglePath -Rect (New-Object System.Drawing.RectangleF 17, 12, 94, 104) -Radius 10
        $panelPath = New-RoundedRectanglePath -Rect (New-Object System.Drawing.RectangleF 31, 28, 65, 69) -Radius 5
        try {
            $graphics.FillEllipse($shadow, 18, 99, 94, 22)
            $graphics.FillPath($body, $bodyPath)
            $graphics.DrawPath($outline, $bodyPath)
            $graphics.FillRectangle($accent, 20, 25, 10, 78)
            $graphics.FillPath($panel, $panelPath)
            $graphics.DrawLine($line, 42, 44, 84, 44)
            $graphics.DrawLine($line, 42, 59, 76, 59)
            $graphics.DrawLine($line, 42, 74, 84, 74)
            $graphics.FillRectangle($gold, 76, 91, 27, 14)
            $graphics.FillEllipse($accent, 84, 94, 7, 7)
        }
        finally {
            $panelPath.Dispose(); $bodyPath.Dispose(); $line.Dispose(); $outline.Dispose()
            $gold.Dispose(); $accent.Dispose(); $panel.Dispose(); $body.Dispose(); $shadow.Dispose()
        }
        Save-Png -Bitmap $bitmap -Path $OutputPath
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

New-Preview -InputPath $sourcePath -OutputPath $previewPath
Copy-Item -LiteralPath $previewPath -Destination $workshopCoverPath -Force
New-SealIcon -OutputPath $sealPath
New-LedgerIcon -OutputPath $ledgerPath

Write-Host "[Example Add-on Artwork] Preview: $previewPath"
Write-Host "[Example Add-on Artwork] Workshop cover: $workshopCoverPath"
Write-Host "[Example Add-on Artwork] Item icons: $sealPath; $ledgerPath"
