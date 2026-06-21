using Godot;

namespace GrowDaGarden;

/// <summary>
/// A kid-friendly modal: answer 5 "tens + ones" addition questions
/// (e.g. 40 + 7). Solve all five and <see cref="OnComplete"/> fires.
/// Retry-until-correct, no penalties.
/// </summary>
public partial class MathQuiz : Control
{
    public System.Action? OnComplete;
    public System.Action? OnCancel;

    private const int Total = 3;

    private readonly (int A, int B)[] _q = new (int, int)[Total];
    private int _idx;
    private int _correct;

    private Label _progress = null!;
    private Label _problem = null!;
    private Label _feedback = null!;
    private LineEdit _input = null!;

    public override void _Ready()
    {
        // Fresh questions: a multiple of ten plus a ones digit.
        for (int i = 0; i < Total; i++)
        {
            int tens = ((int)(GD.Randi() % 9) + 1) * 10; // 10..90
            int ones = (int)(GD.Randi() % 9) + 1;          // 1..9
            _q[i] = (tens, ones);
        }

        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        // Dim everything behind the quiz and swallow clicks.
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.62f) };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.MouseFilter = MouseFilterEnum.Stop;
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(480, 0) };
        panel.AddThemeStyleboxOverride("panel", Box(new Color("2f4a2a"), 18));
        center.AddChild(panel);

        var margin = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(m, 24);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 16);
        margin.AddChild(box);

        var title = new Label
        {
            Text = "🌱 Grow All — solve 3 to grow everything!",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color("eaffd8"));
        box.AddChild(title);

        _progress = Centered(15, new Color("ffe066"));
        box.AddChild(_progress);

        _problem = Centered(54, Colors.White);
        box.AddChild(_problem);

        _input = new LineEdit
        {
            Alignment = HorizontalAlignment.Center,
            PlaceholderText = "answer",
            CustomMinimumSize = new Vector2(0, 52),
        };
        _input.AddThemeFontSizeOverride("font_size", 30);
        _input.TextSubmitted += _ => Submit();
        box.AddChild(_input);

        _feedback = Centered(16, new Color("ff8a7a"));
        _feedback.CustomMinimumSize = new Vector2(0, 22);
        box.AddChild(_feedback);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 12);
        buttons.Alignment = BoxContainer.AlignmentMode.Center;
        box.AddChild(buttons);

        var cancel = new Button { Text = "Cancel" };
        cancel.AddThemeFontSizeOverride("font_size", 16);
        cancel.MouseDefaultCursorShape = CursorShape.PointingHand;
        cancel.Pressed += () => { OnCancel?.Invoke(); QueueFree(); };
        buttons.AddChild(cancel);

        var submit = new Button { Text = "Check ✓", CustomMinimumSize = new Vector2(140, 44) };
        submit.AddThemeFontSizeOverride("font_size", 18);
        submit.MouseDefaultCursorShape = CursorShape.PointingHand;
        submit.Pressed += Submit;
        buttons.AddChild(submit);

        ShowQuestion();
    }

    private void ShowQuestion()
    {
        (int a, int b) = _q[_idx];
        _progress.Text = $"Question {_idx + 1} of {Total}   ·   {_correct} correct";
        _problem.Text = $"{a} + {b} = ?";
        _feedback.Text = "";
        _input.Text = "";
        _input.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void Submit()
    {
        (int a, int b) = _q[_idx];
        if (int.TryParse(_input.Text.Trim(), out int given) && given == a + b)
        {
            _correct++;
            _idx++;
            if (_idx >= Total)
            {
                OnComplete?.Invoke();
                QueueFree();
                return;
            }
            ShowQuestion();
        }
        else
        {
            _feedback.Text = "Not quite — try again! 🙂";
            _input.Text = "";
            _input.CallDeferred(Control.MethodName.GrabFocus);
        }
    }

    private static Label Centered(int fontSize, Color color)
    {
        var l = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", fontSize);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    private static StyleBoxFlat Box(Color bg, int radius) => new()
    {
        BgColor = bg,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        BorderColor = new Color("9be67a"),
        BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
        ContentMarginLeft = 6, ContentMarginRight = 6, ContentMarginTop = 6, ContentMarginBottom = 6,
    };
}
