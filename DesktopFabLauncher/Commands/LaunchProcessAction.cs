using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DesktopFabLauncher.Commands;

public class LaunchProcessAction : ICommandAction
{
    public string Name { get; }
    public string Description { get; }
    private readonly ProcessStartInfo _startInfo;

    public LaunchProcessAction(string name, string description, ProcessStartInfo startInfo)
    {
        Name = name;
        Description = description;
        _startInfo = startInfo;
    }

    public Task ExecuteAsync()
    {
        try
        {
            Process.Start(_startInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Command Error] {Name}: {ex.Message}");
        }
        return Task.CompletedTask;
    }
}
