using System;
using System.Runtime.InteropServices;
using DesktopFabLauncher.Interop;

namespace DesktopFabLauncher.Services;

public class MouseActionArgs
{
    public int MessageType { get; init; }
    public NativeMethods.POINT Point { get; init; }
    public uint Timestamp { get; init; }
}

public class MouseHookService : IDisposable
{
    private const nint INJECTED_MARKER = 0x46414221; // "FAB!" as identifier

    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelMouseProc? _proc;

    public bool IsHookActive => _hookId != IntPtr.Zero;

    /// <summary>
    /// Returns true if the event should be suppressed (swallowed).
    /// </summary>
    public event Func<MouseActionArgs, bool>? MouseAction;

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;

        _proc = HookCallback;
        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _proc,
            NativeMethods.GetModuleHandle(null),
            0);
    }

    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Re-injects a right-click at the current cursor position.
    /// Must NOT be called from within the hook callback — use Dispatcher.BeginInvoke.
    /// </summary>
    public void InjectRightClick(NativeMethods.POINT pos)
    {
        var inputs = new NativeMethods.INPUT[2];

        inputs[0].type = NativeMethods.INPUT_MOUSE;
        inputs[0].mi.dwFlags = NativeMethods.MOUSEEVENTF_RIGHTDOWN;
        inputs[0].mi.dwExtraInfo = INJECTED_MARKER;

        inputs[1].type = NativeMethods.INPUT_MOUSE;
        inputs[1].mi.dwFlags = NativeMethods.MOUSEEVENTF_RIGHTUP;
        inputs[1].mi.dwExtraInfo = INJECTED_MARKER;

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

            // Skip events we injected ourselves
            if (hookStruct.dwExtraInfo == INJECTED_MARKER)
            {
                return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            var args = new MouseActionArgs
            {
                MessageType = (int)wParam,
                Point = hookStruct.pt,
                Timestamp = hookStruct.time
            };

            var handled = MouseAction?.Invoke(args) ?? false;
            if (handled)
            {
                return (IntPtr)1;
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
