using System;
using System.Collections.Generic;
using System.Linq;
using DesktopFabLauncher.Commands;
using DesktopFabLauncher.Models;

namespace DesktopFabLauncher.Services;

public class CommandRegistry
{
    private readonly Dictionary<string, ICommandAction> _commands = new();

    public void Register(List<ClickType> sequence, ICommandAction action)
    {
        var key = SequenceToKey(sequence);
        _commands[key] = action;
    }

    public ICommandAction? Resolve(List<ClickType> sequence)
    {
        var key = SequenceToKey(sequence);
        return _commands.GetValueOrDefault(key);
    }

    public Dictionary<List<ClickType>, ICommandAction> GetAllMappings()
    {
        var result = new Dictionary<List<ClickType>, ICommandAction>();
        foreach (var (key, action) in _commands)
        {
            var sequence = KeyToSequence(key);
            result[sequence] = action;
        }
        return result;
    }

    private static string SequenceToKey(List<ClickType> sequence)
    {
        return string.Concat(sequence.Select(c => c == ClickType.Left ? "L" : "R"));
    }

    private static List<ClickType> KeyToSequence(string key)
    {
        return key.Select(c => c == 'L' ? ClickType.Left : ClickType.Right).ToList();
    }
}
