using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using DesktopFabLauncher.Commands;
using DesktopFabLauncher.Interop;
using DesktopFabLauncher.Models;
using DesktopFabLauncher.Services;

namespace DesktopFabLauncher.ViewModels;

public enum ResultState
{
    None,
    Success,
    Cancelled
}

public class OverlayViewModel : INotifyPropertyChanged
{
    private readonly InputStateMachine _stateMachine;
    private readonly DispatcherTimer _hideTimer;
    private readonly int _maxSlots;
    private readonly Dispatcher _dispatcher;
    private int _showVersion;
    private readonly double _visibleOpacity;

    private bool _isVisible;
    private double _overlayX;
    private double _overlayY;
    private double _overlayOpacity;
    private ResultState _resultState;
    private string _backgroundBrush = DefaultBackground;

    private const string DefaultBackground = "#D9333333";
    private const string SuccessBackground = "#D94CAF50";
    private const string CancelledBackground = "#D9FF6666";

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            OverlayOpacity = _isVisible ? _visibleOpacity : 0.0;
            OnPropertyChanged();
        }
    }

    public double OverlayX
    {
        get => _overlayX;
        set { _overlayX = value; OnPropertyChanged(); }
    }

    public double OverlayY
    {
        get => _overlayY;
        set { _overlayY = value; OnPropertyChanged(); }
    }

    public ResultState ResultState
    {
        get => _resultState;
        set { _resultState = value; OnPropertyChanged(); }
    }

    public double OverlayOpacity
    {
        get => _overlayOpacity;
        set { _overlayOpacity = value; OnPropertyChanged(); }
    }

    public string BackgroundBrush
    {
        get => _backgroundBrush;
        set { _backgroundBrush = value; OnPropertyChanged(); }
    }

    public ObservableCollection<ClickSlot> Slots { get; } = new();

    public OverlayViewModel(InputStateMachine stateMachine, AppConfig config)
    {
        _stateMachine = stateMachine;
        _maxSlots = config.MaxInputCount;
        _dispatcher = System.Windows.Application.Current.Dispatcher;
        _visibleOpacity = config.OverlayOpacity;
        _overlayOpacity = 0.0;

        for (int i = 0; i < _maxSlots; i++)
            Slots.Add(new ClickSlot());

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(config.ResultDisplayMs) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            IsVisible = false;
            ResultState = ResultState.None;
        };

        _stateMachine.StateChanged += OnStateChanged;
        _stateMachine.SequenceUpdated += OnSequenceUpdated;
        _stateMachine.CommandResolved += OnCommandResolved;
        _stateMachine.SequenceCancelled += OnSequenceCancelled;
    }

    private void OnStateChanged(LauncherState state)
    {
        if (state == LauncherState.Detecting)
        {
            _showVersion++;
            // Hide any previous result immediately when a new trigger starts
            _hideTimer.Stop();
            IsVisible = false;
            foreach (var slot in Slots)
                slot.Clear();
            ResultState = ResultState.None;
            BackgroundBrush = DefaultBackground;
            return;
        }

        if (state == LauncherState.Armed)
        {
            _showVersion++;
            _hideTimer.Stop();

            // 0. Hide first to avoid flashing the previous result
            IsVisible = false;

            // 1. Reset content while hidden
            foreach (var slot in Slots)
                slot.Clear();
            ResultState = ResultState.None;
            BackgroundBrush = DefaultBackground;

            // 2. Set position
            PositionOverlay();

            // 3. Show AFTER content and position are ready
            var showToken = _showVersion;
            _dispatcher.BeginInvoke(() =>
            {
                if (showToken != _showVersion) return;
                IsVisible = true;
            }, DispatcherPriority.Render);
        }
    }

    private void OnSequenceUpdated(List<ClickType> sequence)
    {
        for (int i = 0; i < _maxSlots; i++)
        {
            Slots[i].Type = i < sequence.Count ? sequence[i] : null;
        }
    }

    private void OnCommandResolved(ICommandAction? command)
    {
        if (command != null)
        {
            ResultState = ResultState.Success;
            BackgroundBrush = SuccessBackground;
        }
        else
        {
            ResultState = ResultState.Cancelled;
            BackgroundBrush = CancelledBackground;
        }
        _hideTimer.Start();
    }

    private void OnSequenceCancelled()
    {
        ResultState = ResultState.Cancelled;
        BackgroundBrush = CancelledBackground;
        _hideTimer.Start();
    }

    private void PositionOverlay()
    {
        NativeMethods.GetCursorPos(out var pt);

        const double overlayWidth = 200;
        const double overlayHeight = 50;
        const double offset = 20;

        double x = pt.X + offset;
        double y = pt.Y + offset;

        var screenWidth = SystemParameters.VirtualScreenWidth;
        var screenHeight = SystemParameters.VirtualScreenHeight;
        var screenLeft = SystemParameters.VirtualScreenLeft;
        var screenTop = SystemParameters.VirtualScreenTop;

        if (x + overlayWidth > screenLeft + screenWidth)
            x = pt.X - overlayWidth - offset;
        if (y + overlayHeight > screenTop + screenHeight)
            y = pt.Y - overlayHeight - offset;
        if (x < screenLeft)
            x = screenLeft;
        if (y < screenTop)
            y = screenTop;

        OverlayX = x;
        OverlayY = y;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
