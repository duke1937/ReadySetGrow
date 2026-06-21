namespace GrowDaGarden;

/// <summary>Pure game state for a single garden tile (no Godot nodes here).</summary>
public sealed class PlotState
{
    public SeedType? Seed;     // null == empty soil
    public float Growth;       // seconds of growth accumulated
    public Mutation Mutation = Mutation.Normal;
    public bool MutationRolled; // mutation is decided once, the moment the crop ripens
    public float Size = 1f;    // per-plant size roll (~0.7..1.55) — scales the model and the payout
    public int SlaveOf = -1;   // >=0 means this spot is covered by a multi-tile plant at that plot index

    public bool IsEmpty => Seed is null;
    public bool IsReady => Seed is not null && Growth >= Seed.GrowSeconds;

    /// <summary>0..1 growth fraction for bars and drawing.</summary>
    public float Progress =>
        Seed is null ? 0f : System.Math.Clamp(Growth / Seed.GrowSeconds, 0f, 1f);

    /// <summary>Coins this crop yields right now (only meaningful when ready).</summary>
    public double Payout =>
        Seed is null ? 0 : System.Math.Max(1.0, Seed.BaseValue * Mutation.Multiplier * Size);

    public void Plant(SeedType seed, float size = 1f)
    {
        Seed = seed;
        Growth = 0f;
        Mutation = Mutation.Normal;
        MutationRolled = false;
        Size = size;
        SlaveOf = -1;
    }

    public void Clear()
    {
        Seed = null;
        Growth = 0f;
        Mutation = Mutation.Normal;
        MutationRolled = false;
        Size = 1f;
        SlaveOf = -1;
    }
}
