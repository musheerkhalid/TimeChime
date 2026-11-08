using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using TimeChime.App.ViewModels;

namespace TimeChime.App.Services;

public sealed class SystemTrayManager : IDisposable
{
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 101;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;

    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;

    private readonly Window _mainWindow;
    private readonly MainViewModel _viewModel;
    private HwndSource? _hwndSource;
    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _isAdded = false;
    private ContextMenu? _trayContextMenu;

    public SystemTrayManager(Window mainWindow, MainViewModel viewModel)
    {
        _mainWindow = mainWindow;
        _viewModel = viewModel;
    }

    public void Initialize()
    {
        var helper = new WindowInteropHelper(_mainWindow);
        _hwnd = helper.EnsureHandle();

        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);

        LoadIconHandle();
        AddTrayIcon();

        // Intercept close button to minimize to tray when configured
        _mainWindow.Closing += OnWindowClosing;
        _mainWindow.StateChanged += OnWindowStateChanged;
    }

    private void LoadIconHandle()
    {
        try
        {
            string icoPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (File.Exists(icoPath))
            {
                _hIcon = LoadImage(IntPtr.Zero, icoPath, 1 /* IMAGE_ICON */, 16, 16, 0x00000010 /* LR_LOADFROMFILE */);
            }

            if (_hIcon == IntPtr.Zero)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    ExtractIconEx(exePath, 0, out _, out _hIcon, 1);
                }
            }
        }
        catch
        {
            _hIcon = IntPtr.Zero;
        }
    }

    private void AddTrayIcon()
    {
        if (_hwnd == IntPtr.Zero) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "Time Chime — Spoken Clock Chime"
        };

        _isAdded = Shell_NotifyIcon(NIM_ADD, ref nid);
    }

    public void UpdateTooltip(string status)
    {
        if (!_isAdded || _hwnd == IntPtr.Zero) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_TIP,
            szTip = $"Time Chime — {status}"
        };

        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    private void RemoveTrayIcon()
    {
        if (!_isAdded || _hwnd == IntPtr.Zero) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1
        };

        Shell_NotifyIcon(NIM_DELETE, ref nid);
        _isAdded = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            int eventId = lParam.ToInt32();
            if (eventId == WM_LBUTTONDBLCLK || eventId == WM_LBUTTONUP)
            {
                ShowMainWindow();
                handled = true;
            }
            else if (eventId == WM_RBUTTONUP)
            {
                ShowTrayContextMenu();
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void ShowMainWindow()
    {
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Show();
        _mainWindow.Activate();
        _mainWindow.Focus();
    }

    private void ShowTrayContextMenu()
    {
        _trayContextMenu = new ContextMenu
        {
            Placement = PlacementMode.MousePoint
        };

        // 1. Open Time Chime (Bold)
        var openItem = new MenuItem
        {
            Header = "Open Time Chime",
            FontWeight = FontWeights.Bold
        };
        openItem.Click += (s, e) => ShowMainWindow();
        _trayContextMenu.Items.Add(openItem);

        _trayContextMenu.Items.Add(new Separator());

        // 2. Enabled Toggle
        var enabledItem = new MenuItem
        {
            Header = "Master Chime Enabled",
            IsCheckable = true,
            IsChecked = _viewModel.IsEnabled
        };
        enabledItem.Click += (s, e) => _viewModel.ToggleMasterCommand.Execute(null);
        _trayContextMenu.Items.Add(enabledItem);

        // 3. Pause / Resume Toggle
        var pauseItem = new MenuItem
        {
            Header = _viewModel.IsPaused ? "Resume Announcements" : "Pause Announcements"
        };
        pauseItem.Click += (s, e) => _viewModel.TogglePauseCommand.Execute(null);
        _trayContextMenu.Items.Add(pauseItem);

        // 4. Preview Voice
        var previewItem = new MenuItem
        {
            Header = "Preview Selected Voice"
        };
        previewItem.Click += (s, e) => _viewModel.TogglePreviewCommand.Execute(null);
        _trayContextMenu.Items.Add(previewItem);

        _trayContextMenu.Items.Add(new Separator());

        // 5. Exit
        var exitItem = new MenuItem
        {
            Header = "Exit Time Chime"
        };
        exitItem.Click += (s, e) =>
        {
            _mainWindow.Closing -= OnWindowClosing;
            RemoveTrayIcon();
            Application.Current.Shutdown();
        };
        _trayContextMenu.Items.Add(exitItem);

        _trayContextMenu.IsOpen = true;
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_viewModel.MinimizeToTray)
        {
            e.Cancel = true;
            _mainWindow.Hide();
            UpdateTooltip(_viewModel.StatusBadgeText);
        }
        else
        {
            RemoveTrayIcon();
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_mainWindow.WindowState == WindowState.Minimized && _viewModel.MinimizeToTray)
        {
            _mainWindow.Hide();
        }
    }

    public void Dispose()
    {
        _mainWindow.Closing -= OnWindowClosing;
        _mainWindow.StateChanged -= OnWindowStateChanged;
        RemoveTrayIcon();

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }

        _hwndSource?.RemoveHook(WndProc);
        _hwndSource?.Dispose();
        _hwndSource = null;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", EntryPoint = "ExtractIconExW", CharSet = CharSet.Unicode)]
    private static extern int ExtractIconEx(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, int nIcons);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
