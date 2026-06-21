using Godot;
using System;
using System.Collections.Generic;

namespace GrowDaGarden;

/// <summary>
/// The single Growden garden catalog: 75 seeds on the rarity ladder
/// Common → Uncommon → Rare → Epic → Legendary → Secret, plus one
/// crowning Divine seed at the very top (76 in all).
///
/// Cost / sell value / grow time are *generated* from each seed's position in
/// the list, so the economy stays on a smooth curve — reorder or insert a seed
/// and the whole curve rebalances automatically.
/// </summary>
public static class Catalog
{
    public const long StartingCoins = 50;

    // The farm has two open dirt fields, each a grid of planting spots.
    public const int FieldCount = 2;
    public const int FieldCols = 3;
    public const int FieldRows = 4;
    public const int PlotsPerField = FieldCols * FieldRows; // 12
    public const int PlotCount = FieldCount * PlotsPerField; // 24 planting spots

    public const double MaxOfflineSeconds = 8 * 60 * 60; // 8 hours of offline growth

    // Rarity tier sizes, cheapest first. 20+16+14+12+8+5 = 75, then +1 Divine = 76.
    private const int CommonCount = 20;
    private const int UncommonCount = 16;
    private const int RareCount = 14;
    private const int EpicCount = 12;
    private const int LegendaryCount = 8;
    private const int SecretCount = 5;
    // Divine = the single remaining seed at the top.

    private static List<SeedType>? _seeds;

    /// <summary>The full 76-seed catalog (built once, on first access).</summary>
    public static List<SeedType> Seeds => _seeds ??= Build();

    public static SeedType? SeedByName(string name)
    {
        foreach (SeedType s in Seeds)
            if (s.Name == name) return s;
        return null;
    }

    /// <summary>Coins needed to unlock the Magical Tree's premium seeds.</summary>
    public const long TreeUnlockCost = 100_000_000_000L; // 100 billion

    private static List<SeedType>? _treeSeeds;

    /// <summary>The Magical Tree's premium catalog: Divine → Ultra → Titan → Entity
    /// → Eternal → Admin. Unlocked once you reach 100B coins.</summary>
    public static List<SeedType> TreeSeeds => _treeSeeds ??= BuildTree();

    /// <summary>Coins needed to unlock the Pets Shop, and the Hidden Grove.</summary>
    public const double PetsUnlockCost = 1e17;    // 100 Qa
    public const double GroveUnlockCost = 1e18;   // 1 Qi

    private static List<SeedType>? _groveSeeds;

    /// <summary>The Hidden Grove's catalog: Hidden → Alpha → Strange → Celestial → Infinite.</summary>
    public static List<SeedType> GroveSeeds => _groveSeeds ??= BuildGrove();

    private static List<SeedType>? _uniSeeds;

    /// <summary>The Uni-Grape catalog (reached by climbing the vine):
    /// Dimensional → Galaxy → Solar → Blackhole.</summary>
    public static List<SeedType> UniSeeds => _uniSeeds ??= BuildUni();

    private static List<SeedType>? _packSeeds;

    /// <summary>Cost to spin the Mystery Pack for a random top-tier fruit.</summary>
    public const double PackCost = 1e27;

    /// <summary>The three best fruits in the game — only obtainable from the Mystery Pack.</summary>
    public static List<SeedType> PackSeeds => _packSeeds ??= new()
    {
        new SeedType
        {
            Name = "Burning Bud", Rarity = "Mystic", Cost = 5e27, BaseValue = 1e30,
            GrowSeconds = 150f, Color = new Color("ff5a2a"), Shape = PlantShape.BurningBud, Footprint = 2,
        },
        new SeedType
        {
            Name = "Sugar Apple", Rarity = "Mystic", Cost = 6e27, BaseValue = 1.3e30,
            GrowSeconds = 140f, Color = new Color("9aff6a"), Shape = PlantShape.SugarApple,
        },
        new SeedType
        {
            Name = "Candy Cane", Rarity = "Mystic", Cost = 4e27, BaseValue = 8e29,
            GrowSeconds = 120f, Color = new Color("ff4d6a"), Shape = PlantShape.CandyCane, RainbowMultiplier = 120,
        },
    };

    /// <summary>Find a seed in any catalog.</summary>
    public static SeedType? SeedByNameAny(string name)
    {
        SeedType? s = SeedByName(name);
        if (s is not null) return s;
        foreach (SeedType t in TreeSeeds)
            if (t.Name == name) return t;
        foreach (SeedType g in GroveSeeds)
            if (g.Name == name) return g;
        foreach (SeedType u in UniSeeds)
            if (u.Name == name) return u;
        foreach (SeedType pk in PackSeeds)
            if (pk.Name == name) return pk;
        return null;
    }

    /// <summary>Touch every catalog so build errors surface eagerly (e.g. at startup).</summary>
    public static void Warmup()
    {
        _ = Seeds; _ = TreeSeeds; _ = GroveSeeds; _ = UniSeeds; _ = PackSeeds; _ = Pets;
    }

    // ---- catalog construction --------------------------------------------

    private static List<SeedType> Build()
    {
        const double baseCost = 10;
        const double costRatio = 1.30;   // each seed ~30% pricier than the last

        var list = new List<SeedType>(Entries.Length);
        for (int i = 0; i < Entries.Length; i++)
        {
            string rarity = RarityForIndex(i);
            double cost = NiceRound(baseCost * Math.Pow(costRatio, i));
            double value = NiceRound(cost * (1.9 + RarityBonus(rarity)));
            float grow = (float)Math.Round(8.0 * Math.Pow(1.045, i));

            list.Add(new SeedType
            {
                Name = Entries[i].Name,
                Rarity = rarity,
                Cost = cost,
                BaseValue = value,
                GrowSeconds = grow,
                Color = new Color(Entries[i].Hex),
                Shape = ShapeFor(Entries[i].Name, rarity),
            });
        }
        return list;
    }

    // ---- Magical Tree catalog construction --------------------------------

    private static List<SeedType> BuildTree()
    {
        const double baseCost = 100_000_000_000d; // starts at the 100B unlock
        const double costRatio = 1.55;

        var list = new List<SeedType>(TreeEntries.Length);
        for (int i = 0; i < TreeEntries.Length; i++)
        {
            string rarity = TreeRarityForIndex(i);
            double cost = NiceRound(baseCost * Math.Pow(costRatio, i));
            double value = NiceRound(cost * (1.9 + RarityBonus(rarity)));
            float grow = (float)Math.Round(80.0 * Math.Pow(1.04, i));

            list.Add(new SeedType
            {
                Name = TreeEntries[i].Name,
                Rarity = rarity,
                Cost = cost,
                BaseValue = value,
                GrowSeconds = grow,
                Color = new Color(TreeEntries[i].Hex),
                Shape = ShapeFor(TreeEntries[i].Name, rarity),
            });
        }
        return list;
    }

    private static string TreeRarityForIndex(int i)
    {
        if (i < 6) return "Divine";
        if (i < 12) return "Ultra";
        if (i < 17) return "Titan";
        if (i < 22) return "Entity";
        if (i < 26) return "Eternal";
        return "Admin";
    }

    private static readonly (string Name, string Hex)[] TreeEntries =
    {
        // Divine (6)
        ("Genesis Bloom","ffe6a0"), ("Seraph Fruit","fff0b0"), ("Halo Berry","ffe066"),
        ("Empyrean Plum","f0d0a0"), ("Celestial Pear","fde2b3"), ("Ambrosia","fff4c2"),
        // Ultra (6)
        ("Ultra Core","00e5ff"), ("Pulsar Pod","30f0ff"), ("Quasar Bloom","00c8ff"),
        ("Hyperfruit","5af0ff"), ("Singularity Seed","00b0ff"), ("Ultraviolet Vine","8a5aff"),
        // Titan (5)
        ("Titanroot","ff7a1a"), ("Colossus Gourd","ff8a3d"), ("Atlas Bloom","ffa030"),
        ("Gigafruit","ff6a00"), ("Behemoth Berry","ff9450"),
        // Entity (5)
        ("Entity Eye","b14dff"), ("Wraithbloom","9a4dff"), ("Phantom Pod","c060ff"),
        ("Eldritch Fruit","7c4dff"), ("The Watcher","d04dff"),
        // Eternal (4)
        ("Eternal Flame","ffd27a"), ("Foreverfruit","ffe9a8"), ("Timeless Bloom","f5f0d0"), ("Infinity Berry","ffe6c0"),
        // Admin (4)
        ("Admin Apple","ff2d55"), ("Grow Seed","ff4d4d"), ("Banhammer Bloom","ff2d2d"), ("Sudo Fruit","ff5a7a"),
    };

    // ---- Hidden Grove catalog construction --------------------------------

    private static List<SeedType> BuildGrove()
    {
        const double baseCost = GroveUnlockCost; // starts at the 1 Qi unlock
        const double costRatio = 1.32;

        var list = new List<SeedType>(GroveEntries.Length);
        for (int i = 0; i < GroveEntries.Length; i++)
        {
            string rarity = GroveRarityForIndex(i);
            double cost = NiceRound(baseCost * Math.Pow(costRatio, i));
            double value = NiceRound(cost * (1.9 + RarityBonus(rarity)));
            float grow = (float)Math.Round(100.0 * Math.Pow(1.04, i));

            list.Add(new SeedType
            {
                Name = GroveEntries[i].Name,
                Rarity = rarity,
                Cost = cost,
                BaseValue = value,
                GrowSeconds = grow,
                Color = new Color(GroveEntries[i].Hex),
                Shape = ShapeFor(GroveEntries[i].Name, rarity),
            });
        }

        // The Centurnial Seed Pack — the three best crops, all guaranteed Rainbow / Giant / both.
        list.Add(new SeedType
        {
            Name = "Bendboo", Rarity = "Centurnial", Cost = 5e20, BaseValue = 2.2e21,
            GrowSeconds = 90f, Color = new Color("6aff8a"), Shape = PlantShape.Bamboo,
        });
        list.Add(new SeedType
        {
            Name = "Toxikit", Rarity = "Centurnial", Cost = 1.2e21, BaseValue = 1.4e22,
            GrowSeconds = 600f, Color = new Color("9aff3a"), Shape = PlantShape.Mushroom, Footprint = 4,
        });
        list.Add(new SeedType
        {
            Name = "Snapdragon", Rarity = "Centurnial", Cost = 8e20, BaseValue = 3.2e21,
            GrowSeconds = 120f, Color = new Color("ff5a7a"), Shape = PlantShape.Snapdragon,
        });

        return list;
    }

    private static string GroveRarityForIndex(int i)
    {
        if (i < 5) return "Hidden";
        if (i < 10) return "Alpha";
        if (i < 15) return "Strange";
        if (i < 20) return "Celestial";
        return "Infinite";
    }

    private static readonly (string Name, string Hex)[] GroveEntries =
    {
        // Hidden (5)
        ("Hiddenleaf","2fd0b0"), ("Mistroot","49c0a8"), ("Veilberry","2bb6c0"), ("Shadebloom","3a9aa8"), ("Gloomfruit","2f8a90"),
        // Alpha (5)
        ("Alpha Sprout","c8fff0"), ("Prime Pod","a8ffe6"), ("Origin Bloom","d0fff5"), ("Apex Berry","b0f5e0"), ("First Fruit","e0fff8"),
        // Strange (5) — always Gold / Rainbow / Giant
        ("Strangefruit","ff4dd2"), ("Warpberry","ff6ad0"), ("Oddbloom","ff3dba"), ("Glitchgourd","ff7ae0"), ("Anomaly Pod","ff52c8"),
        // Celestial (5)
        ("Star Lotus","6a8cff"), ("Comet Bloom","7aa0ff"), ("Aurora Fruit","8ab0ff"), ("Nebula Berry","5a78ff"), ("Galaxy Bloom","6a6aff"),
        // Infinite (5)
        ("Infinite Bloom","ffd0ff"), ("Endless Berry","f0c0ff"), ("Omega Fruit","ffe0ff"), ("Boundless Bloom","e8c8ff"), ("Forever Seed","fff0ff"),
    };

    // ---- Uni-Grape catalog construction -----------------------------------

    private static List<SeedType> BuildUni()
    {
        const double baseCost = 1e22;   // beyond the Hidden Grove / Centurnial pack
        const double costRatio = 1.6;

        var list = new List<SeedType>(UniEntries.Length);
        for (int i = 0; i < UniEntries.Length; i++)
        {
            string rarity = UniRarityForIndex(i);
            double cost = NiceRound(baseCost * Math.Pow(costRatio, i));
            double value = NiceRound(cost * (1.9 + RarityBonus(rarity)));
            float grow = (float)Math.Round(120.0 * Math.Pow(1.05, i));

            list.Add(new SeedType
            {
                Name = UniEntries[i].Name,
                Rarity = rarity,
                Cost = cost,
                BaseValue = value,
                GrowSeconds = grow,
                Color = new Color(UniEntries[i].Hex),
                Shape = ShapeFor(UniEntries[i].Name, rarity),
            });
        }
        return list;
    }

    private static string UniRarityForIndex(int i)
    {
        if (i < 10) return "Dimensional";
        if (i < 20) return "Galaxy";
        if (i < 29) return "Solar";
        return "Blackhole";
    }

    private static readonly (string Name, string Hex)[] UniEntries =
    {
        // Dimensional (10)
        ("Dimensional Seed","b15cff"), ("Rift Berry","9a5cff"), ("Warp Grape","c060ff"), ("Portal Pod","a64dff"),
        ("Fracture Fruit","8a5cff"), ("Paradox Plum","b070ff"), ("Echo Bloom","9060e0"), ("Mirror Melon","c87aff"),
        ("Phase Berry","a050ff"), ("Tesseract Seed","b88aff"),
        // Galaxy (10)
        ("Galaxy Seed","4a7aff"), ("Starfield Bloom","6a8aff"), ("Cosmos Fruit","3a5aff"), ("Milkyway Melon","5a6aff"),
        ("Andromeda Berry","7a9aff"), ("Supernova Pod","4060e0"), ("Comet Grape","6a7aff"), ("Pulsar Plum","5a8aff"),
        ("Orbit Bloom","8aa0ff"), ("Stardust Seed","4a6aff"),
        // Solar (9)
        ("Solar Seed","ffb02a"), ("Sunflare Bloom","ffd24a"), ("Corona Fruit","ff8a1a"), ("Helios Berry","ffc040"),
        ("Plasma Pod","ff9a30"), ("Radiance Grape","ffaa50"), ("Solaris Melon","ffb838"), ("Flarefruit","ffc868"),
        ("Heliosphere Seed","ff8a3a"),
        // Blackhole (4) — the ultimate
        ("Blackhole Seed","8a30ff"), ("Event Horizon","6a1aaa"), ("Singularity Core","9a40ff"), ("Void Fruit","5a2a8a"),
    };

    // ---- Pets -------------------------------------------------------------

    public sealed class Pet
    {
        public required string Name { get; init; }
        public required string Tier { get; init; }
        public required string Kind { get; init; }   // "yield" (+sell%) or "speed" (+growth%)
        public required double Percent { get; init; }  // bonus as a fraction (0.15 = +15%)
        public required double Cost { get; init; }
    }

    private static List<Pet>? _pets;

    /// <summary>Two pets per rarity tier from Legendary up to Eternal (everything before Admin).
    /// Each tier has a "yield" pet (+sell value) and a "speed" pet (+growth speed).</summary>
    public static List<Pet> Pets => _pets ??= BuildPets();

    private static List<Pet> BuildPets()
    {
        // tier, yield%, speed%, cost, yield-pet name, speed-pet name
        (string Tier, double Yield, double Speed, double Cost, string YName, string SName)[] defs =
        {
            ("Legendary", 0.15, 0.10, 5e15,  "Golden Pup",     "Cheetah"),
            ("Secret",    0.25, 0.16, 1.5e16, "Shadow Cat",    "Phantom Fox"),
            ("Divine",    0.40, 0.24, 4e16,  "Seraph Owl",     "Halo Hare"),
            ("Ultra",     0.65, 0.34, 1.2e17, "Neon Lynx",     "Pulse Hawk"),
            ("Titan",     1.00, 0.48, 3e17,  "Mammoth",        "Raptor"),
            ("Entity",    1.50, 0.65, 9e17,  "Void Stag",      "Wraith Wolf"),
            ("Eternal",   2.20, 0.85, 2.5e18, "Cosmic Phoenix","Chrono Sprite"),
        };

        var list = new List<Pet>(defs.Length * 2);
        foreach (var d in defs)
        {
            list.Add(new Pet { Name = d.YName, Tier = d.Tier, Kind = "yield", Percent = d.Yield, Cost = d.Cost });
            list.Add(new Pet { Name = d.SName, Tier = d.Tier, Kind = "speed", Percent = d.Speed, Cost = d.Cost * 1.1 });
        }
        return list;
    }

    public static Pet? PetByName(string name)
    {
        foreach (Pet p in Pets)
            if (p.Name == name) return p;
        return null;
    }

    private static string RarityForIndex(int i)
    {
        if (i < CommonCount) return "Common";
        if (i < CommonCount + UncommonCount) return "Uncommon";
        if (i < CommonCount + UncommonCount + RareCount) return "Rare";
        if (i < CommonCount + UncommonCount + RareCount + EpicCount) return "Epic";
        if (i < CommonCount + UncommonCount + RareCount + EpicCount + LegendaryCount) return "Legendary";
        if (i < CommonCount + UncommonCount + RareCount + EpicCount + LegendaryCount + SecretCount) return "Secret";
        return "Divine";
    }

    /// <summary>Maps each seed to the 3D form it grows into, so it resembles its namesake.</summary>
    private static PlantShape ShapeFor(string name, string rarity) => name switch
    {
        "Carrot" or "Potato" or "Radish" or "Onion" or "Beet" or "Turnip" or "Garlic" or "Emberroot" => PlantShape.Root,
        "Lettuce" or "Cabbage" or "Spinach" or "Broccoli" => PlantShape.Leafy,
        "Tomato" or "Pea" or "Green Bean" or "Bell Pepper" or "Strawberry" or "Raspberry" or "Phoenix Pepper" => PlantShape.Bush,
        "Cucumber" or "Zucchini" or "Pumpkin" or "Watermelon" or "Cantaloupe" or "Honeydew" or "Durian"
            or "Frostmelon" or "Glowgourd" or "Storm Melon" or "Titan Squash" or "Galaxy Gourd" => PlantShape.Gourd,
        "Apple" or "Pear" or "Peach" or "Plum" or "Cherry" or "Apricot" or "Fig" or "Kiwi" or "Orange" or "Lemon"
            or "Lime" or "Mango" or "Papaya" or "Coconut" or "Avocado" or "Passionfruit" or "Guava" or "Pomegranate"
            or "Golden Apple" or "Mistplum" or "Thornpear" or "Cloudpeach" or "Honeyglobe" or "Voidplum" => PlantShape.TreeFruit,
        "Blueberry" or "Grape" or "Lychee" or "Moonberry" or "Aurora Vine" or "Prism Grape" => PlantShape.Cluster,
        "Corn" => PlantShape.Corn,
        "Banana" => PlantShape.Banana,
        "Pineapple" => PlantShape.Pineapple,
        "Crystal Berry" or "Wishberry" => PlantShape.Crystal,
        "Starfruit" or "Sunfruit" or "Dragonfruit" => PlantShape.Star,
        "Stardust Bloom" or "Dragon Lily" or "Lava Lotus" or "Eclipse Bloom" or "Spectral Rose" or "Eternal Lotus" => PlantShape.Flower,
        "Genesis Seed" => PlantShape.Divine,
        _ => rarity switch
        {
            "Secret" => PlantShape.Crystal,
            "Legendary" => PlantShape.Flower,
            "Divine" => PlantShape.Divine,
            "Ultra" => PlantShape.Crystal,
            "Titan" => PlantShape.Gourd,
            "Entity" => PlantShape.Star,
            "Eternal" => PlantShape.Flower,
            "Admin" => PlantShape.Divine,
            "Hidden" => PlantShape.Crystal,
            "Alpha" => PlantShape.Star,
            "Strange" => PlantShape.Flower,
            "Celestial" => PlantShape.Divine,
            "Infinite" => PlantShape.Divine,
            "Dimensional" => PlantShape.Crystal,
            "Galaxy" => PlantShape.Star,
            "Solar" => PlantShape.Star,
            "Blackhole" => PlantShape.Divine,
            _ => PlantShape.Bush,
        },
    };

    private static double RarityBonus(string rarity) => rarity switch
    {
        "Uncommon"  => 0.10,
        "Rare"      => 0.20,
        "Epic"      => 0.35,
        "Legendary" => 0.50,
        "Secret"    => 0.70,
        "Divine"    => 1.00,
        "Ultra"     => 1.30,
        "Titan"     => 1.60,
        "Entity"    => 2.00,
        "Eternal"   => 2.50,
        "Admin"     => 3.00,
        "Hidden"    => 1.20,
        "Alpha"     => 1.60,
        "Strange"   => 2.20,
        "Celestial" => 3.00,
        "Infinite"  => 4.00,
        "Dimensional"=> 5.00,
        "Galaxy"    => 7.00,
        "Solar"     => 9.00,
        "Blackhole" => 15.00,
        _            => 0.0,
    };

    /// <summary>Round to ~2 significant figures so prices read as tidy numbers.</summary>
    private static double NiceRound(double v)
    {
        if (v < 10) return Math.Max(1, Math.Round(v));
        int digits = (int)Math.Floor(Math.Log10(v));
        double pow = Math.Pow(10, digits - 1);
        return Math.Round(v / pow) * pow;
    }

    // ---- the 76 seeds (cheapest Common .. the Divine Genesis Seed) --------

    private static readonly (string Name, string Hex)[] Entries =
    {
        // --- Common (20) — everyday garden produce ---
        ("Carrot","ff8c2b"), ("Potato","c9a26b"), ("Radish","e0506a"), ("Lettuce","7fc24a"),
        ("Cabbage","9fd17a"), ("Onion","d9c27a"), ("Tomato","e23b2e"), ("Cucumber","4f9e3a"),
        ("Corn","f4d03f"), ("Pea","6fbf4a"), ("Green Bean","8fae5a"), ("Spinach","3f8f3a"),
        ("Beet","a23355"), ("Turnip","e8d2e0"), ("Garlic","ece3d0"), ("Bell Pepper","d83a2a"),
        ("Zucchini","4a8f3a"), ("Broccoli","3f7a3f"), ("Pumpkin","f08a24"), ("Strawberry","e3354b"),

        // --- Uncommon (16) — berries & orchard fruit ---
        ("Blueberry","4060d6"), ("Raspberry","c83a5a"), ("Watermelon","3fa14a"), ("Cantaloupe","e8b06a"),
        ("Honeydew","b6d96a"), ("Apple","d83a3a"), ("Pear","c2d24a"), ("Peach","ffb07a"),
        ("Plum","7a3a8f"), ("Cherry","c41e3a"), ("Apricot","f0a24a"), ("Grape","6a3a9f"),
        ("Fig","7a4a6a"), ("Kiwi","8fae3a"), ("Orange","ff8c1a"), ("Lemon","f4e04a"),

        // --- Rare (14) — tropical & exotic ---
        ("Lime","9fd13a"), ("Mango","f0a01a"), ("Pineapple","e8c24a"), ("Banana","f4d84a"),
        ("Papaya","ee9a4a"), ("Coconut","b0825a"), ("Avocado","5a7a3a"), ("Passionfruit","6a3a7a"),
        ("Guava","e07a8a"), ("Lychee","e05a6a"), ("Dragonfruit","ff4d8d"), ("Starfruit","f4d24a"),
        ("Durian","c2b04a"), ("Pomegranate","b5302f"),

        // --- Epic (12) — magical crops ---
        ("Golden Apple","ffd24a"), ("Moonberry","7a8aff"), ("Sunfruit","ff9a2a"), ("Crystal Berry","8af0ff"),
        ("Frostmelon","aef0ff"), ("Emberroot","ff6a3a"), ("Stardust Bloom","b0a0ff"), ("Glowgourd","c8ff6a"),
        ("Mistplum","9a8ab0"), ("Honeyglobe","ffcf5a"), ("Thornpear","7a9a4a"), ("Cloudpeach","ffc8d8"),

        // --- Legendary (8) — elemental titans ---
        ("Phoenix Pepper","ff5a1a"), ("Voidplum","4a2a6a"), ("Aurora Vine","6affc8"), ("Dragon Lily","ff3a6a"),
        ("Storm Melon","6a8aff"), ("Prism Grape","c86aff"), ("Titan Squash","e0a83a"), ("Lava Lotus","ff4a2a"),

        // --- Secret (5) — whispered-about wonders ---
        ("Eclipse Bloom","6a3a8a"), ("Galaxy Gourd","5a4aff"), ("Spectral Rose","ff5ad0"),
        ("Eternal Lotus","f0d0ff"), ("Wishberry","9affd0"),

        // --- Divine (1) — the one at the very top ---
        ("Genesis Seed","ffe6a0"),
    };
}
