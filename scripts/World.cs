using System.Collections.Generic;

namespace GrowDaGarden;

/// <summary>One playable level/world (Garden, Space, Heaven) with its own seed catalog.</summary>
public sealed class World
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required string Icon { get; init; }
    public required List<SeedType> Seeds { get; init; }

    /// <summary>Coins needed to unlock the NEXT world (long.MaxValue if this is the last).</summary>
    public required long UnlockNextAt { get; init; }

    public SeedType? SeedByName(string name)
    {
        foreach (SeedType s in Seeds)
            if (s.Name == name) return s;
        return null;
    }
}
