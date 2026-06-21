using Godot;
using System.Collections.Generic;

namespace GrowDaGarden;

/// <summary>
/// One planting spot on an open dirt field. Owns its <see cref="PlotState"/> and
/// shows a 3D model that resembles the crop (built by <see cref="PlantVisual"/>),
/// scaled by growth progress and the crop's random size. A thin
/// <see cref="StaticBody3D"/> on layer 2 lets the player's look-ray target it.
/// The spot's origin sits on the dirt surface (local y = 0).
/// </summary>
public partial class Plot3D : Node3D
{
    /// <summary>Index of this spot in the garden (stamped onto the look body's meta).</summary>
    public int Index;

    /// <summary>Pure game state for this spot.</summary>
    public PlotState State = new();

    /// <summary>Offset/extra-scale for multi-tile plants (e.g. Toxikit covering a 2x2 block).</summary>
    public Vector3 PlantOffset = Vector3.Zero;
    public float ExtraScale = 1f;

    private MeshInstance3D _marker = null!;
    private Label3D _label = null!;

    private Node3D? _plantRoot;
    private List<StandardMaterial3D> _glow = new();
    private bool _magic;
    private SeedType? _builtSeed;
    private float _bobPhase;

    public override void _Ready()
    {
        // "Plant here" ring shown on the empty dirt spot.
        _marker = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.16f, OuterRadius = 0.3f },
            Position = new Vector3(0, 0.03f, 0),
            RotationDegrees = new Vector3(90, 0, 0),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1, 1, 1, 0.16f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            },
        };
        AddChild(_marker);

        // Floating status label.
        _label = new Label3D
        {
            Position = new Vector3(0, 2.0f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 64,
            PixelSize = 0.004f,
            OutlineSize = 18,
            Modulate = Colors.White,
            OutlineModulate = new Color(0, 0, 0, 0.85f),
            NoDepthTest = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            RenderPriority = 2,
        };
        AddChild(_label);

        // Look target (layer 2 only — the player walks straight through it).
        var body = new StaticBody3D { CollisionLayer = 2, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(1.1f, 1.7f, 1.1f) },
            Position = new Vector3(0, 0.85f, 0),
        });
        body.SetMeta("kind", "plot");
        body.SetMeta("plot_index", Index);
        AddChild(body);

        Refresh(0f);
    }

    /// <summary>Push current state into the model. Cheap — safe to call per frame.</summary>
    public void Refresh(float delta)
    {
        _bobPhase += delta * 2.2f;

        // A spot covered by a neighbouring multi-tile plant shows nothing.
        if (State.SlaveOf >= 0)
        {
            _marker.Visible = false;
            if (_plantRoot is not null) { _plantRoot.QueueFree(); _plantRoot = null; _builtSeed = null; }
            _label.Text = "";
            return;
        }

        if (State.IsEmpty)
        {
            _marker.Visible = true;
            if (_plantRoot is not null) { _plantRoot.QueueFree(); _plantRoot = null; _builtSeed = null; }
            _label.Text = "";
            return;
        }

        _marker.Visible = false;
        SeedType seed = State.Seed!;
        if (_builtSeed != seed)
            Rebuild(seed);

        // Grow from a sprout to full size, then scale by the crop's own size roll
        // (and ExtraScale for multi-tile plants).
        float grow = Mathf.Lerp(0.2f, 1f, State.Progress);
        float scale = grow * State.Size * ExtraScale;
        bool ready = State.IsReady;
        bool mutated = State.IsMutated;
        float bob = ready ? Mathf.Sin(_bobPhase) * 0.04f : 0f;

        _plantRoot!.Scale = new Vector3(scale, scale, scale);
        _plantRoot.Position = PlantOffset + new Vector3(0, bob, 0);

        // Edible parts glow when ripe-and-mutated, or always for magical crops.
        bool glow = _magic || (ready && mutated);
        foreach (StandardMaterial3D m in _glow)
        {
            m.EmissionEnabled = glow;
            if (glow)
            {
                m.Emission = mutated ? State.Primary.Tint : seed.Color;
                m.EmissionEnergyMultiplier = mutated ? 2.0f : (ready ? 1.2f : 0.6f);
            }
        }

        _label.Position = PlantOffset + new Vector3(0, 1.3f * State.Size * ExtraScale + 0.6f, 0);
        if (ready)
        {
            string mut = mutated ? $"✨{State.Mutations.Count}× " : "";
            _label.Text = $"{mut}{SizeWord(State.Size)}{seed.Name}\n+{Num.Fmt(State.Payout)}";
            _label.Modulate = mutated ? State.Primary.Tint : new Color("ffe66d");
        }
        else
        {
            float remain = seed.GrowSeconds - State.Growth;
            _label.Text = $"{seed.Name}\n{FormatTime(remain)}";
            _label.Modulate = new Color(1, 1, 1, 0.9f);
        }
    }

    private void Rebuild(SeedType seed)
    {
        if (_plantRoot is not null)
            _plantRoot.QueueFree();

        PlantViz viz = PlantVisual.Build(seed);
        _plantRoot = viz.Root;
        _glow = viz.Glow;
        _magic = viz.Magic;
        AddChild(_plantRoot);
        _builtSeed = seed;
    }

    /// <summary>Word shown before the crop name for unusually small / large plants.</summary>
    public static string SizeWord(float s) =>
        s >= 1.45f ? "Giant " : s >= 1.25f ? "Big " : s <= 0.8f ? "Small " : "";

    private static string FormatTime(float seconds)
    {
        if (seconds < 0) seconds = 0;
        int s = Mathf.CeilToInt(seconds);
        if (s < 60) return $"{s}s";
        int m = s / 60;
        int r = s % 60;
        return $"{m}m {r:00}s";
    }
}
