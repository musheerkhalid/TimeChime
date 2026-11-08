using System.Runtime.InteropServices;
using System.Threading;

namespace TimeChime.App.Services;

public sealed class SingleInstanceMutex : IDisposable
{
    private const string MutexName = @"Local\TimeChime_App_SingleInstance_Mutex";
    private Mutex? _mutex;
    private bool _hasHandle;

    public bool IsFirstInstance { get; private set; }

    public bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            _hasHandle = createdNew;
            IsFirstInstance = createdNew;
            return createdNew;
        }
        catch (AbandonedMutexException)
        {
            _hasHandle = true;
            IsFirstInstance = true;
            return true;
        }
        catch
        {
            // If mutex creation fails due to restrictive sandbox/environment, allow single instance to proceed
            IsFirstInstance = true;
            return true;
        }
    }

    public static uint ShowMainWindowMessage => RegisterWindowMessage("TimeChime_ShowMainWindow");

    public static void BringExistingWindowToFront(string windowTitle)
    {
        try
        {
            uint msg = ShowMainWindowMessage;
            if (msg != 0)
            {
                PostMessage((IntPtr)HWND_BROADCAST, msg, IntPtr.Zero, IntPtr.Zero);
            }

            IntPtr hWnd = FindWindow(null, windowTitle);
            if (hWnd != IntPtr.Zero)
            {
                if (msg != 0)
                {
                    PostMessage(hWnd, msg, IntPtr.Zero, IntPtr.Zero);
                }
                ShowWindow(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
            }
        }
        catch
        {
            // Ignore if window lookup fails
        }
    }

    public void Dispose()
    {
        if (_mutex != null)
        {
            if (_hasHandle)
            {
                try { _mutex.ReleaseMutex(); } catch { }
            }
            _mutex.Dispose();
            _mutex = null;
        }
    }

    private const int SW_RESTORE = 9;
    private const int HWND_BROADCAST = 0xFFFF;

    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
