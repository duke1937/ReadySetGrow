using Godot;

namespace GrowDaGarden;

/// <summary>
/// A little 3D companion built from primitives that wanders around near the
/// player. <see cref="Home"/> is updated each frame to the player's position so
/// the pet trots about wherever you go.
/// </summary>
public partial class Pet3D : Node3D
{
    public Vector3 Home;            // wander centre (the player's position)

    private Vector3 _target;
    private float _retarget;
    private float _bob;
    private float _speed = 1.6f;

    public void Setup(Color color, string name, Vector3 start, float speed)
    {
        _speed = speed;
        Position = start;
        Home = start;
        _target = start;

        var body = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.6f };

        AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.28f, Height = 0.56f }, Position = new Vector3(0, 0.3f, 0), Scale = new Vector3(1.3f, 1f, 1.1f), MaterialOverride = body });
        AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.2f, Height = 0.4f }, Position = new Vector3(0, 0.42f, 0.28f), MaterialOverride = body });

        var ear = new SphereMesh { Radius = 0.08f, Height = 0.18f };
        AddChild(new MeshInstance3D { Mesh = ear, Position = new Vector3(-0.1f, 0.6f, 0.28f), Scale = new Vector3(1, 1.6f, 1), MaterialOverride = body });
        AddChild(new MeshInstance3D { Mesh = ear, Position = new Vector3(0.1f, 0.6f, 0.28f), Scale = new Vector3(1, 1.6f, 1), MaterialOverride = body });

        var eye = new StandardMaterial3D { AlbedoColor = new Color("181818") };
        AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.04f, Height = 0.08f }, Position = new Vector3(-0.07f, 0.46f, 0.46f), MaterialOverride = eye });
        AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.04f, Height = 0.08f }, Position = new Vector3(0.07f, 0.46f, 0.46f), MaterialOverride = eye });

        AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.1f, Height = 0.2f }, Position = new Vector3(0, 0.34f, -0.3f), MaterialOverride = body });

        foreach ((float lx, float lz) in new[] { (-0.14f, 0.16f), (0.14f, 0.16f), (-0.14f, -0.16f), (0.14f, -0.16f) })
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.05f, Height = 0.18f }, Position = new Vector3(lx, 0.1f, lz), MaterialOverride = body });

        AddChild(new Label3D
        {
            Text = name,
            Position = new Vector3(0, 1.0f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 40, PixelSize = 0.004f, OutlineSize = 10,
            Modulate = color, OutlineModulate = new Color(0, 0, 0, 0.8f),
        });
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Vector3 here = GlobalPosition; here.Y = 0;

        _retarget -= dt;
        if (_retarget <= 0f || here.DistanceTo(_target) < 0.6f)
        {
            float ang = GD.Randf() * Mathf.Tau;
            float rad = 1.5f + GD.Randf() * 4f;
            _target = new Vector3(Home.X + Mathf.Cos(ang) * rad, 0, Home.Z + Mathf.Sin(ang) * rad);
            _retarget = 2f + GD.Randf() * 3f;
        }

        Vector3 to = _target - here; to.Y = 0;
        if (to.Length() > 0.1f)
        {
            Vector3 dir = to.Normalized();
            Vector3 np = here + dir * _speed * dt;
            _bob += dt * 9f;
            GlobalPosition = new Vector3(np.X, Mathf.Abs(Mathf.Sin(_bob)) * 0.08f, np.Z);
            Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Z), 0);
        }
    }
}
