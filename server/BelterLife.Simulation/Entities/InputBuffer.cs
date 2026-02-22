using System.Collections.Concurrent;
using BelterLife.Shared.Contracts.Hubs;

namespace BelterLife.Simulation.Entities;

public interface IInputBuffer
{
    void Set(string playerId, InputEvent input);
    IReadOnlyDictionary<string, InputEvent> GetAll();
}

/// <summary>
/// Thread-safe last-write-wins store of per-player input events.
/// Shard HTTP endpoint writes; physics loop reads a snapshot per tick.
/// </summary>
public class InputBuffer : IInputBuffer
{
    private readonly ConcurrentDictionary<string, InputEvent> _store = new();

    public void Set(string playerId, InputEvent input) =>
        _store[playerId] = input;

    public IReadOnlyDictionary<string, InputEvent> GetAll() =>
        new Dictionary<string, InputEvent>(_store);
}
