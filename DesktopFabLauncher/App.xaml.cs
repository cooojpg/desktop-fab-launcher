using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using DesktopFabLauncher.Commands;
using DesktopFabLauncher.Interop;
using DesktopFabLauncher.Models;
using DesktopFabLauncher.Services;
using DesktopFabLauncher.ViewModels;
using DesktopFabLauncher.Views;
using Forms = System.Windows.Forms;

namespace DesktopFabLauncher;

public partial class App : System.Windows.Application
{
    private MouseHookService? _hookService;
    private InputStateMachine? _stateMachine;
    private Forms.NotifyIcon? _notifyIcon;
    private OverlayWindow? _overlayWindow;
    private DispatcherTimer? _focusCheckTimer;
    private AppConfig? _config;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 0. Attach to parent console for debug output
        NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);

        // 1. Load config
        _config = AppConfig.Load();

        // 2. Create CommandRegistry — register all sequences of length 2-5
        var registry = new CommandRegistry();
        RegisterAllSequences(registry, _config.MaxInputCount);
        RegisterSampleMappings(registry);

        // 3. Create and start MouseHookService
        _hookService = new MouseHookService();
        _hookService.Start();

        // 4. Create InputStateMachine
        _stateMachine = new InputStateMachine(_config, registry, _hookService);

        // 5. Create OverlayViewModel
        var overlayVm = new OverlayViewModel(_stateMachine, _config);

        // 6. Create OverlayWindow (starts hidden)
        _overlayWindow = new OverlayWindow(overlayVm);
        _overlayWindow.Show();

        // 7. Focus check timer (for foreground window loss detection)
        _focusCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _focusCheckTimer.Tick += (_, _) => _stateMachine.CheckForegroundWindow();
        _focusCheckTimer.Start();

        // 8. Setup tray icon
        SetupNotifyIcon();

        // 9. Handle Ctrl+C from terminal
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
            Dispatcher.Invoke(() => Shutdown());
        };
    }

    private void SetupNotifyIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Exit", null, (_, _) => Shutdown());

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "Desktop Fab Launcher (left-click for menu)",
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu
        };

        // Show menu on left-click too, since right-click is captured by the hook
        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                // Use reflection to call the private ShowContextMenu method
                var mi = typeof(Forms.NotifyIcon).GetMethod("ShowContextMenu",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                mi?.Invoke(_notifyIcon, null);
            }
        };
    }

    private static void RegisterAllSequences(CommandRegistry registry, int maxLen)
    {
        for (int len = 2; len <= maxLen; len++)
        {
            // 長さ3はコマンド登録なし（テスト用：不一致→赤表示）
            if (len == 3) continue;

            int combinations = 1 << len; // 2^len
            for (int bits = 0; bits < combinations; bits++)
            {
                var seq = new List<ClickType>(len);
                var name = "";
                for (int i = len - 1; i >= 0; i--)
                {
                    bool isRight = ((bits >> i) & 1) == 1;
                    seq.Add(isRight ? ClickType.Right : ClickType.Left);
                    name += isRight ? "R" : "L";
                }
                registry.Register(seq, new LogCommandAction(name, $"{name} command"));
            }
        }
    }

    private static void RegisterSampleMappings(CommandRegistry registry)
    {
        registry.Register(ParseSequence("LLRLR"),
            new LaunchProcessAction(
                "Open Browser",
                "Launch default browser",
                new ProcessStartInfo("https://www.google.com") { UseShellExecute = true }));

        registry.Register(ParseSequence("LLRRR"),
            new LaunchProcessAction(
                "Open Explorer",
                "Launch File Explorer",
                new ProcessStartInfo("explorer.exe") { UseShellExecute = true }));
    }

    private static List<ClickType> ParseSequence(string sequence)
    {
        var result = new List<ClickType>(sequence.Length);
        foreach (var ch in sequence)
        {
            result.Add(ch == 'L' ? ClickType.Left : ClickType.Right);
        }
        return result;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _focusCheckTimer?.Stop();
        _hookService?.Stop();
        _hookService?.Dispose();
        _config?.Save();

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        base.OnExit(e);
    }
}
