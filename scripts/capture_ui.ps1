Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

$dllPath = Resolve-Path "src/TimeChime.App/bin/Debug/net10.0-windows/TimeChime.App.dll"
[System.Reflection.Assembly]::LoadFrom($dllPath) | Out-Null

$screenshotDir = "docs/screenshots"
if (!(Test-Path $screenshotDir)) {
    New-Item -ItemType Directory -Path $screenshotDir -Force | Out-Null
}

function Render-WpfWindow([string]$theme, [string]$outputPath) {
    $thread = New-Object System.Threading.Thread([System.Threading.ThreadStart]{
        [TimeChime.App.Services.ThemeManager]::ApplyTheme($theme)

        $app = [System.Windows.Application]::Current
        if ($null -eq $app) {
            $app = New-Object System.Windows.Application
        }

        # Ensure merged dictionaries
        $app.Resources.MergedDictionaries.Clear()
        $themeDict = New-Object System.Windows.ResourceDictionary
        $themeDict.Source = New-Object System.Uri("pack://application:,,,/TimeChime.App;component/Resources/$($theme)Theme.xaml")
        $app.Resources.MergedDictionaries.Add($themeDict)

        $iconsDict = New-Object System.Windows.ResourceDictionary
        $iconsDict.Source = New-Object System.Uri("pack://application:,,,/TimeChime.App;component/Resources/Icons.xaml")
        $app.Resources.MergedDictionaries.Add($iconsDict)

        $stylesDict = New-Object System.Windows.ResourceDictionary
        $stylesDict.Source = New-Object System.Uri("pack://application:,,,/TimeChime.App;component/Resources/Styles.xaml")
        $app.Resources.MergedDictionaries.Add($stylesDict)

        $window = New-Object TimeChime.App.Views.MainWindow
        $window.Width = 860
        $window.Height = 740

        # Measure & Arrange
        $size = New-Object System.Windows.Size(860, 740)
        $window.Measure($size)
        $window.Arrange((New-Object System.Windows.Rect($size)))
        $window.UpdateLayout()

        # Render to Bitmap
        $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(860, 740, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
        $rtb.Render($window)

        $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
        $frame = [System.Windows.Media.Imaging.BitmapFrame]::Create($rtb)
        $encoder.Frames.Add($frame)

        $stream = [System.IO.File]::Create($outputPath)
        $encoder.Save($stream)
        $stream.Close()
        Write-Host "Saved: $outputPath"
    })

    $thread.SetApartmentState([System.Threading.ApartmentState]::STA)
    $thread.Start()
    $thread.Join()
}

Render-WpfWindow "Dark" "$screenshotDir/dark-theme.png"
Render-WpfWindow "Light" "$screenshotDir/light-theme.png"

