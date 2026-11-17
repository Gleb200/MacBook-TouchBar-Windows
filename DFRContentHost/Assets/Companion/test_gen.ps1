Add-Type -AssemblyName System.Drawing

function Generate-CompanionSprites {
    $outDir = "C:\Users\Gleb\.gemini\antigravity\scratch\DFRDisplayKm\Source\DFRContentHost-master\DFRContentHost\Assets\Companion"
    if (!(Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir }

    $size = 64

    # Helper function to create base character graphics
    function New-BaseBitmap {
        $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)
        return @{ Bitmap = $bmp; Graphics = $g }
    }

    # Helper brushes and colors
    $skinColor = [System.Drawing.Color]::FromArgb(255, 238, 227)
    $skinBrush = New-Object System.Drawing.SolidBrush($skinColor)
    $hairColor = [System.Drawing.Color]::FromArgb(235, 175, 215)
    $hairBrush = New-Object System.Drawing.SolidBrush($hairColor)
    $hairDarkColor = [System.Drawing.Color]::FromArgb(205, 140, 185)
    $hairDarkBrush = New-Object System.Drawing.SolidBrush($hairDarkColor)
    $innerEarColor = [System.Drawing.Color]::FromArgb(255, 180, 200)
    $innerEarBrush = New-Object System.Drawing.SolidBrush($innerEarColor)
    $blushColor = [System.Drawing.Color]::FromArgb(180, 255, 130, 160)
    $blushBrush = New-Object System.Drawing.SolidBrush($blushColor)
    $clothColor = [System.Drawing.Color]::FromArgb(38, 38, 48)
    $clothBrush = New-Object System.Drawing.SolidBrush($clothColor)
    $clothTrimColor = [System.Drawing.Color]::FromArgb(0, 229, 255)
    $clothTrimBrush = New-Object System.Drawing.SolidBrush($clothTrimColor)
    $eyeDark = [System.Drawing.Color]::FromArgb(30, 20, 50)
    $eyeDarkBrush = New-Object System.Drawing.SolidBrush($eyeDark)
    $eyeBlue = [System.Drawing.Color]::FromArgb(80, 160, 240)
    $eyeBlueBrush = New-Object System.Drawing.SolidBrush($eyeBlue)
    $whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $outlinePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(60, 30, 60), 1.5)
    $mouthPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(160, 70, 90), 1.6)

    # 1. IDLE 1 (Normal resting, alert, sweet smile)
    $obj = New-BaseBitmap
    $g = $obj.Graphics

    # Back hair
    $g.FillEllipse($hairDarkBrush, 14, 18, 36, 40)

    # Cat ears
    $leftEar = @(
        (New-Object System.Drawing.PointF(15, 22)),
        (New-Object System.Drawing.PointF(20, 6)),
        (New-Object System.Drawing.PointF(29, 18))
    )
    $rightEar = @(
        (New-Object System.Drawing.PointF(35, 18)),
        (New-Object System.Drawing.PointF(44, 6)),
        (New-Object System.Drawing.PointF(49, 22))
    )
    $g.FillPolygon($hairBrush, $leftEar)
    $g.FillPolygon($hairBrush, $rightEar)

    # Inner ears
    $leftInner = @(
        (New-Object System.Drawing.PointF(18, 20)),
        (New-Object System.Drawing.PointF(22, 10)),
        (New-Object System.Drawing.PointF(27, 18))
    )
    $rightInner = @(
        (New-Object System.Drawing.PointF(37, 18)),
        (New-Object System.Drawing.PointF(42, 10)),
        (New-Object System.Drawing.PointF(46, 20))
    )
    $g.FillPolygon($innerEarBrush, $leftInner)
    $g.FillPolygon($innerEarBrush, $rightInner)

    # Head (Face)
    $g.FillEllipse($skinBrush, 17, 16, 30, 28)

    # Body (Hoodie)
    $g.FillEllipse($clothBrush, 19, 40, 26, 22)
    $g.FillEllipse($clothTrimBrush, 28, 42, 8, 4)

    # Hair bangs & side hair
    $g.FillEllipse($hairBrush, 14, 14, 36, 20)
    $g.FillEllipse($hairBrush, 13, 22, 10, 22)
    $g.FillEllipse($hairBrush, 41, 22, 10, 22)

    # Star hairpin
    $starBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 220, 50))
    $g.FillEllipse($starBrush, 18, 20, 6, 6)

    # Eyes (Big anime style)
    # Left eye
    $g.FillEllipse($eyeDarkBrush, 23, 25, 6, 8)
    $g.FillEllipse($eyeBlueBrush, 24, 28, 4, 5)
    $g.FillEllipse($whiteBrush, 24, 26, 2.5, 3)
    $g.FillEllipse($whiteBrush, 26, 30, 1.5, 1.5)

    # Right eye
    $g.FillEllipse($eyeDarkBrush, 35, 25, 6, 8)
    $g.FillEllipse($eyeBlueBrush, 36, 28, 4, 5)
    $g.FillEllipse($whiteBrush, 36, 26, 2.5, 3)
    $g.FillEllipse($whiteBrush, 38, 30, 1.5, 1.5)

    # Blush
    $g.FillEllipse($blushBrush, 20, 31, 5, 3)
    $g.FillEllipse($blushBrush, 39, 31, 5, 3)

    # Mouth (cute small cat w / smile)
    $g.DrawArc($mouthPen, 29, 31, 3, 3, 0, 180)
    $g.DrawArc($mouthPen, 32, 31, 3, 3, 0, 180)

    # Paws resting at chest
    $g.FillEllipse($skinBrush, 25, 45, 6, 5)
    $g.FillEllipse($skinBrush, 33, 45, 6, 5)

    $obj.Bitmap.Save("$outDir\idle_1.png", [System.Drawing.Imaging.ImageFormat]::Png)
    $obj.Graphics.Dispose()
    $obj.Bitmap.Dispose()

    Write-Host "Generated idle_1.png successfully"
}

Generate-CompanionSprites
