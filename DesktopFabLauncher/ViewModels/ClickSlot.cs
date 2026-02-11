using System.ComponentModel;
using System.Runtime.CompilerServices;
using DesktopFabLauncher.Models;

namespace DesktopFabLauncher.ViewModels;

public class ClickSlot : INotifyPropertyChanged
{
    private ClickType? _type;

    public ClickType? Type
    {
        get => _type;
        set
        {
            _type = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFilled));
        }
    }

    public bool IsFilled => _type.HasValue;

    public void Clear() => Type = null;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
