using System.Collections.Generic;

namespace GrowDaGarden;

/// <summary>Pure game state for a single garden tile (no Godot nodes here).</summary>
public sealed class PlotState
{
    public SeedType? Seed;            // null == empty soil
    public float Growth;             // seconds of growth accumulated
    public readonly List<Mutation> Mutations = new(); // stacked mutations (no limit)
    public bool MutationRolled;       // the initial mutation roll (the moment it ripens) is done
    public float MutTimer;            // accumulates time while ripe for extra mutation rolls
    public float Size = 1f;           // per-plant size roll (~0.7..1.55)
    public int SlaveOf = -1;          // >=0 means covered by a multi-tile plant at that plot index

    public bool IsEmpty => Seed is null;
    public bool IsReady => Seed is not null && Growth >= Seed.GrowSeconds;

    /// <summary>0..1 growth fraction for bars and drawing.</summary>
    public float Progress =>
        Seed is null ? 0f : System.Math.Clamp(Growth / Seed.GrowSeconds, 0f, 1f);

    public bool IsMutated => Mutations.Count > 0;

    /// <summary>Combined multiplier of every stacked mutation (1 if none).</summary>
    public double MutMultiplier
    {
        get
        {
            double p = 1.0;
            foreach (Mutation m in Mutations)
            {
                double mult = m.Multiplier;
                if (Seed is not null && Seed.RainbowMultiplier > 0 && m.Name == "Rainbow")
                    mult = Seed.RainbowMultiplier;   // e.g. Candy Cane's 120x
                p *= mult;
            }
            return p;
        }
    }

    /// <summary>The strongest mutation (used for the fruit's colour); Normal if none.</summary>
    public Mutation Primary
    {
        get
        {
            Mutation best = Mutation.Normal;
            foreach (Mutation m in Mutations)
                if (m.Multiplier > best.Multiplier) best = m;
            return best;
        }
    }

    /// <summary>Back-compat single-mutation accessor (used by the 2D game).</summary>
    public Mutation Mutation
    {
        get => Primary;
        set { Mutations.Clear(); if (!value.IsNormal) Mutations.Add(value); }
    }

    /// <summary>Coins this crop yields right now (only meaningful when ready).</summary>
    public double Payout =>
        Seed is null ? 0 : System.Math.Max(1.0, Seed.BaseValue * MutMultiplier * Size);

    /// <summary>Add a mutation — up to 3 *different* ones per crop (no duplicates).</summary>
    public void AddMutation(Mutation m)
    {
        if (m.IsNormal || Mutations.Count >= 3) return;
        foreach (Mutation e in Mutations)
            if (e.Name == m.Name) return;   // already has this one
        Mutations.Add(m);
    }

    /// <summary>e.g. "Gold×2 Shocked" — distinct mutation names with counts.</summary>
    public string MutationSummary()
    {
        if (Mutations.Count == 0) return "";
        var order = new List<string>();
        var counts = new Dictionary<string, int>();
        foreach (Mutation m in Mutations)
        {
            if (!counts.ContainsKey(m.Name)) { counts[m.Name] = 0; order.Add(m.Name); }
            counts[m.Name]++;
        }
        var parts = new List<string>();
        foreach (string n in order)
            parts.Add(counts[n] > 1 ? $"{n}×{counts[n]}" : n);
        return string.Join(" ", parts);
    }

    public void Plant(SeedType seed, float size = 1f)
    {
        Seed = seed;
        Growth = 0f;
        Mutations.Clear();
        MutationRolled = false;
        MutTimer = 0f;
        Size = size;
        SlaveOf = -1;
    }

    public void Clear()
    {
        Seed = null;
        Growth = 0f;
        Mutations.Clear();
        MutationRolled = false;
        MutTimer = 0f;
        Size = 1f;
        SlaveOf = -1;
    }
}
