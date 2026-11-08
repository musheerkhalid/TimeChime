using System.Runtime.InteropServices;
using System.Threading;

namespace TimeChime.App.Services;

public sealed class SingleInstanceMutex : IDisposable
{
    private const string MutexName = @"Global\TimeChime_App_SingleInstance_Mutex";
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

    public static void BringExistingWindowToFront(string windowTitle)
    {
        try
        {
            IntPtr hWnd = FindWindow(null, windowTitle);
            if (hWnd != IntPtr.Zero)
            {
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

    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
