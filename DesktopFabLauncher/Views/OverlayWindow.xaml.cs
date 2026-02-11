using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using DesktopFabLauncher.Interop;
using DesktopFabLauncher.ViewModels;
using WpfPoint = System.Windows.Point;

namespace DesktopFabLauncher.Views;

public partial class OverlayWindow : Window
{
    public OverlayWindow(OverlayViewModel viewModel)
    {
        ShowActivated = false;
        InitializeComponent();
        DataContext = viewModel;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Start off-screen to prevent any flash on first show
        Left = -9999;
        Top = -9999;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TRANSPARENT);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not OverlayViewModel vm) return;

        switch (e.PropertyName)
        {
            case nameof(OverlayViewModel.OverlayX):
            case nameof(OverlayViewModel.OverlayY):
                var logical = PhysicalToLogical(vm.OverlayX, vm.OverlayY);
                Left = logical.X;
                Top = logical.Y;
                break;
        }
    }

    private WpfPoint PhysicalToLogical(double physX, double physY)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            var transform = source.CompositionTarget.TransformFromDevice;
            return transform.Transform(new WpfPoint(physX, physY));
        }
        return new WpfPoint(physX, physY);
    }
}
