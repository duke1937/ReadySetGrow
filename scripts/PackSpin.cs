using Godot;

namespace GrowDaGarden;

/// <summary>
/// The Mystery Pack roulette: a name spins through the possible fruits, slows
/// down, and lands on a random winner. <see cref="OnDone"/> fires with the
/// winning index when the player clicks Continue.
/// </summary>
public partial class PackSpin : Control
{
    public string[] Names = System.Array.Empty<string>();
    public Color[] Colors = System.Array.Empty<Color>();
    public System.Action<int>? OnDone;

    private const float SpinTime = 2.6f;

    private int _winner;
    private int _display;
    private float _elapsed;
    private float _tickAcc;
    private bool _landed;

    private Label _spin = null!;
    private Label _result = null!;
    private Button _continue = null!;

    public override void _Ready()
    {
        _winner = Names.Length > 0 ? (int)(GD.Randi() % (uint)Names.Length) : 0;

        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.66f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(520, 0) };
        panel.AddThemeStyleboxOverride("panel", Box(new Color("2b2440"), 18));
        center.AddChild(panel);

        var margin = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(m, 26);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 16);
        margin.AddChild(box);

        var title = new Label { Text = "🎁 Mystery Pack", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 26);
        title.AddThemeColorOverride("font_color", new Color("ffd0ff"));
        box.AddChild(title);

        _spin = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.Off };
        _spin.AddThemeFontSizeOverride("font_size", 48);
        _spin.CustomMinimumSize = new Vector2(0, 72);
        box.AddChild(_spin);

        var sub = new Label { Text = string.Join("   ·   ", Names), HorizontalAlignment = HorizontalAlignment.Center };
        sub.AddThemeFontSizeOverride("font_size", 14);
        sub.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.55f));
        box.AddChild(sub);

        _result = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _result.AddThemeFontSizeOverride("font_size", 20);
        _result.AddThemeColorOverride("font_color", new Color("9be67a"));
        _result.CustomMinimumSize = new Vector2(0, 28);
        box.AddChild(_result);

        _continue = new Button { Text = "Plant it! ✓", CustomMinimumSize = new Vector2(0, 46), Visible = false };
        _continue.AddThemeFontSizeOverride("font_size", 18);
        _continue.MouseDefaultCursorShape = CursorShape.PointingHand;
        _continue.Pressed += () => { OnDone?.Invoke(_winner); QueueFree(); };
        box.AddChild(_continue);

        Show(_display);
    }

    public override void _Process(double delta)
    {
        if (_landed)
            return;

        _elapsed += (float)delta;
        if (_elapsed < SpinTime)
        {
            _tickAcc += (float)delta;
            float interval = Mathf.Lerp(0.04f, 0.42f, _elapsed / SpinTime); // decelerate
            if (_tickAcc >= interval)
            {
                _tickAcc = 0f;
                _display = (_display + 1) % Mathf.Max(1, Names.Length);
                Show(_display);
            }
        }
        else
        {
            _landed = true;
            Show(_winner);
            _result.Text = $"🎉 You won {Names[_winner]}!";
            _continue.Visible = true;
            _continue.CallDeferred(Control.MethodName.GrabFocus);
        }
    }

    private void Show(int i)
    {
        if (Names.Length == 0) return;
        _spin.Text = Names[i];
        _spin.AddThemeColorOverride("font_color", Colors[i]);
    }

    private static StyleBoxFlat Box(Color bg, int radius) => new()
    {
        BgColor = bg,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        BorderColor = new Color("ff77ff"),
        BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
        ContentMarginLeft = 6, ContentMarginRight = 6, ContentMarginTop = 6, ContentMarginBottom = 6,
    };
}
