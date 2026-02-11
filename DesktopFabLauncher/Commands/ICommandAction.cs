using System.Threading.Tasks;

namespace DesktopFabLauncher.Commands;

public interface ICommandAction
{
    string Name { get; }
    string Description { get; }
    Task ExecuteAsync();
}
