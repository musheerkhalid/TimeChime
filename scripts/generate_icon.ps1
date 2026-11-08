Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

function Create-AppIcon {
    $sizes = @(256, 128, 64, 48, 32, 16)
    $pngDataList = @()

    foreach ($size in $sizes) {
        $visual = New-Object System.Windows.Media.DrawingVisual
        $dc = $visual.RenderOpen()

        # Scale factor
        $scale = $size / 256.0

        # Background: Modern rounded squircle with blue accent gradient
        $rect = New-Object System.Windows.Rect(0, 0, $size, $size)
        $cornerRadius = 48.0 * $scale

        $bgBrush = New-Object System.Windows.Media.LinearGradientBrush(
            [System.Windows.Media.Color]::FromArgb(255, 37, 99, 235), # #2563EB
            [System.Windows.Media.Color]::FromArgb(255, 29, 78, 216), # #1D4ED8
            (New-Object System.Windows.Point(0, 0)),
            (New-Object System.Windows.Point(1, 1))
        )
        $dc.DrawRoundedRectangle($bgBrush, $null, $rect, $cornerRadius, $cornerRadius)

        # Subtle inner border/ring
        $borderPen = New-Object System.Windows.Media.Pen(
            (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromArgb(60, 255, 255, 255))),
            1.5 * $scale
        )
        $innerRect = New-Object System.Windows.Rect(4 * $scale, 4 * $scale, $size - 8 * $scale, $size - 8 * $scale)
        $dc.DrawRoundedRectangle($null, $borderPen, $innerRect, $cornerRadius - 2, $cornerRadius - 2)

        # Chime Bell Path
        $bellGeometry = [System.Windows.Media.Geometry]::Parse("M12,2A3,3 0 0,0 9,5V6.29C6.67,7.38 5,9.75 5,12.5V17L3,19V20H21V19L19,17V12.5C19,9.75 17.33,7.38 15,6.29V5A3,3 0 0,0 12,2M12,4A1,1 0 0,1 13,5V6H11V5A1,1 0 0,1 12,4M7,12.5C7,9.74 9.24,7.5 12,7.5C14.76,7.5 17,9.74 17,12.5V18H7V12.5M10,21A2,2 0 0,0 12,23A2,2 0 0,0 14,21H10Z")
        
        $transformGroup = New-Object System.Windows.Media.TransformGroup
        # Original geometry is in a 24x24 box. We scale to roughly 60% of icon size.
        $targetIconSize = 150.0 * $scale
        $scaleFactor = $targetIconSize / 24.0
        $transformGroup.Children.Add((New-Object System.Windows.Media.ScaleTransform($scaleFactor, $scaleFactor)))
        $offset = ($size - $targetIconSize) / 2.0
        $transformGroup.Children.Add((New-Object System.Windows.Media.TranslateTransform($offset, $offset - (4 * $scale))))
        $bellGeometry.Transform = $transformGroup

        # Bell fill: crisp white
        $bellBrush = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromArgb(255, 255, 255, 255))
        $dc.DrawGeometry($bellBrush, $null, $bellGeometry)

        # Chime Accent Ring Dot (small golden bell clapper accent)
        $dotBrush = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromArgb(255, 251, 191, 36)) # amber #FBBF24
        $dc.DrawEllipse($dotBrush, $null, (New-Object System.Windows.Point($size / 2.0, $size / 2.0 + (32 * $scale))), 12 * $scale, 8 * $scale)

        $dc.Close()

        # Render to RenderTargetBitmap
        $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
        $rtb.Render($visual)

        # Encode to PNG
        $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
        $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
        $ms = New-Object System.IO.MemoryStream
        $encoder.Save($ms)
        $pngBytes = $ms.ToArray()
        $ms.Close()

        $pngDataList += ,@($size, $pngBytes)
    }

    # Save 256x256 PNG for WPF Window Icon
    $png256 = ($pngDataList | Where-Object { $_[0] -eq 256 })[1]
    [System.IO.File]::WriteAllBytes("src/TimeChime.App/Resources/app_icon.png", $png256)
    if (!(Test-Path "assets")) { New-Item -ItemType Directory -Path "assets" | Out-Null }
    [System.IO.File]::WriteAllBytes("assets/app_icon.png", $png256)

    # Build multi-resolution ICO file
    $icoStream = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($icoStream)

    # ICONDIR header
    $bw.Write([UInt16]0) # Reserved
    $bw.Write([UInt16]1) # Type (1 = ICO)
    $bw.Write([UInt16]$pngDataList.Count) # Number of images

    # Calculate offsets
    # Header is 6 bytes. Each entry is 16 bytes.
    $offset = 6 + ($pngDataList.Count * 16)

    foreach ($item in $pngDataList) {
        $s = $item[0]
        $bytes = $item[1]

        $bWidth = if ($s -ge 256) { 0 } else { [byte]$s }
        $bHeight = if ($s -ge 256) { 0 } else { [byte]$s }

        $bw.Write([byte]$bWidth)
        $bw.Write([byte]$bHeight)
        $bw.Write([byte]0) # Color palette count (0 = no palette)
        $bw.Write([byte]0) # Reserved
        $bw.Write([UInt16]1) # Color planes
        $bw.Write([UInt16]32) # Bits per pixel
        $bw.Write([UInt32]$bytes.Length) # Image size in bytes
        $bw.Write([UInt32]$offset) # Offset to image data

        $offset += $bytes.Length
    }

    # Write actual PNG data blocks
    foreach ($item in $pngDataList) {
        $bytes = $item[1]
        $bw.Write($bytes)
    }

    $bw.Flush()
    $icoBytes = $icoStream.ToArray()
    $bw.Close()
    $icoStream.Close()

    [System.IO.File]::WriteAllBytes("src/TimeChime.App/app.ico", $icoBytes)
    [System.IO.File]::WriteAllBytes("assets/app_icon.ico", $icoBytes)
    Write-Host "Generated app.ico and app_icon.png successfully with sizes: $($sizes -join ', ')"
}

Create-AppIcon

