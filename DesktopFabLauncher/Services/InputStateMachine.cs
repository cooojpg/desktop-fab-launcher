using System;
using System.Collections.Generic;
using System.Windows.Threading;
using DesktopFabLauncher.Commands;
using DesktopFabLauncher.Interop;
using DesktopFabLauncher.Models;

namespace DesktopFabLauncher.Services;

public class InputStateMachine
{
    private readonly AppConfig _config;
    private readonly CommandRegistry _registry;
    private readonly MouseHookService _hookService;
    private readonly Dispatcher _dispatcher;

    private readonly DispatcherTimer _longPressTimer;
    private readonly DispatcherTimer _inputTimeoutTimer;

    private NativeMethods.POINT _rightDownPos;
    private IntPtr _foregroundWindowOnArm;

    // Deferred actions — executed via BeginInvoke after hook callback returns
    private readonly List<Action> _deferred = new();

    // Button-up suppression for Armed state clicks
    private int _pendingRightUps;
    private int _pendingLeftUps;
    // Tracks whether the initial trigger right button is still physically held
    private bool _initialRightHeld;

    public LauncherState State { get; private set; } = LauncherState.Idle;
    public List<ClickType> CurrentSequence { get; } = new();

    public event Action<LauncherState>? StateChanged;
    public event Action<List<ClickType>>? SequenceUpdated;
    public event Action<ICommandAction?>? CommandResolved;
    public event Action? SequenceCancelled;

    public InputStateMachine(AppConfig config, CommandRegistry registry, MouseHookService hookService)
    {
        _config = config;
        _registry = registry;
        _hookService = hookService;
        _dispatcher = System.Windows.Application.Current.Dispatcher;

        _longPressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_config.LongPressThresholdMs) };
        _longPressTimer.Tick += OnLongPressElapsed;

        _inputTimeoutTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_config.InputTimeoutMs) };
        _inputTimeoutTimer.Tick += OnInputTimeout;

        _hookService.MouseAction += OnMouseAction;
    }

    /// <summary>
    /// Called from hook callback. Must return FAST. No I/O, no SendInput.
    /// </summary>
    private bool OnMouseAction(MouseActionArgs args)
    {
        // 1. Suppress orphaned button-ups (from Armed state clicks)
        if (args.MessageType == NativeMethods.WM_RBUTTONUP && _pendingRightUps > 0)
        {
            _pendingRightUps--;
            return true;
        }
        if (args.MessageType == NativeMethods.WM_LBUTTONUP && _pendingLeftUps > 0)
        {
            _pendingLeftUps--;
            return true;
        }

        // 2. State dispatch (state changes only, no heavy work)
        _deferred.Clear();

        var suppress = State switch
        {
            LauncherState.Idle => HandleIdle(args),
            LauncherState.Detecting => HandleDetecting(args),
            LauncherState.Armed => HandleArmed(args),
            _ => false
        };

        // 3. Flush deferred actions asynchronously
        if (_deferred.Count > 0)
        {
            var batch = _deferred.ToArray();
            _dispatcher.BeginInvoke(() =>
            {
                foreach (var action in batch)
                    action();
            });
        }

        return suppress;
    }

    private bool HandleIdle(MouseActionArgs args)
    {
        if (args.MessageType == NativeMethods.WM_RBUTTONDOWN)
        {
            _rightDownPos = args.Point;
            _initialRightHeld = true;
            State = LauncherState.Detecting;
            _deferred.Add(() =>
            {
                _longPressTimer.Start();
                StateChanged?.Invoke(LauncherState.Detecting);
            });
            return true;
        }
        return false;
    }

    private bool HandleDetecting(MouseActionArgs args)
    {
        switch (args.MessageType)
        {
            case NativeMethods.WM_RBUTTONUP:
                // Short press — cancel and re-inject
                _initialRightHeld = false;
                State = LauncherState.Idle;
                CurrentSequence.Clear();
                var pos1 = _rightDownPos;
                _deferred.Add(() =>
                {
                    _longPressTimer.Stop();
                    StateChanged?.Invoke(LauncherState.Idle);
                    _hookService.InjectRightClick(pos1);
                });
                return true;

            case NativeMethods.WM_MOUSEMOVE:
                var dx = Math.Abs(args.Point.X - _rightDownPos.X);
                var dy = Math.Abs(args.Point.Y - _rightDownPos.Y);
                if (dx > _config.MovementThresholdPx || dy > _config.MovementThresholdPx)
                {
                    // Movement too large — fall back to normal right click
                    // Keep _initialRightHeld = true; the real up will come later
                    // and be suppressed by the pending counter
                    _pendingRightUps++; // suppress the real right-up that comes later
                    _initialRightHeld = false;
                    State = LauncherState.Idle;
                    CurrentSequence.Clear();
                    var pos2 = _rightDownPos;
                    _deferred.Add(() =>
                    {
                        _longPressTimer.Stop();
                        StateChanged?.Invoke(LauncherState.Idle);
                        _hookService.InjectRightClick(pos2);
                    });
                }
                return false;

            case NativeMethods.WM_RBUTTONDOWN:
                return true;

            default:
                return false;
        }
    }

    private bool HandleArmed(MouseActionArgs args)
    {
        switch (args.MessageType)
        {
            case NativeMethods.WM_LBUTTONDOWN:
                _pendingLeftUps++;
                AddClick(ClickType.Left);
                return true;

            case NativeMethods.WM_LBUTTONUP:
                return true;

            case NativeMethods.WM_RBUTTONDOWN:
                _pendingRightUps++;
                AddClick(ClickType.Right);
                return true;

            case NativeMethods.WM_RBUTTONUP:
                // Release of initial trigger right button
                _initialRightHeld = false;
                return true;

            case NativeMethods.WM_MBUTTONDOWN:
            case NativeMethods.WM_MBUTTONUP:
                GoIdle();
                _deferred.Add(() => SequenceCancelled?.Invoke());
                return true;

            default:
                return false;
        }
    }

    private void AddClick(ClickType click)
    {
        CurrentSequence.Add(click);

        var snapshot = new List<ClickType>(CurrentSequence);
        _deferred.Add(() =>
        {
            _inputTimeoutTimer.Stop();
            SequenceUpdated?.Invoke(snapshot);
        });

        if (CurrentSequence.Count >= _config.MaxInputCount)
        {
            var command = _registry.Resolve(CurrentSequence);
            GoIdle();
            _deferred.Add(() =>
            {
                CommandResolved?.Invoke(command);
                if (command != null)
                    _ = command.ExecuteAsync();
            });
        }
        else
        {
            // 2入力目以降は短いタイムアウトを使用
            var useShort = CurrentSequence.Count >= 2;
            _deferred.Add(() =>
            {
                _inputTimeoutTimer.Interval = useShort
                    ? TimeSpan.FromMilliseconds(_config.ShortInputTimeoutMs)
                    : TimeSpan.FromMilliseconds(_config.InputTimeoutMs);
                _inputTimeoutTimer.Start();
            });
        }
    }

    private void OnLongPressElapsed(object? sender, EventArgs e)
    {
        _longPressTimer.Stop();
        State = LauncherState.Armed;
        _foregroundWindowOnArm = NativeMethods.GetForegroundWindow();
        _inputTimeoutTimer.Start();
        StateChanged?.Invoke(LauncherState.Armed);
        SequenceUpdated?.Invoke(new List<ClickType>(CurrentSequence));
    }

    private void OnInputTimeout(object? sender, EventArgs e)
    {
        _inputTimeoutTimer.Stop();
        var command = CurrentSequence.Count > 0 ? _registry.Resolve(CurrentSequence) : null;
        // If the initial right button is still held, catch its up via counter
        if (_initialRightHeld)
        {
            _pendingRightUps++;
            _initialRightHeld = false;
        }
        State = LauncherState.Idle;
        CurrentSequence.Clear();
        StateChanged?.Invoke(LauncherState.Idle);
        CommandResolved?.Invoke(command);
        if (command != null)
            _ = command.ExecuteAsync();
    }

    /// <summary>
    /// Transition to Idle from Armed/Cancel (called from hook callback context).
    /// Only updates state — all notifications must go through _deferred.
    /// </summary>
    private void GoIdle()
    {
        _inputTimeoutTimer.Stop();
        _longPressTimer.Stop();

        // If the initial right button is still held, ensure its up gets suppressed
        if (_initialRightHeld)
        {
            _pendingRightUps++;
            _initialRightHeld = false;
        }

        State = LauncherState.Idle;
        CurrentSequence.Clear();

        _deferred.Add(() => StateChanged?.Invoke(LauncherState.Idle));
    }

    public void CheckForegroundWindow()
    {
        if (State == LauncherState.Armed)
        {
            var current = NativeMethods.GetForegroundWindow();
            if (current != _foregroundWindowOnArm)
            {
                _inputTimeoutTimer.Stop();
                _longPressTimer.Stop();
                if (_initialRightHeld)
                {
                    _pendingRightUps++;
                    _initialRightHeld = false;
                }
                State = LauncherState.Idle;
                CurrentSequence.Clear();
                StateChanged?.Invoke(LauncherState.Idle);
                SequenceCancelled?.Invoke();
            }
        }
    }
}
