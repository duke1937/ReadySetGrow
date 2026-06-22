using Godot;
using System.Collections.Generic;

namespace GrowDaGarden;

/// <summary>A rare bonus a crop can roll on harvest, multiplying its value.</summary>
public sealed class Mutation
{
    public required string Name { get; init; }
    public required float Multiplier { get; init; }
    public required float Chance { get; init; }   // 0..1 probability, checked rarest-first
    public required Color Tint { get; init; }      // overlay colour on the fruit

    public bool IsNormal => Multiplier <= 1f;

    /// <summary>The "no mutation" default.</summary>
    public static readonly Mutation Normal = new()
    {
        Name = "Normal", Multiplier = 1f, Chance = 1f, Tint = new Color(1, 1, 1, 0)
    };

    /// <summary>Storm-only mega-mutation — only rolls while a Storm event is active.</summary>
    public static readonly Mutation Shocked = new()
    {
        Name = "Shocked", Multiplier = 8f, Chance = 0f, Tint = new Color("f6ff3a")
    };

    /// <summary>A huge crop — one of the guaranteed mutations on "Strange" grove crops.</summary>
    public static readonly Mutation Giant = new()
    {
        Name = "Giant", Multiplier = 12f, Chance = 0f, Tint = new Color("8aff6a")
    };

    /// <summary>Rainbow AND Giant at once — the jackpot on Centurnial pack crops.</summary>
    public static readonly Mutation RainbowGiant = new()
    {
        Name = "Rainbow Giant", Multiplier = 300f, Chance = 0f, Tint = new Color("ff8af0")
    };

    // Event-only mutations (rolled while the matching weather event is active).
    public static readonly Mutation SunTouch = new()  { Name = "Sun-touch", Multiplier = 30f, Chance = 0f, Tint = new Color("ffcf3a") };
    public static readonly Mutation Weird = new()     { Name = "Strange",   Multiplier = 6f,  Chance = 0f, Tint = new Color("8a8a3a") };
    public static readonly Mutation Big = new()       { Name = "Big",       Multiplier = 5f,  Chance = 0f, Tint = new Color("9aff9a") };
    public static readonly Mutation Gigantic = new()  { Name = "Gigantic",  Multiplier = 30f, Chance = 0f, Tint = new Color("5aff5a") };

    /// <summary>Granted by the Admin Monkey pet as it roams the farm.</summary>
    public static readonly Mutation Admin = new()     { Name = "Admin",     Multiplier = 10f, Chance = 0f, Tint = new Color("ff2d55") };

    /// <summary>Rarest first — Roll() returns the first one that hits.</summary>
    public static readonly List<Mutation> Table = new()
    {
        new() { Name = "Rainbow", Multiplier = 4f,  Chance = 0.005f, Tint = new Color("ff66cc") },
        new() { Name = "Frozen",  Multiplier = 10f, Chance = 0.015f, Tint = new Color("66ccff") },
        new() { Name = "Gold",    Multiplier = 5f,  Chance = 0.04f,  Tint = new Color("ffd24a") },
        new() { Name = "Wet",     Multiplier = 1.5f,Chance = 0.10f,  Tint = new Color("4a9fff") },
    };

    /// <summary>
    /// Roll a mutation for a crop ripening now. During a "Storm" event crops can
    /// become Shocked (48×); during a "Rainbow" event Rainbow is far more likely.
    /// </summary>
    public static Mutation Roll(string? activeEvent = null, string? rarity = null)
    {
        // "Strange" grove crops ALWAYS ripen with Gold, Rainbow, or Giant.
        if (rarity == "Strange")
        {
            float r = GD.Randf();
            return r < 0.34f ? ByName("Gold") : r < 0.67f ? ByName("Rainbow") : Giant;
        }
        // "Centurnial" pack crops ALWAYS ripen Rainbow, Giant, or both.
        if (rarity == "Centurnial")
        {
            float r = GD.Randf();
            return r < 0.4f ? ByName("Rainbow") : r < 0.8f ? Giant : RainbowGiant;
        }

        if (activeEvent == "Storm" && GD.Randf() < 0.35f)
            return Shocked;
        if (activeEvent == "Rainbow" && GD.Randf() < 0.30f)
            return ByName("Rainbow");
        if (activeEvent == "Solar" && GD.Randf() < 0.35f)
            return SunTouch;
        if (activeEvent == "Strange" && GD.Randf() < 0.90f)
            return Weird;
        if (activeEvent == "Ground")               // ground event mutates every crop
            return GD.Randf() < 0.6f ? Big : Gigantic;

        foreach (Mutation m in Table)
        {
            if (GD.Randf() < m.Chance)
                return m;
        }
        return Normal;
    }

    public static Mutation ByName(string name)
    {
        if (name == Shocked.Name) return Shocked;
        if (name == Giant.Name) return Giant;
        if (name == RainbowGiant.Name) return RainbowGiant;
        if (name == SunTouch.Name) return SunTouch;
        if (name == Weird.Name) return Weird;
        if (name == Big.Name) return Big;
        if (name == Gigantic.Name) return Gigantic;
        if (name == Admin.Name) return Admin;
        foreach (Mutation m in Table)
            if (m.Name == name) return m;
        return Normal;
    }
}
