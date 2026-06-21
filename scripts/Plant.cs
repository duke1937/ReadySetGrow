using Godot;
using System.Collections.Generic;

namespace GrowDaGarden;

/// <summary>A built plant model: the mesh tree, the materials that should glow
/// (the edible parts), and whether it glows by default (magical crops).</summary>
public sealed class PlantViz
{
    public required Node3D Root;
    public required List<StandardMaterial3D> Glow;
    public required bool Magic;
}

/// <summary>
/// Builds a stylised 3D model for a crop from primitive meshes so each plant
/// resembles its namesake (carrot, tomato bush, melon, apple tree, grapes, …).
/// Models are built at "full grown, size 1" scale with their base on the dirt
/// (local y = 0); <see cref="Plot3D"/> scales them by growth and per-plant size.
/// </summary>
public static class PlantVisual
{
    private static readonly Color Stem = new("2f8f3a");
    private static readonly Color Leaf = new("3fae4a");
    private static readonly Color BushGreen = new("357a39");
    private static readonly Color Trunk = new("6b4a2c");

    public static PlantViz Build(SeedType seed)
    {
        var root = new Node3D();
        var glow = new List<StandardMaterial3D>();
        Color c = seed.Color;
        bool magic = false;

        switch (seed.Shape)
        {
            case PlantShape.Root: Root(root, c, glow); break;
            case PlantShape.Leafy: Leafy(root, c, glow); break;
            case PlantShape.Bush: Bush(root, c, glow); break;
            case PlantShape.Gourd: Gourd(root, c, glow); break;
            case PlantShape.TreeFruit: Tree(root, c, glow); break;
            case PlantShape.Cluster: Cluster(root, c, glow); break;
            case PlantShape.Corn: Corn(root, c, glow); break;
            case PlantShape.Banana: Banana(root, c, glow); break;
            case PlantShape.Pineapple: Pineapple(root, c, glow); break;
            case PlantShape.Crystal: Crystal(root, c, glow); magic = true; break;
            case PlantShape.Star: Star(root, c, glow); magic = true; break;
            case PlantShape.Flower: Flower(root, c, glow); magic = true; break;
            case PlantShape.Divine: Divine(root, c, glow); magic = true; break;
            case PlantShape.Bamboo: Bamboo(root, c, glow); magic = true; break;
            case PlantShape.Mushroom: Mushroom(root, c, glow); magic = true; break;
            case PlantShape.Snapdragon: Snapdragon(root, c, glow); magic = true; break;
            case PlantShape.BurningBud: BurningBud(root, c, glow); magic = true; break;
            case PlantShape.SugarApple: SugarApple(root, c, glow); magic = true; break;
            case PlantShape.CandyCane: CandyCane(root, c, glow); magic = true; break;
            default: Bush(root, c, glow); break;
        }

        return new PlantViz { Root = root, Glow = glow, Magic = magic };
    }

    // ---- shape builders ---------------------------------------------------

    private static void Root(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        Add(p, Cone(0.16f, 0.5f), new Vector3(0, 0.25f, 0), Fruit(c, g));        // taproot
        var lm = M(Leaf, 0.8f);
        for (int i = 0; i < 4; i++)
            Add(p, Cone(0.045f, 0.32f), new Vector3(0, 0.52f, 0), lm, rot: new Vector3(26, i * 90f, 0)); // leafy top
    }

    private static void Leafy(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        Add(p, Sph(0.3f), new Vector3(0, 0.28f, 0), Fruit(c, g, 0.85f), new Vector3(1.1f, 0.9f, 1.1f)); // head
        var lm = M(Leaf, 0.8f);
        for (int i = 0; i < 5; i++)
        {
            float a = Mathf.DegToRad(i * 72f);
            Add(p, Sph(0.17f), new Vector3(Mathf.Cos(a) * 0.22f, 0.16f, Mathf.Sin(a) * 0.22f), lm,
                new Vector3(1.3f, 0.35f, 1.3f), new Vector3(0, i * 72f, 16));
        }
    }

    private static void Bush(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        Add(p, Sph(0.26f), new Vector3(0, 0.24f, 0), M(BushGreen, 0.85f), new Vector3(1.1f, 0.85f, 1.1f)); // bush
        var fm = Fruit(c, g);
        Add(p, Sph(0.12f), new Vector3(0.15f, 0.32f, 0.05f), fm);
        Add(p, Sph(0.11f), new Vector3(-0.13f, 0.3f, -0.08f), fm);
        Add(p, Sph(0.1f), new Vector3(0.02f, 0.42f, 0.12f), fm);
    }

    private static void Gourd(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        Add(p, Sph(0.32f), new Vector3(0, 0.3f, 0), Fruit(c, g, 0.5f), new Vector3(1.25f, 0.95f, 1.25f)); // big fruit
        Add(p, Cyl(0.04f, 0.12f), new Vector3(0, 0.62f, 0), M(Stem, 0.8f));                                 // stem nub
        Add(p, Sph(0.16f), new Vector3(0.32f, 0.16f, 0.12f), M(Leaf, 0.8f), new Vector3(1.4f, 0.22f, 1.0f)); // vine leaf
    }

    private static void Tree(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        Add(p, Cyl(0.06f, 0.55f), new Vector3(0, 0.28f, 0), M(Trunk, 0.9f));                                  // trunk
        Add(p, Sph(0.3f), new Vector3(0, 0.66f, 0), M(BushGreen, 0.85f), new Vector3(1.15f, 0.95f, 1.15f));   // canopy
        var fm = Fruit(c, g);
        Add(p, Sph(0.12f), new Vector3(0.2f, 0.54f, 0.08f), fm);
        Add(p, Sph(0.12f), new Vector3(-0.18f, 0.6f, -0.05f), fm);
        Add(p, Sph(0.11f), new Vector3(0.05f, 0.5f, -0.2f), fm);
    }

    private static void Cluster(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        Add(p, Cyl(0.035f, 0.4f), new Vector3(0, 0.5f, 0), M(Stem, 0.8f));
        Add(p, Sph(0.15f), new Vector3(0.12f, 0.66f, 0), M(Leaf, 0.8f), new Vector3(1.4f, 0.3f, 1.0f));
        var fm = Fruit(c, g, 0.4f);
        int[] rows = { 3, 3, 2, 1 };
        for (int r = 0; r < rows.Length; r++)
        {
            float ry = 0.5f - r * 0.11f;
            float rad = 0.13f * (1f - r * 0.18f);
            for (int i = 0; i < rows[r]; i++)
            {
                float a = Mathf.DegToRad(360f / rows[r] * i + r * 30f);
                Add(p, Sph(0.075f), new Vector3(Mathf.Cos(a) * rad, ry, Mathf.Sin(a) * rad), fm);
            }
        }
    }

    private static void Corn(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        Add(p, Cyl(0.05f, 1.0f), new Vector3(0, 0.5f, 0), M(Stem, 0.85f));
        var lm = M(Leaf, 0.8f);
        Add(p, Sph(0.2f), new Vector3(0.18f, 0.6f, 0), lm, new Vector3(1.7f, 0.18f, 0.5f), new Vector3(0, 0, 35));
        Add(p, Sph(0.2f), new Vector3(-0.18f, 0.4f, 0), lm, new Vector3(1.7f, 0.18f, 0.5f), new Vector3(0, 0, -35));
        Add(p, Sph(0.1f), new Vector3(0.12f, 0.5f, 0.05f), Fruit(c, g, 0.5f), new Vector3(1f, 2.4f, 1f), new Vector3(10, 0, 8)); // cob
    }

    private static void Banana(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        Add(p, Cyl(0.05f, 0.5f), new Vector3(0, 0.5f, 0), M(Stem, 0.8f));
        var fm = Fruit(c, g, 0.5f);
        for (int i = 0; i < 5; i++)
        {
            float a = i * 72f;
            float ar = Mathf.DegToRad(a);
            Add(p, Cyl(0.05f, 0.34f), new Vector3(Mathf.Cos(ar) * 0.12f, 0.58f, Mathf.Sin(ar) * 0.12f), fm,
                rot: new Vector3(42, a, 0));
        }
    }

    private static void Pineapple(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        Add(p, Sph(0.22f), new Vector3(0, 0.42f, 0), Fruit(c, g, 0.6f), new Vector3(1f, 1.5f, 1f)); // body
        var lm = M(Leaf, 0.8f);
        for (int i = 0; i < 6; i++)
            Add(p, Cone(0.05f, 0.3f), new Vector3(0, 0.78f, 0), lm, rot: new Vector3(24, i * 60f, 0)); // crown
    }

    private static void Crystal(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        var fm = Fruit(c, g, 0.2f, 0.35f);
        Add(p, Cone(0.16f, 0.42f), new Vector3(0, 0.55f, 0), fm);
        Add(p, Cone(0.16f, 0.3f), new Vector3(0, 0.34f, 0), fm, rot: new Vector3(180, 0, 0));
        Add(p, Sph(0.06f), new Vector3(0.22f, 0.6f, 0), fm);
        Add(p, Sph(0.05f), new Vector3(-0.2f, 0.46f, 0.1f), fm);
    }

    private static void Star(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        Add(p, Cyl(0.04f, 0.45f), new Vector3(0, 0.4f, 0), M(Stem, 0.8f));
        var fm = Fruit(c, g, 0.3f);
        Add(p, Sph(0.1f), new Vector3(0, 0.68f, 0), fm, new Vector3(1, 0.5f, 1));
        for (int i = 0; i < 5; i++)
            Add(p, Bx(0.07f, 0.05f, 0.24f), new Vector3(0, 0.68f, 0), fm, rot: new Vector3(0, i * 72f, 0),
                pivot: new Vector3(0, 0, 0.16f));
    }

    private static void Flower(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        Add(p, Cyl(0.04f, 0.55f), new Vector3(0, 0.32f, 0), M(Stem, 0.8f));
        Add(p, Sph(0.14f), new Vector3(0.14f, 0.34f, 0), M(Leaf, 0.8f), new Vector3(1.5f, 0.22f, 0.8f));
        var fm = Fruit(c, g, 0.4f);
        Add(p, Sph(0.09f), new Vector3(0, 0.66f, 0), fm); // center
        for (int i = 0; i < 7; i++)
        {
            float a = Mathf.DegToRad(360f / 7f * i);
            Add(p, Sph(0.1f), new Vector3(Mathf.Cos(a) * 0.16f, 0.66f, Mathf.Sin(a) * 0.16f), fm,
                new Vector3(1.1f, 0.4f, 0.7f), new Vector3(0, -360f / 7f * i, 0));
        }
    }

    private static void Divine(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        Add(p, Cyl(0.05f, 0.55f), new Vector3(0, 0.3f, 0), M(new Color("d8c070"), 0.5f, 0.4f)); // golden stalk
        var fm = Fruit(c, g, 0.2f, 0.4f);
        Add(p, Sph(0.24f), new Vector3(0, 0.78f, 0), fm);                                       // central orb
        var halo = Fruit(new Color("fff2b0"), g, 0.3f);
        Add(p, new TorusMesh { InnerRadius = 0.28f, OuterRadius = 0.34f }, new Vector3(0, 0.78f, 0), halo, rot: new Vector3(80, 0, 0));
        for (int i = 0; i < 4; i++)
        {
            float a = Mathf.DegToRad(i * 90f);
            Add(p, Cone(0.05f, 0.16f), new Vector3(Mathf.Cos(a) * 0.4f, 0.78f, Mathf.Sin(a) * 0.4f), fm);
        }
    }

    private static void Bamboo(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        var stalk = Fruit(c, g, 0.55f);
        var node = M(new Color("3a7a3a"), 0.7f);
        const int segs = 9;
        const float seg = 0.32f;
        const float radius = 0.22f;
        for (int i = 0; i < segs; i++)
        {
            float ang = Mathf.DegToRad(i * 55f);              // spiral up
            float t = i / (float)(segs - 1);
            float x = Mathf.Sin(ang) * radius * (0.4f + t);
            float z = Mathf.Cos(ang) * radius * (0.4f + t);
            float y = 0.18f + i * seg;
            Add(p, Cyl(0.085f, seg), new Vector3(x, y, z), stalk);
            Add(p, Cyl(0.1f, 0.05f), new Vector3(x, y + seg * 0.5f, z), node); // node ring
        }
        var lm = M(Leaf, 0.8f);
        float topAng = Mathf.DegToRad((segs - 1) * 55f);
        float tx = Mathf.Sin(topAng) * radius * 1.3f, tz = Mathf.Cos(topAng) * radius * 1.3f;
        Add(p, Sph(0.18f), new Vector3(tx + 0.12f, 0.18f + segs * seg, tz), lm, new Vector3(1.8f, 0.18f, 0.5f), new Vector3(0, 30, 40));
        Add(p, Sph(0.18f), new Vector3(tx - 0.12f, 0.18f + (segs - 0.5f) * seg, tz), lm, new Vector3(1.8f, 0.18f, 0.5f), new Vector3(0, -20, -40));
    }

    private static void Mushroom(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        Add(p, Cyl(0.18f, 0.7f), new Vector3(0, 0.35f, 0), M(new Color("e8e0c8"), 0.8f)); // stem
        Add(p, Sph(0.6f), new Vector3(0, 0.78f, 0), Fruit(c, g, 0.5f), new Vector3(1.3f, 0.72f, 1.3f)); // cap
        var spot = M(new Color("fff0d8"), 0.7f);
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.DegToRad(i * 60f);
            Add(p, Sph(0.08f), new Vector3(Mathf.Cos(a) * 0.42f, 0.9f, Mathf.Sin(a) * 0.42f), spot);
        }
    }

    private static void Snapdragon(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        Add(p, Cyl(0.06f, 1.4f), new Vector3(0, 0.7f, 0), M(Stem, 0.8f));
        var lm = M(Leaf, 0.8f);
        Add(p, Sph(0.16f), new Vector3(0.12f, 0.35f, 0), lm, new Vector3(1.4f, 0.2f, 0.6f), new Vector3(0, 0, 30));
        Add(p, Sph(0.16f), new Vector3(-0.12f, 0.6f, 0), lm, new Vector3(1.4f, 0.2f, 0.6f), new Vector3(0, 0, -30));
        var fm = Fruit(c, g, 0.4f);
        for (int i = 0; i < 6; i++)
        {
            float y = 0.85f + i * 0.18f;
            float side = (i % 2 == 0) ? 0.12f : -0.12f;
            Add(p, Sph(0.13f), new Vector3(side, y, 0), fm, new Vector3(1.2f, 1.0f, 1.0f));
        }
        Add(p, Sph(0.12f), new Vector3(0, 0.85f + 6 * 0.18f, 0), fm); // top bud
    }

    private static void BurningBud(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        // A very long flower whose stem leans to the left as it rises.
        var stemMat = M(Stem, 0.8f);
        const int seg = 6;
        const float segH = 0.45f;
        for (int i = 0; i < seg; i++)
        {
            float t = i / (float)(seg - 1);
            float x = -0.55f * t;     // lean left
            Add(p, Cyl(0.07f, segH + 0.06f), new Vector3(x, 0.25f + i * segH, 0), stemMat);
        }
        float topX = -0.55f, topY = 0.25f + seg * segH;
        var fm = Fruit(c, g, 0.4f);
        Add(p, Sph(0.28f), new Vector3(topX, topY, 0), fm);            // fiery centre
        for (int i = 0; i < 8; i++)                                     // petals in a vertical fan
        {
            float a = Mathf.DegToRad(i * 45f);
            Add(p, Sph(0.17f), new Vector3(topX + Mathf.Cos(a) * 0.3f, topY + Mathf.Sin(a) * 0.3f, 0),
                fm, new Vector3(1.3f, 0.5f, 0.7f), new Vector3(0, 0, i * 45f));
        }
    }

    private static void SugarApple(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        Add(p, Cyl(0.09f, 1.4f), new Vector3(0, 0.7f, 0), M(Trunk, 0.9f));                                  // tall trunk
        Add(p, Sph(0.5f), new Vector3(0, 1.5f, 0), M(BushGreen, 0.85f), new Vector3(1.1f, 0.9f, 1.1f));     // canopy
        var fm = Fruit(c, g, 0.45f);
        Add(p, Sph(0.14f), new Vector3(0.28f, 1.3f, 0.05f), fm, new Vector3(0.9f, 1.7f, 0.9f));             // long green apples
        Add(p, Sph(0.14f), new Vector3(-0.26f, 1.46f, -0.05f), fm, new Vector3(0.9f, 1.7f, 0.9f));
        Add(p, Sph(0.13f), new Vector3(0.05f, 1.24f, -0.28f), fm, new Vector3(0.9f, 1.8f, 0.9f));
    }

    private static void CandyCane(Node3D p, Color c, List<StandardMaterial3D> g)
    {
        var red = Fruit(c, g, 0.4f);                       // c is red
        var white = Fruit(new Color("fff0f0"), g, 0.4f);
        const int seg = 8;
        const float segH = 0.18f;
        for (int i = 0; i < seg; i++)                       // striped vertical shaft
            Add(p, Cyl(0.09f, segH + 0.02f), new Vector3(0, 0.2f + i * segH, 0), (i % 2 == 0) ? red : white);
        float baseY = 0.2f + seg * segH;                    // the hook
        Add(p, Cyl(0.09f, 0.18f), new Vector3(0.08f, baseY + 0.02f, 0), red, rot: new Vector3(0, 0, 40));
        Add(p, Cyl(0.09f, 0.18f), new Vector3(0.22f, baseY + 0.06f, 0), white, rot: new Vector3(0, 0, 75));
        Add(p, Cyl(0.09f, 0.16f), new Vector3(0.34f, baseY - 0.02f, 0), red, rot: new Vector3(0, 0, 110));
    }

    // ---- helpers ----------------------------------------------------------

    private static StandardMaterial3D M(Color c, float rough = 0.6f, float metal = 0f) =>
        new() { AlbedoColor = c, Roughness = rough, Metallic = metal };

    /// <summary>An edible-part material (added to the glow list so it can light up).</summary>
    private static StandardMaterial3D Fruit(Color c, List<StandardMaterial3D> glow, float rough = 0.45f, float metal = 0f)
    {
        var m = M(c, rough, metal);
        glow.Add(m);
        return m;
    }

    /// <summary>Adds a mesh. <paramref name="pivot"/> offsets the mesh within its own
    /// node before rotation, so a rotated part can orbit an axis (used for star points).</summary>
    private static MeshInstance3D Add(
        Node3D parent, Mesh mesh, Vector3 pos, StandardMaterial3D mat,
        Vector3? scale = null, Vector3? rot = null, Vector3? pivot = null)
    {
        Node3D holder = parent;
        if (pivot.HasValue)
        {
            var h = new Node3D { Position = pos };
            if (rot.HasValue) h.RotationDegrees = rot.Value;
            parent.AddChild(h);
            holder = h;
            pos = pivot.Value;
            rot = null;
        }

        var mi = new MeshInstance3D { Mesh = mesh, Position = pos, MaterialOverride = mat };
        if (scale.HasValue) mi.Scale = scale.Value;
        if (rot.HasValue) mi.RotationDegrees = rot.Value;
        holder.AddChild(mi);
        return mi;
    }

    private static SphereMesh Sph(float r) => new() { Radius = r, Height = r * 2f };
    private static CylinderMesh Cyl(float r, float h) => new() { TopRadius = r, BottomRadius = r, Height = h };
    private static CylinderMesh Cone(float bottom, float h) => new() { TopRadius = 0.001f, BottomRadius = bottom, Height = h };
    private static BoxMesh Bx(float x, float y, float z) => new() { Size = new Vector3(x, y, z) };
}
