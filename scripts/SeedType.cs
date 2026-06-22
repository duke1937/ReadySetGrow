using Godot;

namespace GrowDaGarden;

/// <summary>The 3D growth form a crop is drawn as, so it resembles its namesake.</summary>
public enum PlantShape
{
    Root,       // carrot, potato, beet — taproot with a leafy top
    Leafy,      // lettuce, cabbage — a head of leaves
    Bush,       // tomato, pepper, strawberry — berries on a low bush
    Gourd,      // melon, pumpkin — a big fruit resting on the ground
    TreeFruit,  // apple, orange — fruit hanging on a small tree
    Cluster,    // grapes, blueberries — a hanging bunch
    Corn,       // a tall stalk with a cob
    Banana,     // a hanging bunch of curved fruit
    Pineapple,  // a body with a spiky crown
    Crystal,    // magical faceted gem (glows)
    Star,       // star-shaped fruit (glows)
    Flower,     // a bloom of petals (glows)
    Divine,     // a haloed orb with orbiting shards (glows)
    Bamboo,     // a very tall stalk that spirals upward
    Mushroom,   // a thick stem with a big spotted cap
    Snapdragon, // a tall flowering spike of blossoms
    BurningBud, // a very long flower that leans left
    SugarApple, // a tall tree of long green apples
    CandyCane,  // a striped candy-cane hook
    BigTree,    // an enormous tree (3x3 footprint)
}

/// <summary>A kind of plant the player can buy and grow.</summary>
public sealed class SeedType
{
    public required string Name { get; init; }
    public required string Rarity { get; init; }   // Common / Uncommon / Rare / Epic / Legendary / Secret / Mythical / Divine
    public required double Cost { get; init; }       // coins to buy one seed
    public required double BaseValue { get; init; }  // coins when harvested (before mutation bonus)
    public required float GrowSeconds { get; init; } // real seconds from plant -> ready
    public required Color Color { get; init; }       // fruit colour used when drawing
    public PlantShape Shape { get; init; } = PlantShape.Bush; // 3D growth form (defaults harmlessly for the 2D game)
    public int Footprint { get; init; } = 1;         // planting spots taken (2 = a 1x2 pair, 4 = a 2x2 block)
    public double RainbowMultiplier { get; init; } = 0; // if >0, Rainbow mutations on this crop use this instead of 25x

    /// <summary>Colour used to tag the rarity in the shop.</summary>
    public Color RarityColor => Rarity switch
    {
        "Uncommon"  => new Color("5db05d"),
        "Rare"      => new Color("4aa3ff"),
        "Epic"      => new Color("b46bff"),
        "Legendary" => new Color("ffb000"),
        "Secret"    => new Color("ff3df0"),
        "Mythical"  => new Color("ff5ad0"),
        "Divine"    => new Color("ffe06a"),
        "Ultra"     => new Color("00e5ff"),
        "Titan"     => new Color("ff7a1a"),
        "Entity"    => new Color("b14dff"),
        "Eternal"   => new Color("ffe9a8"),
        "Admin"     => new Color("ff2d55"),
        "Hidden"    => new Color("2fd0b0"),
        "Alpha"     => new Color("c8fff0"),
        "Strange"   => new Color("ff4dd2"),
        "Celestial" => new Color("6a8cff"),
        "Infinite"  => new Color("ffd0ff"),
        "Centurnial"=> new Color("ffe08a"),
        "Dimensional"=> new Color("b15cff"),
        "Galaxy"    => new Color("4a7aff"),
        "Solar"     => new Color("ffb02a"),
        "Blackhole" => new Color("8a30ff"),
        "Mystic"    => new Color("ff77ff"),
        _            => new Color("c8c8c8"), // Common
    };
}
