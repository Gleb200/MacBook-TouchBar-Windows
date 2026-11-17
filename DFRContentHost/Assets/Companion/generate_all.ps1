Add-Type -AssemblyName System.Drawing

$outDir = "C:\Users\Gleb\.gemini\antigravity\scratch\DFRDisplayKm\Source\DFRContentHost-master\DFRContentHost\Assets\Companion"
if (!(Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir }

$size = 64

function Draw-CompanionFrame {
    param(
        [string]$Filename,
        [float]$dx = 0,
        [float]$dy = 0,
        [string]$EyeState = "open",      # "open", "blink", "happy", "sleep"
        [string]$MouthState = "w",       # "w", "open", "sleep"
        [string]$PawState = "chest",     # "chest", "type_left", "type_right", "music", "petted", "sleep"
        [bool]$Headphones = $false,
        [bool]$Keyboard = $false,
        [string]$Particle = "none",      # "none", "heart1", "heart2", "note1", "note2", "z1", "z2"
        [float]$EarTwitch = 0
    )

    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Brushes & Pens
    $skinBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 238, 227))
    $hairBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(238, 180, 220)) # Pastel sakura
    $hairDarkBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(208, 145, 190))
    $innerEarBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 185, 205))
    $blushBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(180, 255, 130, 160))
    $clothBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(35, 35, 45))     # Obsidian hoodie
    $clothTrimBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0, 229, 255)) # Cyan trim
    $eyeDarkBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(35, 25, 55))
    $eyeBlueBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(70, 170, 250))
    $eyeSparkleBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(140, 230, 255))
    $whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $mouthPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(170, 75, 95), 1.6)
    $eyePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(40, 25, 50), 2.0)
    $neonCyanBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0, 230, 255))
    $neonPinkBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 60, 150))
    $neonYellowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 225, 50))
    $headphonePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(40, 40, 50), 3.0)

    # 1. Back hair
    $g.FillEllipse($hairDarkBrush, (14 + $dx), (18 + $dy), 36, 40)

    # 2. Cat ears
    $leftEar = @(
        (New-Object System.Drawing.PointF((15 + $dx + $EarTwitch), (22 + $dy))),
        (New-Object System.Drawing.PointF((19 + $dx + $EarTwitch * 1.5), (6 + $dy))),
        (New-Object System.Drawing.PointF((29 + $dx), (18 + $dy)))
    )
    $rightEar = @(
        (New-Object System.Drawing.PointF((35 + $dx), (18 + $dy))),
        (New-Object System.Drawing.PointF((45 + $dx), (6 + $dy))),
        (New-Object System.Drawing.PointF((49 + $dx), (22 + $dy)))
    )
    $g.FillPolygon($hairBrush, $leftEar)
    $g.FillPolygon($hairBrush, $rightEar)

    # Inner ears
    $leftInner = @(
        (New-Object System.Drawing.PointF((18 + $dx + $EarTwitch), (20 + $dy))),
        (New-Object System.Drawing.PointF((21 + $dx + $EarTwitch * 1.3), (10 + $dy))),
        (New-Object System.Drawing.PointF((27 + $dx), (18 + $dy)))
    )
    $rightInner = @(
        (New-Object System.Drawing.PointF((37 + $dx), (18 + $dy))),
        (New-Object System.Drawing.PointF((43 + $dx), (10 + $dy))),
        (New-Object System.Drawing.PointF((46 + $dx), (20 + $dy)))
    )
    $g.FillPolygon($innerEarBrush, $leftInner)
    $g.FillPolygon($innerEarBrush, $rightInner)

    # 3. Head (Face)
    $g.FillEllipse($skinBrush, (17 + $dx), (16 + $dy), 30, 28)

    # 4. Body (Hoodie)
    $g.FillEllipse($clothBrush, (19 + $dx), (40 + $dy), 26, 22)
    $g.FillEllipse($clothTrimBrush, (28 + $dx), (42 + $dy), 8, 4)

    # 5. Hair bangs & side hair
    $g.FillEllipse($hairBrush, (14 + $dx), (14 + $dy), 36, 20)
    $g.FillEllipse($hairBrush, (13 + $dx), (22 + $dy), 10, 22)
    $g.FillEllipse($hairBrush, (41 + $dx), (22 + $dy), 10, 22)

    # Star hairpin
    $g.FillEllipse($neonYellowBrush, (18 + $dx), (20 + $dy), 6, 6)

    # 6. Headphones (Music Mode)
    if ($Headphones) {
        $g.DrawArc($headphonePen, (16 + $dx), (10 + $dy), 32, 28, 180, 180)
        # Left neon earcup
        $g.FillEllipse($clothBrush, (11 + $dx), (22 + $dy), 8, 16)
        $g.FillEllipse($neonCyanBrush, (12 + $dx), (25 + $dy), 5, 10)
        # Right neon earcup
        $g.FillEllipse($clothBrush, (45 + $dx), (22 + $dy), 8, 16)
        $g.FillEllipse($neonCyanBrush, (47 + $dx), (25 + $dy), 5, 10)
    }

    # 7. Eyes
    if ($EyeState -eq "open") {
        # Left eye
        $g.FillEllipse($eyeDarkBrush, (23 + $dx), (25 + $dy), 6, 8)
        $g.FillEllipse($eyeBlueBrush, (24 + $dx), (28 + $dy), 4, 5)
        $g.FillEllipse($whiteBrush, (24 + $dx), (26 + $dy), 2.5, 3)
        $g.FillEllipse($eyeSparkleBrush, (26 + $dx), (30 + $dy), 1.5, 1.5)

        # Right eye
        $g.FillEllipse($eyeDarkBrush, (35 + $dx), (25 + $dy), 6, 8)
        $g.FillEllipse($eyeBlueBrush, (36 + $dx), (28 + $dy), 4, 5)
        $g.FillEllipse($whiteBrush, (36 + $dx), (26 + $dy), 2.5, 3)
        $g.FillEllipse($eyeSparkleBrush, (38 + $dx), (30 + $dy), 1.5, 1.5)
    }
    elseif ($EyeState -eq "blink") {
        # Closed curved resting eyes ( ◡ ◡ )
        $g.DrawArc($eyePen, (23 + $dx), (27 + $dy), 6, 4, 0, 180)
        $g.DrawArc($eyePen, (35 + $dx), (27 + $dy), 6, 4, 0, 180)
    }
    elseif ($EyeState -eq "happy") {
        # Ecstatic closed anime angle eyes ( > ◡ < )
        $leftPoints = @(
            (New-Object System.Drawing.PointF((23 + $dx), (26 + $dy))),
            (New-Object System.Drawing.PointF((28 + $dx), (28 + $dy))),
            (New-Object System.Drawing.PointF((23 + $dx), (30 + $dy)))
        )
        $rightPoints = @(
            (New-Object System.Drawing.PointF((41 + $dx), (26 + $dy))),
            (New-Object System.Drawing.PointF((36 + $dx), (28 + $dy))),
            (New-Object System.Drawing.PointF((41 + $dx), (30 + $dy)))
        )
        $g.DrawLines($eyePen, $leftPoints)
        $g.DrawLines($eyePen, $rightPoints)
    }
    elseif ($EyeState -eq "sleep") {
        # Straight sleeping lines ( - ◡ - )
        $g.DrawLine($eyePen, (23 + $dx), (28 + $dy), (29 + $dx), (28 + $dy))
        $g.DrawLine($eyePen, (35 + $dx), (28 + $dy), (41 + $dx), (28 + $dy))
    }

    # Blush
    $g.FillEllipse($blushBrush, (20 + $dx), (31 + $dy), 5, 3)
    $g.FillEllipse($blushBrush, (39 + $dx), (31 + $dy), 5, 3)

    # 8. Mouth
    if ($MouthState -eq "w") {
        $g.DrawArc($mouthPen, (29 + $dx), (31 + $dy), 3, 3, 0, 180)
        $g.DrawArc($mouthPen, (32 + $dx), (31 + $dy), 3, 3, 0, 180)
    }
    elseif ($MouthState -eq "open") {
        $g.FillEllipse($mouthPen.Brush, (30 + $dx), (31 + $dy), 4, 4)
    }
    elseif ($MouthState -eq "sleep") {
        $g.DrawLine($mouthPen, (30 + $dx), (33 + $dy), (34 + $dx), (33 + $dy))
    }

    # 9. Keyboard (Typing Mode)
    if ($Keyboard) {
        # Base cyber keyboard
        $kbBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(25, 25, 35))
        $g.FillRectangle($kbBrush, 14, 50, 36, 12)
        $kbOutline = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(60, 60, 80), 1.0)
        $g.DrawRectangle($kbOutline, 14, 50, 36, 12)

        # Illuminated glowing key rows
        $g.FillRectangle($neonCyanBrush, 16, 52, 6, 3)
        $g.FillRectangle($neonPinkBrush, 24, 52, 6, 3)
        $g.FillRectangle($neonYellowBrush, 32, 52, 6, 3)
        $g.FillRectangle($neonCyanBrush, 40, 52, 8, 3)

        $g.FillRectangle($neonPinkBrush, 16, 57, 10, 3)
        $g.FillRectangle($neonCyanBrush, 28, 57, 12, 3)
        $g.FillRectangle($neonYellowBrush, 42, 57, 6, 3)
    }

    # 10. Paws
    if ($PawState -eq "chest") {
        $g.FillEllipse($skinBrush, (25 + $dx), (45 + $dy), 6, 5)
        $g.FillEllipse($skinBrush, (33 + $dx), (45 + $dy), 6, 5)
    }
    elseif ($PawState -eq "type_left") {
        # Left paw tapping down on keyboard, right paw slightly raised
        $g.FillEllipse($skinBrush, 20, 49, 7, 6) # Left tapping
        $g.FillEllipse($skinBrush, 36, 44, 7, 6) # Right raised
        # Keystroke spark over left key
        $g.FillEllipse($neonYellowBrush, 18, 47, 4, 4)
    }
    elseif ($PawState -eq "type_right") {
        # Right paw tapping down on keyboard, left paw slightly raised
        $g.FillEllipse($skinBrush, 20, 44, 7, 6) # Left raised
        $g.FillEllipse($skinBrush, 36, 49, 7, 6) # Right tapping
        # Keystroke spark over right key
        $g.FillEllipse($neonYellowBrush, 39, 47, 4, 4)
    }
    elseif ($PawState -eq "music") {
        # Paws rocking near headphones/chest
        $g.FillEllipse($skinBrush, (22 + $dx), (42 + $dy), 6, 6)
        $g.FillEllipse($skinBrush, (36 + $dx), (42 + $dy), 6, 6)
    }
    elseif ($PawState -eq "petted") {
        # Paws raised up in joy with cute pink paw pads
        $g.FillEllipse($skinBrush, (16 + $dx), (36 + $dy), 8, 8)
        $g.FillEllipse($innerEarBrush, (18 + $dx), (38 + $dy), 4, 4)
        $g.FillEllipse($skinBrush, (40 + $dx), (36 + $dy), 8, 8)
        $g.FillEllipse($innerEarBrush, (42 + $dx), (38 + $dy), 4, 4)
    }
    elseif ($PawState -eq "sleep") {
        # Paws tucked under cheek
        $g.FillEllipse($skinBrush, (28 + $dx), (42 + $dy), 10, 6)
    }

    # 11. Floating Particles
    if ($Particle -eq "heart1") {
        # Single glowing magenta heart
        $g.FillEllipse($neonPinkBrush, 43, 6, 5, 5)
        $g.FillEllipse($neonPinkBrush, 47, 6, 5, 5)
        $tri = @(
            (New-Object System.Drawing.PointF(42, 8)),
            (New-Object System.Drawing.PointF(53, 8)),
            (New-Object System.Drawing.PointF(47.5, 14))
        )
        $g.FillPolygon($neonPinkBrush, $tri)
    }
    elseif ($Particle -eq "heart2") {
        # Double hearts (one big, one small)
        $g.FillEllipse($neonPinkBrush, 43, 4, 5, 5)
        $g.FillEllipse($neonPinkBrush, 47, 4, 5, 5)
        $tri1 = @(
            (New-Object System.Drawing.PointF(42, 6)),
            (New-Object System.Drawing.PointF(53, 6)),
            (New-Object System.Drawing.PointF(47.5, 12))
        )
        $g.FillPolygon($neonPinkBrush, $tri1)

        $g.FillEllipse($neonPinkBrush, 10, 10, 4, 4)
        $g.FillEllipse($neonPinkBrush, 13, 10, 4, 4)
        $tri2 = @(
            (New-Object System.Drawing.PointF(9, 12)),
            (New-Object System.Drawing.PointF(18, 12)),
            (New-Object System.Drawing.PointF(13.5, 17))
        )
        $g.FillPolygon($neonPinkBrush, $tri2)
    }
    elseif ($Particle -eq "note1") {
        # Musical note ♪
        $noteFont = New-Object System.Drawing.Font("Segoe UI Symbol", 12, [System.Drawing.FontStyle]::Bold)
        $g.DrawString("♪", $noteFont, $neonCyanBrush, 44, 4)
    }
    elseif ($Particle -eq "note2") {
        # Musical note ♫
        $noteFont = New-Object System.Drawing.Font("Segoe UI Symbol", 13, [System.Drawing.FontStyle]::Bold)
        $g.DrawString("♫", $noteFont, $neonPinkBrush, 44, 2)
    }
    elseif ($Particle -eq "z1") {
        # Sleep z
        $zFont = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
        $g.DrawString("z", $zFont, $neonCyanBrush, 45, 12)
    }
    elseif ($Particle -eq "z2") {
        # Sleep Z z
        $zFont1 = New-Object System.Drawing.Font("Segoe UI", 8, [System.Drawing.FontStyle]::Bold)
        $zFont2 = New-Object System.Drawing.Font("Segoe UI", 11, [System.Drawing.FontStyle]::Bold)
        $g.DrawString("z", $zFont1, $neonCyanBrush, 44, 16)
        $g.DrawString("Z", $zFont2, $neonCyanBrush, 48, 6)
    }

    $outPath = "$outDir\$Filename"
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
    Write-Host "Created $Filename"
}

# Generate all 11 animation frames:
# IDLE
Draw-CompanionFrame -Filename "idle_1.png" -dx 0 -dy 0 -EyeState "open" -MouthState "w" -PawState "chest"
Draw-CompanionFrame -Filename "idle_2.png" -dx 0 -dy 0 -EyeState "blink" -MouthState "w" -PawState "chest"
Draw-CompanionFrame -Filename "idle_3.png" -dx 0 -dy 0 -EyeState "open" -MouthState "w" -PawState "chest" -EarTwitch -2.0

# TYPING (Bongo mode)
Draw-CompanionFrame -Filename "type_1.png" -dx 0 -dy 1 -EyeState "happy" -MouthState "open" -PawState "type_left" -Keyboard $true
Draw-CompanionFrame -Filename "type_2.png" -dx 0 -dy 1 -EyeState "happy" -MouthState "open" -PawState "type_right" -Keyboard $true

# MUSIC (DJ vibing mode)
Draw-CompanionFrame -Filename "music_1.png" -dx -1 -dy 1 -EyeState "happy" -MouthState "w" -PawState "music" -Headphones $true -Particle "note1"
Draw-CompanionFrame -Filename "music_2.png" -dx 1 -dy -1 -EyeState "happy" -MouthState "open" -PawState "music" -Headphones $true -Particle "note2"

# PETTED / CLICKED
Draw-CompanionFrame -Filename "pet_1.png" -dx 0 -dy 0 -EyeState "happy" -MouthState "open" -PawState "petted" -Particle "heart1"
Draw-CompanionFrame -Filename "pet_2.png" -dx 0 -dy -3 -EyeState "happy" -MouthState "open" -PawState "petted" -Particle "heart2"

# SLEEPING
Draw-CompanionFrame -Filename "sleep_1.png" -dx 0 -dy 1 -EyeState "sleep" -MouthState "sleep" -PawState "sleep" -Particle "z1"
Draw-CompanionFrame -Filename "sleep_2.png" -dx 0 -dy 2 -EyeState "sleep" -MouthState "sleep" -PawState "sleep" -Particle "z2"

Write-Host "All frames generated successfully!"
