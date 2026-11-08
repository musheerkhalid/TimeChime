using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TimeChime.App.Services;

namespace TimeChime.App;

public partial class App : Application
{
    private static readonly string LogFile = Path.Combine(AppContext.BaseDirectory, "startup_trace.log");
    private readonly SingleInstanceMutex _singleInstanceMutex = new();

    public App()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash_dispatcher.log"), e.Exception.ToString());
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash_domain.log"), e.ExceptionObject?.ToString());
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        File.WriteAllText(LogFile, $"OnStartup reached. BaseDir: {AppContext.BaseDirectory}\n");

        if (e.Args.Contains("--generate-icon"))
        {
            try
            {
                GenerateAppIcons();
                Console.WriteLine("App icons generated successfully.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Icon generation failed: {ex.Message}");
            }

            Shutdown(0);
            return;
        }

        if (e.Args.Contains("--screenshot"))
        {
            try
            {
                GenerateAppIcons();
                CaptureScreenshot("dark", "docs/screenshots/dark-theme.png");
                CaptureScreenshot("light", "docs/screenshots/light-theme.png");
                Console.WriteLine("Screenshots captured successfully.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Screenshot capture failed: {ex.Message}");
            }

            Shutdown(0);
            return;
        }

        bool startMinimized = e.Args.Any(a =>
            string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "-silent", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "/minimized", StringComparison.OrdinalIgnoreCase));

        if (!_singleInstanceMutex.TryAcquire())
        {
            if (!startMinimized)
            {
                SingleInstanceMutex.BringExistingWindowToFront("Time Chime — Spoken Clock Chime");
            }
            Shutdown(0);
            return;
        }

        try
        {
            File.AppendAllText(LogFile, "Instantiating MainWindow...\n");
            var mainWindow = new Views.MainWindow();
            MainWindow = mainWindow;

            if (startMinimized)
            {
                File.AppendAllText(LogFile, "Starting minimized to tray (window hidden).\n");
            }
            else
            {
                File.AppendAllText(LogFile, "Calling MainWindow.Show()...\n");
                mainWindow.Show();
                File.AppendAllText(LogFile, "MainWindow.Show() completed successfully!\n");
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash_startup.log"), ex.ToString());
            System.Windows.MessageBox.Show(ex.ToString(), "Time Chime Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex.Dispose();
        base.OnExit(e);
    }

    public static void GenerateAppIcons()
    {
        int[] sizes = [256, 128, 64, 48, 32, 16];
        var pngList = new List<(int Size, byte[] Bytes)>();

        foreach (int size in sizes)
        {
            double scale = size / 256.0;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // Background squircle with electric blue gradient
                var bgBrush = new LinearGradientBrush(
                    Color.FromRgb(37, 99, 235), // #2563EB
                    Color.FromRgb(29, 78, 216), // #1D4ED8
                    new Point(0, 0),
                    new Point(1, 1));

                double cornerRadius = 48.0 * scale;
                dc.DrawRoundedRectangle(bgBrush, null, new Rect(0, 0, size, size), cornerRadius, cornerRadius);

                // Inner subtle ring
                var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1.5 * scale);
                dc.DrawRoundedRectangle(null, borderPen, new Rect(3 * scale, 3 * scale, size - (6 * scale), size - (6 * scale)), cornerRadius - 2, cornerRadius - 2);

                // Chime Bell Path
                var geom = Geometry.Parse("M12,2A3,3 0 0,0 9,5V6.29C6.67,7.38 5,9.75 5,12.5V17L3,19V20H21V19L19,17V12.5C19,9.75 17.33,7.38 15,6.29V5A3,3 0 0,0 12,2M12,4A1,1 0 0,1 13,5V6H11V5A1,1 0 0,1 12,4M7,12.5C7,9.74 9.24,7.5 12,7.5C14.76,7.5 17,9.74 17,12.5V18H7V12.5M10,21A2,2 0 0,0 12,23A2,2 0 0,0 14,21H10Z").Clone();

                double targetIconSize = 148.0 * scale;
                double scaleFactor = targetIconSize / 24.0;
                var tg = new TransformGroup();
                tg.Children.Add(new ScaleTransform(scaleFactor, scaleFactor));
                double offset = (size - targetIconSize) / 2.0;
                tg.Children.Add(new TranslateTransform(offset, offset - (4 * scale)));
                geom.Transform = tg;

                dc.DrawGeometry(Brushes.White, null, geom);

                // Chime clapper accent dot (amber)
                var dotBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                dc.DrawEllipse(dotBrush, null, new Point(size / 2.0, (size / 2.0) + (30 * scale)), 10 * scale, 7 * scale);
            }

            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            pngList.Add((size, ms.ToArray()));
        }

        // Save PNG
        byte[] png256 = pngList.First(x => x.Size == 256).Bytes;
        SaveFileSafe("src/TimeChime.App/Resources/app_icon.png", png256);
        SaveFileSafe("assets/app_icon.png", png256);

        // Build multi-res ICO file
        using var icoStream = new MemoryStream();
        using var bw = new BinaryWriter(icoStream);
        bw.Write((ushort)0);
        bw.Write((ushort)1);
        bw.Write((ushort)pngList.Count);

        int icoOffset = 6 + (pngList.Count * 16);
        foreach (var (s, bytes) in pngList)
        {
            bw.Write(s >= 256 ? (byte)0 : (byte)s);
            bw.Write(s >= 256 ? (byte)0 : (byte)s);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((ushort)1);
            bw.Write((ushort)32);
            bw.Write((uint)bytes.Length);
            bw.Write((uint)icoOffset);
            icoOffset += bytes.Length;
        }

        foreach (var (_, bytes) in pngList)
        {
            bw.Write(bytes);
        }

        bw.Flush();
        byte[] icoBytes = icoStream.ToArray();

        SaveFileSafe("src/TimeChime.App/app.ico", icoBytes);
        SaveFileSafe("assets/app_icon.ico", icoBytes);
    }

    private static void SaveFileSafe(string relativePath, byte[] bytes)
    {
        string full = Path.GetFullPath(relativePath);
        string? dir = Path.GetDirectoryName(full);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllBytes(full, bytes);
    }

    private static void CaptureScreenshot(string theme, string relativePath)
    {
        ThemeManager.ApplyTheme(theme);

        var window = new Views.MainWindow();
        var content = (FrameworkElement)window.Content;
        window.Content = null;

        content.Width = 860;
        content.Height = 740;

        var size = new Size(860, 740);
        content.Measure(size);
        content.Arrange(new Rect(size));
        content.UpdateLayout();

        var rtb = new RenderTargetBitmap(860, 740, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(content);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        string fullPath = Path.GetFullPath(relativePath);
        string? dir = Path.GetDirectoryName(fullPath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(fullPath);
        encoder.Save(stream);
    }
}
