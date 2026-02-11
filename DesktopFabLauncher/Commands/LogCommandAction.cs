using System;
using System.Threading.Tasks;

namespace DesktopFabLauncher.Commands;

public class LogCommandAction : ICommandAction
{
    public string Name { get; }
    public string Description { get; }

    public LogCommandAction(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public Task ExecuteAsync()
    {
        Console.WriteLine($"[Command Executed] {Name}: {Description}");
        return Task.CompletedTask;
    }
}
