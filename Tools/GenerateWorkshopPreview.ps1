param(
    [string]$EnglishSource = (Join-Path $PSScriptRoot '..\Workshop\Artwork\Source\PrisonerDiplomacy-cover-en-original.png'),
    [string]$ChineseSource = (Join-Path $PSScriptRoot '..\Workshop\Artwork\Source\PrisonerDiplomacy-cover-zh-TW-original.png'),
    [string]$ArtworkDirectory = (Join-Path $PSScriptRoot '..\Workshop\Artwork'),
    [string]$PreviewPath = (Join-Path $PSScriptRoot '..\About\Preview.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-CoverStringFormat {
    $format = [System.Drawing.StringFormat]::GenericTypographic.Clone()
    $format.FormatFlags = $format.FormatFlags -bor [System.Drawing.StringFormatFlags]::NoWrap
    return $format
}

function Draw-CenteredSegments {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Font]$Font,
        [System.Drawing.StringFormat]$Format,
        [object[]]$Segments,
        [float]$CenterX,
        [float]$Y
    )

    $widths = @()
    $totalWidth = 0.0
    foreach ($segment in $Segments) {
        $size = $Graphics.MeasureString($segment.Text, $Font, 2000, $Format)
        $widths += $size.Width
        $totalWidth += $size.Width
    }

    $x = $CenterX - ($totalWidth / 2.0)
    $shadow = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(230, 0, 0, 0))
    try {
        for ($i = 0; $i -lt $Segments.Count; $i++) {
            $Graphics.DrawString($Segments[$i].Text, $Font, $shadow, $x + 2, $Y + 2, $Format)
            $x += $widths[$i]
        }

        $x = $CenterX - ($totalWidth / 2.0)
        for ($i = 0; $i -lt $Segments.Count; $i++) {
            $brush = New-Object System.Drawing.SolidBrush($Segments[$i].Color)
            try {
                $Graphics.DrawString($Segments[$i].Text, $Font, $brush, $x, $Y, $Format)
            }
            finally {
                $brush.Dispose()
            }
            $x += $widths[$i]
        }
    }
    finally {
        $shadow.Dispose()
    }
}

function New-CorrectedCover {
    param(
        [string]$SourcePath,
        [string]$OutputPath,
        [string]$FontName,
        [float]$FontSize,
        [object[]]$FirstLine,
        [object[]]$SecondLine,
        [int]$BandY = 116,
        [int]$BandHeight = 72,
        [float]$FirstLineY = 121,
        [float]$SecondLineY = 151,
        [int]$OutputWidth = 860,
        [int]$OutputHeight = 480
    )

    $resolvedSource = [System.IO.Path]::GetFullPath($SourcePath)
    $resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
    if (-not (Test-Path -LiteralPath $resolvedSource)) {
        throw "Cover source not found: $resolvedSource"
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resolvedOutput) | Out-Null
    $source = [System.Drawing.Image]::FromFile($resolvedSource)
    $bitmap = New-Object System.Drawing.Bitmap($source.Width, $source.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.DrawImageUnscaled($source, 0, 0)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

        $bandRect = New-Object System.Drawing.Rectangle(154, $BandY, 1068, $BandHeight)
        $band = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $bandRect,
            [System.Drawing.Color]::FromArgb(255, 5, 15, 18),
            [System.Drawing.Color]::FromArgb(255, 7, 25, 28),
            90.0
        )
        $topLine = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(90, 62, 196, 207), 1)
        $bottomLine = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(120, 35, 118, 127), 1)
        try {
            $graphics.FillRectangle($band, $bandRect)
            $graphics.DrawLine($topLine, $bandRect.Left, $bandRect.Top, $bandRect.Right, $bandRect.Top)
            $graphics.DrawLine($bottomLine, $bandRect.Left, $bandRect.Bottom - 1, $bandRect.Right, $bandRect.Bottom - 1)
        }
        finally {
            $bottomLine.Dispose()
            $topLine.Dispose()
            $band.Dispose()
        }

        $font = New-Object System.Drawing.Font($FontName, $FontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $format = New-CoverStringFormat
        try {
            Draw-CenteredSegments -Graphics $graphics -Font $font -Format $format -Segments $FirstLine -CenterX 688 -Y $FirstLineY
            if ($SecondLine.Count -gt 0) {
                Draw-CenteredSegments -Graphics $graphics -Font $font -Format $format -Segments $SecondLine -CenterX 688 -Y $SecondLineY
            }
        }
        finally {
            $format.Dispose()
            $font.Dispose()
        }

        $outputBitmap = New-Object System.Drawing.Bitmap($OutputWidth, $OutputHeight, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $outputGraphics = [System.Drawing.Graphics]::FromImage($outputBitmap)
        try {
            $outputGraphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $outputGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $outputGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $outputGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $outputGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $outputGraphics.DrawImage($bitmap, 0, 0, $OutputWidth, $OutputHeight)
            $outputBitmap.Save($resolvedOutput, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $outputGraphics.Dispose()
            $outputBitmap.Dispose()
        }

        $outputLength = (Get-Item -LiteralPath $resolvedOutput).Length
        if ($outputLength -ge 1MB) {
            throw "Steam Workshop preview must be smaller than 1 MB: $resolvedOutput ($outputLength bytes)"
        }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
        $source.Dispose()
    }
}

$artworkRoot = [System.IO.Path]::GetFullPath($ArtworkDirectory)
$englishOutput = Join-Path $artworkRoot 'PrisonerDiplomacy-cover-en.png'
$chineseOutput = Join-Path $artworkRoot 'PrisonerDiplomacy-cover-zh-TW.png'
$white = [System.Drawing.Color]::FromArgb(255, 232, 234, 232)
$cyan = [System.Drawing.Color]::FromArgb(255, 74, 190, 211)

New-CorrectedCover `
    -SourcePath $EnglishSource `
    -OutputPath $englishOutput `
    -FontName 'Segoe UI Semibold' `
    -FontSize 25 `
    -FirstLine @(
        [pscustomobject]@{ Text = 'CAPTIVES ARE MORE THAN SPARE ORGANS AND LEATHER ARMCHAIRS.'; Color = $white }
    ) `
    -SecondLine @(
        [pscustomobject]@{ Text = "THEY ARE THE RIM'S"; Color = $white },
        [pscustomobject]@{ Text = ' MOST VALUABLE DIPLOMATIC BARGAINING CHIPS.'; Color = $cyan }
    )

New-CorrectedCover `
    -SourcePath $ChineseSource `
    -OutputPath $chineseOutput `
    -FontName 'Noto Sans TC Black' `
    -FontSize 29 `
    -FirstLine @(
        [pscustomobject]@{ Text = '俘虜不只是備用器官與皮革沙發，'; Color = $white },
        [pscustomobject]@{ Text = '更是邊緣世界最有價值的外交籌碼。'; Color = $cyan }
    ) `
    -SecondLine @() `
    -BandY 132 `
    -BandHeight 56 `
    -FirstLineY 143

$resolvedPreview = [System.IO.Path]::GetFullPath($PreviewPath)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resolvedPreview) | Out-Null
Copy-Item -LiteralPath $englishOutput -Destination $resolvedPreview -Force

Write-Output "English cover: $englishOutput ($((Get-Item -LiteralPath $englishOutput).Length) bytes)"
Write-Output "Chinese cover: $chineseOutput ($((Get-Item -LiteralPath $chineseOutput).Length) bytes)"
Write-Output "RimWorld preview: $resolvedPreview ($((Get-Item -LiteralPath $resolvedPreview).Length) bytes)"
