using Godot;
using System;
using System.Collections.Generic;

namespace GrowDaGarden;

/// <summary>Catalogs for all three worlds, plus tuning constants and unlock gates.</summary>
public static class GameData
{
    public const long StartingCoins = 50;
    public const int GardenColumns = 5;
    public const int GardenRows = 4;
    public const int PlotCount = GardenColumns * GardenRows;

    public const double MaxOfflineSeconds = 8 * 60 * 60; // 8 hours

    // Coin gates for unlocking the next world.
    public const long UnlockSpaceAt = 50_000_000L;            // 50 million  -> Space
    public const long UnlockHeavenAt = 5_000_000_000_000L;    // 5 trillion  -> Heaven

    public static readonly List<World> Worlds = new()
    {
        new World
        {
            Index = 0, Name = "Garden", Icon = "🌍",
            Seeds = Build(GardenEntries(), baseCost: 10, costRatio: 1.194),
            UnlockNextAt = UnlockSpaceAt,
        },
        new World
        {
            Index = 1, Name = "Space", Icon = "🚀",
            Seeds = Build(GenSpace(), baseCost: 25_000, costRatio: 1.25),
            UnlockNextAt = UnlockHeavenAt,
        },
        new World
        {
            Index = 2, Name = "Heaven", Icon = "☁️",
            Seeds = Build(GenHeaven(), baseCost: 1_000_000_000, costRatio: 1.30),
            UnlockNextAt = long.MaxValue,
        },
    };

    /// <summary>Coins required to unlock the given world (0 for the starting world).</summary>
    public static long UnlockThreshold(int worldIndex) => worldIndex switch
    {
        1 => UnlockSpaceAt,
        2 => UnlockHeavenAt,
        _ => 0,
    };

    // ---- shared catalog construction --------------------------------------

    private static List<SeedType> Build(
        IReadOnlyList<(string Name, Color Color)> entries, double baseCost, double costRatio)
    {
        var list = new List<SeedType>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            string rarity = RarityForIndex(i);
            long cost = NiceRound(baseCost * Math.Pow(costRatio, i));
            long value = NiceRound(cost * (1.9 + RarityBonus(rarity)));
            float grow = (float)Math.Round(12.0 * Math.Pow(1.07, i));

            list.Add(new SeedType
            {
                Name = entries[i].Name,
                Rarity = rarity,
                Cost = cost,
                BaseValue = value,
                GrowSeconds = grow,
                Color = entries[i].Color,
            });
        }
        return list;
    }

    private static string RarityForIndex(int i) =>
        i < 15 ? "Common"
        : i < 30 ? "Uncommon"
        : i < 43 ? "Rare"
        : i < 55 ? "Epic"
        : i < 65 ? "Legendary"
        : i < 71 ? "Mythical"
        : "Divine";

    private static double RarityBonus(string rarity) => rarity switch
    {
        "Uncommon"  => 0.10,
        "Rare"      => 0.20,
        "Epic"      => 0.30,
        "Legendary" => 0.45,
        "Mythical"  => 0.60,
        "Divine"    => 0.80,
        _            => 0.0,
    };

    /// <summary>Round to ~2 significant figures so prices read as tidy numbers.</summary>
    private static long NiceRound(double v)
    {
        if (v < 10) return (long)Math.Max(1, Math.Round(v));
        int digits = (int)Math.Floor(Math.Log10(v));
        double pow = Math.Pow(10, digits - 1);
        return (long)(Math.Round(v / pow) * pow);
    }

    // ---- world 1: Garden (curated real produce, 75) -----------------------

    private static List<(string, Color)> GardenEntries()
    {
        (string Name, string Hex)[] defs =
        {
            ("Carrot","ff8c2b"),("Potato","c9a26b"),("Radish","e0506a"),("Lettuce","7fc24a"),
            ("Cabbage","9fd17a"),("Onion","d9c27a"),("Tomato","e23b2e"),("Cucumber","4f9e3a"),
            ("Corn","f4d03f"),("Pea","6fbf4a"),("Green Bean","8fae5a"),("Spinach","3f8f3a"),
            ("Beet","a23355"),("Turnip","e8d2e0"),("Garlic","ece3d0"),
            ("Strawberry","e3354b"),("Blueberry","4060d6"),("Raspberry","c83a5a"),("Blackberry","3a2a44"),
            ("Bell Pepper","d83a2a"),("Eggplant","6a3a8f"),("Zucchini","4a8f3a"),("Broccoli","3f7a3f"),
            ("Cauliflower","ece6d2"),("Pumpkin","f08a24"),("Squash","e0b84a"),("Celery","9fc24a"),
            ("Asparagus","6fae4a"),("Artichoke","7f9f5a"),("Leek","aaca6a"),
            ("Watermelon","3fa14a"),("Cantaloupe","e8b06a"),("Honeydew","b6d96a"),("Apple","d83a3a"),
            ("Pear","c2d24a"),("Peach","ffb07a"),("Plum","7a3a8f"),("Cherry","c41e3a"),
            ("Apricot","f0a24a"),("Grape","6a3a9f"),("Fig","7a4a6a"),("Pomegranate","b5302f"),("Kiwi","8fae3a"),
            ("Orange","ff8c1a"),("Lemon","f4e04a"),("Lime","9fd13a"),("Mango","f0a01a"),
            ("Pineapple","e8c24a"),("Banana","f4d84a"),("Papaya","ee9a4a"),("Coconut","b0825a"),
            ("Avocado","5a7a3a"),("Passionfruit","6a3a7a"),("Guava","e07a8a"),("Lychee","e05a6a"),
            ("Dragon Fruit","ff4d8d"),("Star Fruit","f4d24a"),("Durian","c2b04a"),("Jackfruit","d9b04a"),
            ("Rambutan","e0405a"),("Mangosteen","5a2a4a"),("Persimmon","f08a3a"),("Golden Apple","ffd24a"),
            ("Moonberry","7a8aff"),("Sunfruit","ff9a2a"),
            ("Crystal Berry","8af0ff"),("Phoenix Pepper","ff5a1a"),("Frostmelon","aef0ff"),
            ("Voidplum","4a2a6a"),("Stardust Bloom","b0a0ff"),("Emberroot","ff6a3a"),
            ("Celestial Lotus","f0d0ff"),("Aurora Vine","6affc8"),("Eternal Rose","ff4d6d"),("Genesis Seed","ffe6a0"),
        };
        var list = new List<(string, Color)>(defs.Length);
        foreach ((string name, string hex) in defs)
            list.Add((name, new Color(hex)));
        return list;
    }

    // ---- worlds 2 & 3: themed generators (75 unique each) -----------------

    private static List<(string, Color)> GenSpace()
    {
        string[] pre =
        {
            "Cosmic", "Lunar", "Astro", "Stellar", "Nebula", "Galactic", "Solar", "Meteor",
            "Quantum", "Orbital", "Plasma", "Void", "Comet", "Nova", "Photon",
        };
        string[] core = { "Sprout", "Berry", "Bloom", "Melon", "Pod" };
        return GenThemed(pre, core, 0.52f, 0.92f, 0.58f, 0.95f);
    }

    private static List<(string, Color)> GenHeaven()
    {
        string[] pre =
        {
            "Angelic", "Divine", "Holy", "Celestial", "Radiant", "Sacred", "Heavenly", "Golden",
            "Eternal", "Seraph", "Cherub", "Glory", "Halo", "Spirit", "Blessed",
        };
        string[] core = { "Lily", "Rose", "Lotus", "Bloom", "Fruit" };
        return GenThemed(pre, core, 0.08f, 0.62f, 0.35f, 1.0f);
    }

    /// <summary>15 prefixes × 5 cores = 75 unique names, coloured along an HSV ramp.</summary>
    private static List<(string, Color)> GenThemed(
        string[] pre, string[] core, float hue0, float hue1, float sat, float val)
    {
        var list = new List<(string, Color)>(pre.Length * core.Length);
        foreach (string c in core)
            foreach (string p in pre)
            {
                float t = list.Count / 74f;
                float hue = Mathf.Lerp(hue0, hue1, t);
                list.Add(($"{p} {c}", Color.FromHsv(hue, sat, val)));
            }
        return list;
    }
}
