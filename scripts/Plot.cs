using Godot;

namespace GrowDaGarden;

/// <summary>
/// One clickable garden tile. Owns its <see cref="PlotState"/> and renders the
/// soil, the growing plant and its progress entirely with custom drawing —
/// no art assets required.
/// </summary>
public partial class Plot : Button
{
    /// <summary>Re-pointed by Main when switching worlds so one grid shows any level.</summary>
    public PlotState State = new();

    private Label _name = null!;
    private Label _status = null!;
    private ProgressBar _bar = null!;
    private StyleBoxFlat _bg = null!;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(150, 150);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        Text = "";
        ClipContents = true;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        FocusMode = FocusModeEnum.None;

        _bg = MakeBox(new Color("6f9350"));
        AddThemeStyleboxOverride("normal", _bg);
        AddThemeStyleboxOverride("hover", MakeBox(new Color("7da75b")));
        AddThemeStyleboxOverride("pressed", MakeBox(new Color("5c7d42")));
        AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        _name = MakeLabel(15, new Color("f4ffe9"));
        Pin(_name, top: 4, bottom: 24, height: true);
        AddChild(_name);

        _status = MakeLabel(13, new Color("ffffff"));
        _status.AnchorTop = 1; _status.AnchorBottom = 1;
        _status.OffsetTop = -40; _status.OffsetBottom = -20;
        _status.OffsetLeft = 4; _status.OffsetRight = -4;
        AddChild(_status);

        _bar = new ProgressBar
        {
            MinValue = 0, MaxValue = 1, ShowPercentage = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _bar.AnchorLeft = 0; _bar.AnchorRight = 1;
        _bar.AnchorTop = 1; _bar.AnchorBottom = 1;
        _bar.OffsetLeft = 8; _bar.OffsetRight = -8;
        _bar.OffsetTop = -15; _bar.OffsetBottom = -5;
        AddChild(_bar);

        Refresh();
    }

    /// <summary>Push current state into the visuals. Cheap — safe to call per frame.</summary>
    public void Refresh()
    {
        if (_name is null) return; // not built yet

        if (State.IsEmpty)
        {
            _name.Text = "Empty";
            _name.SelfModulate = new Color(1, 1, 1, 0.55f);
            _status.Text = "tap to plant";
            _bar.Visible = false;
            _bg.BorderWidthBottom = _bg.BorderWidthTop =
                _bg.BorderWidthLeft = _bg.BorderWidthRight = 0;
        }
        else
        {
            SeedType seed = State.Seed!;
            _name.Text = seed.Name;
            _name.SelfModulate = Colors.White;
            _bar.Visible = !State.IsReady;
            _bar.Value = State.Progress;

            if (State.IsReady)
            {
                string mut = State.Mutation.IsNormal ? "" : State.Mutation.Name + " ";
                _status.Text = $"{mut}+{Num.Fmt(State.Payout)}";
                _status.SelfModulate = State.Mutation.IsNormal
                    ? new Color("ffe66d") : State.Mutation.Tint;
                SetBorder(State.Mutation.IsNormal ? new Color("ffe66d") : State.Mutation.Tint, 3);
            }
            else
            {
                float remain = seed.GrowSeconds - State.Growth;
                _status.Text = FormatTime(remain);
                _status.SelfModulate = new Color(1, 1, 1, 0.8f);
                SetBorder(default, 0);
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float w = Size.X, h = Size.Y;
        float soilTop = h * 0.66f;

        // Soil bed.
        DrawRect(new Rect2(0, soilTop, w, h - soilTop), new Color("4e3424"));
        DrawRect(new Rect2(0, soilTop, w, 5), new Color("5f4030"));

        if (State.IsEmpty)
        {
            var hint = new Color(1, 1, 1, 0.16f);
            float cx = w / 2, cy = soilTop - 20;
            DrawRect(new Rect2(cx - 11, cy - 2.5f, 22, 5), hint);
            DrawRect(new Rect2(cx - 2.5f, cy - 11, 5, 22), hint);
            return;
        }

        SeedType seed = State.Seed!;
        float f = State.Progress;
        float groundY = soilTop + 2;
        float stemH = Mathf.Lerp(8f, h * 0.46f, f);
        float stemX = w / 2;

        // Stem.
        DrawRect(new Rect2(stemX - 3, groundY - stemH, 6, stemH), new Color("2f8f3a"));

        // Leaves grow with the plant.
        float leaf = Mathf.Lerp(4f, 17f, f);
        DrawCircle(new Vector2(stemX - leaf, groundY - stemH * 0.62f), leaf, new Color("3fae4a"));
        DrawCircle(new Vector2(stemX + leaf, groundY - stemH * 0.52f), leaf * 0.9f, new Color("48c554"));

        if (State.IsReady)
        {
            var fc = new Vector2(stemX, groundY - stemH - 4);
            const float r = 17f;
            DrawCircle(fc, r, seed.Color);
            DrawCircle(fc + new Vector2(-r * 0.32f, -r * 0.32f), r * 0.28f, new Color(1, 1, 1, 0.5f));
            if (!State.Mutation.IsNormal)
                DrawArc(fc, r + 4, 0, Mathf.Tau, 32, State.Mutation.Tint, 3f, true);
        }
    }

    private void SetBorder(Color c, int width)
    {
        _bg.BorderColor = c;
        _bg.BorderWidthTop = _bg.BorderWidthBottom =
            _bg.BorderWidthLeft = _bg.BorderWidthRight = width;
    }

    private static StyleBoxFlat MakeBox(Color bg) => new()
    {
        BgColor = bg,
        CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
        CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
        ContentMarginLeft = 4, ContentMarginRight = 4,
        ContentMarginTop = 4, ContentMarginBottom = 4,
    };

    private static Label MakeLabel(int fontSize, Color color)
    {
        var l = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.Off,
        };
        l.AddThemeFontSizeOverride("font_size", fontSize);
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.5f));
        l.AddThemeConstantOverride("shadow_offset_x", 1);
        l.AddThemeConstantOverride("shadow_offset_y", 1);
        return l;
    }

    private static void Pin(Control c, float top, float bottom, bool height)
    {
        c.AnchorLeft = 0; c.AnchorRight = 1;
        c.AnchorTop = 0; c.AnchorBottom = 0;
        c.OffsetLeft = 4; c.OffsetRight = -4;
        c.OffsetTop = top; c.OffsetBottom = bottom;
    }

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
