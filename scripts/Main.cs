using Godot;
using System.Collections.Generic;

namespace GrowDaGarden;

/// <summary>
/// Game root. Builds the whole UI in code, runs the grow/harvest economy across
/// three unlockable worlds, and persists progress (including offline growth).
/// </summary>
public partial class Main : Control
{
    private const string SavePath = "user://growdagarden_save.json";
    private const double AutosaveSeconds = 5.0;

    private double _coins = GameData.StartingCoins;
    private int _world;                  // active world index
    private int _unlocked = 1;           // how many worlds are unlocked (Garden only at start)

    // Pure per-world plot state ([world][plot]); all worlds keep growing in the
    // background. The 20 visible Plot nodes are re-pointed at the active world.
    private PlotState[][] _states = System.Array.Empty<PlotState[]>();
    private Plot[] _plots = System.Array.Empty<Plot>();
    private int[] _selectedByWorld = System.Array.Empty<int>();

    private readonly List<ShopRow> _shopRows = new();
    private VBoxContainer _shopList = null!;
    private Label _shopHeader = null!;
    private readonly List<Button> _tabs = new();

    private ColorRect _bg = null!;
    private Label _coinsLabel = null!;
    private Label _toast = null!;
    private float _toastTime;
    private double _saveTimer;

    private double _lastShopCoins = -1;
    private int _lastShopSelected = -1;
    private int _lastShopWorld = -1;

    private bool _auto = true;
    private double _autoTimer;
    private Button _autoBtn = null!;
    private bool _quizOpen;

    private sealed class ShopRow
    {
        public required SeedType Seed;
        public required Button Btn;
        public required StyleBoxFlat Box;
        public required RichTextLabel Info;
    }

    private int Selected
    {
        get => _selectedByWorld[_world];
        set => _selectedByWorld[_world] = value;
    }

    private List<SeedType> CurrentSeeds => GameData.Worlds[_world].Seeds;

    public override void _Ready()
    {
        int worldCount = GameData.Worlds.Count;
        _states = new PlotState[worldCount][];
        _selectedByWorld = new int[worldCount];
        for (int w = 0; w < worldCount; w++)
        {
            _states[w] = new PlotState[GameData.PlotCount];
            for (int i = 0; i < GameData.PlotCount; i++)
                _states[w][i] = new PlotState();
        }

        BuildUi();
        LoadGame();
        ApplyWorld();
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;

        // Every world keeps growing, even the ones we're not looking at.
        foreach (PlotState[] world in _states)
            foreach (PlotState st in world)
                Grow(st, dt);

        foreach (Plot p in _plots)
            p.Refresh();

        if (_auto && !_quizOpen)
        {
            _autoTimer += delta;
            if (_autoTimer >= 0.4)
            {
                _autoTimer = 0;
                AutoStep();
            }
        }

        CheckUnlocks();

        _coinsLabel.Text = $"🪙 {Num.Fmt(_coins)}";
        if (_coins != _lastShopCoins || Selected != _lastShopSelected || _world != _lastShopWorld)
        {
            UpdateShop();
            _lastShopCoins = _coins;
            _lastShopSelected = Selected;
            _lastShopWorld = _world;
        }

        if (_toastTime > 0f)
        {
            _toastTime -= dt;
            _toast.SelfModulate = new Color(
                _toast.SelfModulate.R, _toast.SelfModulate.G, _toast.SelfModulate.B,
                Mathf.Clamp(_toastTime, 0f, 1f));
        }

        _saveTimer += delta;
        if (_saveTimer >= AutosaveSeconds)
        {
            _saveTimer = 0;
            SaveGame();
        }
    }

    public override void _ExitTree() => SaveGame();

    // ---- growth -----------------------------------------------------------

    private static void Grow(PlotState st, float dt)
    {
        if (st.Seed is null || st.IsReady)
            return;

        st.Growth += dt;
        if (st.IsReady && !st.MutationRolled)
        {
            st.Mutation = Mutation.Roll();
            st.MutationRolled = true;
        }
    }

    private void CheckUnlocks()
    {
        if (_unlocked < 2 && _coins >= GameData.UnlockSpaceAt)
        {
            _unlocked = 2;
            UpdateTabs();
            ShowToast("🚀 SPACE UNLOCKED!  Tap the Space tab.", new Color("9bdcff"));
        }
        if (_unlocked < 3 && _coins >= GameData.UnlockHeavenAt)
        {
            _unlocked = 3;
            UpdateTabs();
            ShowToast("☁️ HEAVEN UNLOCKED!  Tap the Heaven tab.", new Color("ffe6a0"));
        }
    }

    // ---- UI ---------------------------------------------------------------

    private void BuildUi()
    {
        _bg = new ColorRect();
        AddChild(_bg);
        _bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var root = new MarginContainer();
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (string m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            root.AddThemeConstantOverride(m, 16);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        root.AddChild(col);

        BuildTopBar(col);
        BuildLevelTabs(col);

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 14);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        col.AddChild(body);

        BuildGarden(body);
        BuildShop(body);

        _toast = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _toast.AddThemeFontSizeOverride("font_size", 20);
        _toast.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
        _toast.AddThemeConstantOverride("shadow_offset_x", 1);
        _toast.AddThemeConstantOverride("shadow_offset_y", 1);
        AddChild(_toast);
        _toast.AnchorLeft = 0; _toast.AnchorRight = 1;
        _toast.AnchorTop = 1; _toast.AnchorBottom = 1;
        _toast.OffsetTop = -64; _toast.OffsetBottom = -30;
        _toast.SelfModulate = new Color(1, 1, 1, 0);
    }

    private void BuildTopBar(Control parent)
    {
        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", Card(new Color("2f4a2a"), 14));
        parent.AddChild(bar);

        var inner = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right" })
            inner.AddThemeConstantOverride(m, 16);
        foreach (string m in new[] { "margin_top", "margin_bottom" })
            inner.AddThemeConstantOverride(m, 10);
        bar.AddChild(inner);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        inner.AddChild(row);

        var title = new Label { Text = "🌱 Grow Da Garden" };
        title.AddThemeFontSizeOverride("font_size", 26);
        title.AddThemeColorOverride("font_color", new Color("eaffd8"));
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(title);

        _coinsLabel = new Label { Text = $"🪙 {Num.Fmt(_coins)}", VerticalAlignment = VerticalAlignment.Center };
        _coinsLabel.AddThemeFontSizeOverride("font_size", 24);
        _coinsLabel.AddThemeColorOverride("font_color", new Color("ffe066"));
        row.AddChild(_coinsLabel);

        var growAll = new Button { Text = "🌱 Grow All" };
        growAll.AddThemeFontSizeOverride("font_size", 16);
        growAll.MouseDefaultCursorShape = CursorShape.PointingHand;
        growAll.Pressed += OnGrowAllPressed;
        row.AddChild(growAll);

        _autoBtn = new Button { Text = "🤖 Auto: ON" };
        _autoBtn.AddThemeFontSizeOverride("font_size", 16);
        _autoBtn.MouseDefaultCursorShape = CursorShape.PointingHand;
        _autoBtn.Pressed += ToggleAuto;
        row.AddChild(_autoBtn);

        var harvest = new Button { Text = "Harvest All" };
        harvest.AddThemeFontSizeOverride("font_size", 16);
        harvest.MouseDefaultCursorShape = CursorShape.PointingHand;
        harvest.Pressed += HarvestAll;
        row.AddChild(harvest);
    }

    private void BuildLevelTabs(Control parent)
    {
        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", Card(new Color("23351f"), 12));
        parent.AddChild(bar);

        var inner = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            inner.AddThemeConstantOverride(m, 8);
        bar.AddChild(inner);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        inner.AddChild(row);

        for (int i = 0; i < GameData.Worlds.Count; i++)
        {
            int idx = i;
            var tab = new Button { CustomMinimumSize = new Vector2(0, 40) };
            tab.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            tab.AddThemeFontSizeOverride("font_size", 17);
            tab.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            tab.MouseDefaultCursorShape = CursorShape.PointingHand;
            tab.Pressed += () => OnTabPressed(idx);
            row.AddChild(tab);
            _tabs.Add(tab);
        }

        UpdateTabs();
    }

    private void BuildGarden(Control parent)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Card(new Color("3c5a30"), 16));
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        parent.AddChild(panel);

        var margin = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(m, 16);
        panel.AddChild(margin);

        var center = new CenterContainer();
        margin.AddChild(center);

        var grid = new GridContainer { Columns = GameData.GardenColumns };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        center.AddChild(grid);

        _plots = new Plot[GameData.PlotCount];
        for (int i = 0; i < _plots.Length; i++)
        {
            var plot = new Plot();
            _plots[i] = plot;
            grid.AddChild(plot);
            plot.Pressed += () => OnPlotPressed(plot);
        }
    }

    private void BuildShop(Control parent)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(340, 0) };
        panel.AddThemeStyleboxOverride("panel", Card(new Color("26331f"), 16));
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        parent.AddChild(panel);

        var margin = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(m, 14);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);

        _shopHeader = new Label();
        _shopHeader.AddThemeFontSizeOverride("font_size", 22);
        _shopHeader.AddThemeColorOverride("font_color", new Color("eaffd8"));
        box.AddChild(_shopHeader);

        var hint = new Label
        {
            Text = "Pick a seed, then tap soil to plant. Scroll for rarer crops.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        hint.AddThemeFontSizeOverride("font_size", 13);
        hint.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.6f));
        box.AddChild(hint);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        box.AddChild(scroll);

        _shopList = new VBoxContainer();
        _shopList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _shopList.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_shopList);
    }

    /// <summary>(Re)build the shop rows for the active world.</summary>
    private void PopulateShop()
    {
        foreach (Node c in _shopList.GetChildren())
            c.QueueFree();
        _shopRows.Clear();

        World world = GameData.Worlds[_world];
        _shopHeader.Text = $"{world.Icon} {world.Name} Shop  ·  {world.Seeds.Count} seeds";

        string lastRarity = "";
        for (int i = 0; i < world.Seeds.Count; i++)
        {
            SeedType seed = world.Seeds[i];
            int index = i;

            if (seed.Rarity != lastRarity)
            {
                lastRarity = seed.Rarity;
                var section = new Label { Text = seed.Rarity.ToUpper() };
                section.AddThemeFontSizeOverride("font_size", 13);
                section.AddThemeColorOverride("font_color", seed.RarityColor);
                _shopList.AddChild(section);
            }

            var btn = new Button
            {
                MouseDefaultCursorShape = CursorShape.PointingHand,
                CustomMinimumSize = new Vector2(0, 56),
            };
            btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var sbNormal = Card(new Color("36462b"), 10);
            sbNormal.ContentMarginLeft = 12;
            btn.AddThemeStyleboxOverride("normal", sbNormal);
            btn.AddThemeStyleboxOverride("hover", Card(new Color("415434"), 10));
            btn.AddThemeStyleboxOverride("pressed", Card(new Color("2c3a22"), 10));
            btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            btn.Pressed += () => SelectSeed(index);
            _shopList.AddChild(btn);

            var info = new RichTextLabel
            {
                BbcodeEnabled = true,
                FitContent = true,
                ScrollActive = false,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            info.AddThemeFontSizeOverride("normal_font_size", 14);
            info.SetAnchorsPreset(LayoutPreset.FullRect);
            info.OffsetLeft = 12; info.OffsetTop = 6;
            info.OffsetRight = -10; info.OffsetBottom = -6;
            btn.AddChild(info);

            _shopRows.Add(new ShopRow { Seed = seed, Btn = btn, Box = sbNormal, Info = info });
        }
    }

    // ---- world switching --------------------------------------------------

    private void OnTabPressed(int idx)
    {
        if (idx >= _unlocked)
        {
            long need = GameData.UnlockThreshold(idx);
            World w = GameData.Worlds[idx];
            ShowToast($"Reach {Num.Fmt(need)} 🪙 to unlock {w.Name} {w.Icon}", new Color("ffcc6a"));
            return;
        }
        if (idx == _world)
            return;

        _world = idx;
        ApplyWorld();
        World world = GameData.Worlds[idx];
        ShowToast($"Entered {world.Name} {world.Icon}", new Color("d9f7a6"));
    }

    /// <summary>Point the visible grid + shop at the current world and re-theme.</summary>
    private void ApplyWorld()
    {
        for (int i = 0; i < _plots.Length; i++)
        {
            _plots[i].State = _states[_world][i];
            _plots[i].Refresh();
        }

        _bg.Color = _world switch
        {
            1 => new Color("0f1230"), // Space — deep indigo
            2 => new Color("3b3160"), // Heaven — twilight violet
            _ => new Color("335a33"), // Garden — green
        };

        PopulateShop();
        UpdateTabs();
        _lastShopCoins = -1;
        _lastShopSelected = -1;
        _lastShopWorld = -1;
    }

    private void UpdateTabs()
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            World w = GameData.Worlds[i];
            Button tab = _tabs[i];
            bool unlocked = i < _unlocked;
            bool active = i == _world;

            tab.Text = unlocked
                ? $"{w.Icon} {w.Name}"
                : $"{w.Icon} {w.Name} 🔒 {Num.Fmt(GameData.UnlockThreshold(i))}";

            Color bg = !unlocked ? new Color("2a2a2a")
                : active ? new Color("4f7a3a")
                : new Color("36462b");
            var box = Card(bg, 10);
            if (active)
            {
                box.BorderColor = new Color("ffe066");
                box.BorderWidthTop = box.BorderWidthBottom =
                    box.BorderWidthLeft = box.BorderWidthRight = 2;
            }
            tab.AddThemeStyleboxOverride("normal", box);
            tab.AddThemeStyleboxOverride("hover", Card(bg.Lightened(0.08f), 10));
            tab.AddThemeStyleboxOverride("pressed", Card(bg.Darkened(0.1f), 10));
            tab.AddThemeColorOverride("font_color",
                unlocked ? new Color("f4ffe9") : new Color(1, 1, 1, 0.55f));
        }
    }

    // ---- auto-play --------------------------------------------------------

    private void ToggleAuto()
    {
        _auto = !_auto;
        _autoBtn.Text = _auto ? "🤖 Auto: ON" : "🤖 Auto: OFF";
        ShowToast(_auto ? "Auto-play on — sit back!" : "Auto-play off — your turn 🌱",
            new Color("d9f7a6"));
    }

    private void AutoStep()
    {
        double earned = 0;
        int harvested = 0;
        foreach (Plot p in _plots)
        {
            if (p.State.IsReady)
            {
                earned += p.State.Payout;
                harvested++;
                p.State.Clear();
                p.Refresh();
            }
        }
        if (earned > 0)
            _coins += earned;

        int empties = 0;
        foreach (Plot p in _plots)
            if (p.State.IsEmpty) empties++;

        if (empties > 0)
        {
            List<SeedType> seeds = CurrentSeeds;
            double perPlot = _coins / empties;
            SeedType chosen = seeds[0];
            int chosenIdx = 0;
            for (int i = 0; i < seeds.Count; i++)
            {
                if (seeds[i].Cost <= perPlot)
                {
                    chosen = seeds[i];
                    chosenIdx = i;
                }
            }

            foreach (Plot p in _plots)
            {
                if (!p.State.IsEmpty)
                    continue;
                if (_coins < chosen.Cost)
                    break;
                _coins -= chosen.Cost;
                Selected = chosenIdx;
                p.State.Plant(chosen);
                p.Refresh();
            }
        }

        if (harvested > 0)
            ShowToast($"🤖 harvested {harvested}  +{Num.Fmt(earned)} 🪙", new Color("9be67a"));
    }

    // ---- Grow All math quiz ----------------------------------------------

    private void OnGrowAllPressed()
    {
        if (_quizOpen)
            return;

        bool anyGrowing = false;
        foreach (Plot p in _plots)
        {
            if (!p.State.IsEmpty && !p.State.IsReady) { anyGrowing = true; break; }
        }
        if (!anyGrowing)
        {
            ShowToast("Plant some seeds first! 🌱", new Color(1, 1, 1, 0.85f));
            return;
        }

        var quiz = new MathQuiz
        {
            OnComplete = () => { _quizOpen = false; GrowAllNow(); },
            OnCancel = () => { _quizOpen = false; },
        };
        AddChild(quiz);
        _quizOpen = true;
    }

    private void GrowAllNow()
    {
        int grew = 0;
        foreach (Plot p in _plots)
        {
            PlotState st = p.State;
            if (!st.IsEmpty && !st.IsReady)
            {
                st.Growth = st.Seed!.GrowSeconds;
                if (!st.MutationRolled)
                {
                    st.Mutation = Mutation.Roll();
                    st.MutationRolled = true;
                }
                grew++;
            }
            p.Refresh();
        }

        ShowToast(grew > 0 ? $"🌱 Grew {grew} crop{(grew == 1 ? "" : "s")}! Harvest away!"
                           : "All grown!", new Color("9be67a"));
    }

    // ---- plot / shop interaction -----------------------------------------

    private void OnPlotPressed(Plot plot)
    {
        PlotState st = plot.State;

        if (st.IsReady)
        {
            double payout = st.Payout;
            string mut = st.Mutation.IsNormal ? "" : st.Mutation.Name + " ";
            _coins += payout;
            ShowToast($"{mut}{st.Seed!.Name}  +{Num.Fmt(payout)} 🪙", new Color("9be67a"));
            st.Clear();
        }
        else if (st.IsEmpty)
        {
            SeedType seed = CurrentSeeds[Selected];
            if (_coins >= seed.Cost)
            {
                _coins -= seed.Cost;
                st.Plant(seed);
                ShowToast($"Planted {seed.Name}  -{Num.Fmt(seed.Cost)} 🪙", new Color("d9f7a6"));
            }
            else
            {
                ShowToast($"Need {Num.Fmt(seed.Cost)} 🪙 for {seed.Name}", new Color("ff8a7a"));
            }
        }
        else
        {
            ShowToast("Still growing…", new Color(1, 1, 1, 0.85f));
        }

        plot.Refresh();
    }

    private void SelectSeed(int index)
    {
        Selected = index;
        ShowToast($"Selected {CurrentSeeds[index].Name}", new Color(1, 1, 1, 0.85f));
    }

    private void HarvestAll()
    {
        double total = 0;
        int count = 0;
        foreach (Plot p in _plots)
        {
            if (p.State.IsReady)
            {
                total += p.State.Payout;
                count++;
                p.State.Clear();
                p.Refresh();
            }
        }

        if (count == 0)
            ShowToast("Nothing ready to harvest yet", new Color(1, 1, 1, 0.8f));
        else
        {
            _coins += total;
            ShowToast($"Harvested {count}  +{Num.Fmt(total)} 🪙", new Color("9be67a"));
        }
    }

    private void UpdateShop()
    {
        foreach (ShopRow row in _shopRows)
        {
            bool selected = CurrentSeeds[Selected] == row.Seed;
            bool afford = _coins >= row.Seed.Cost;

            row.Box.BorderColor = selected ? new Color("ffe066") : new Color(0, 0, 0, 0.25f);
            int bw = selected ? 3 : 1;
            row.Box.BorderWidthTop = row.Box.BorderWidthBottom =
                row.Box.BorderWidthLeft = row.Box.BorderWidthRight = bw;
            row.Btn.Modulate = afford ? Colors.White : new Color(1, 1, 1, 0.5f);

            string rc = row.Seed.RarityColor.ToHtml(false);
            string costCol = afford ? "ffe066" : "ff8a7a";
            row.Info.Text =
                $"[b]{row.Seed.Name}[/b]  [color=#{rc}]{row.Seed.Rarity}[/color]\n" +
                $"[color=#{costCol}]{Num.Fmt(row.Seed.Cost)}🪙[/color]  " +
                $"sells {Num.Fmt(row.Seed.BaseValue)}  ·  {FormatGrow(row.Seed.GrowSeconds)}";
        }
    }

    private void ShowToast(string text, Color color)
    {
        _toast.Text = text;
        _toast.AddThemeColorOverride("font_color", color);
        _toast.SelfModulate = new Color(1, 1, 1, 1);
        _toastTime = 2.4f;
    }

    // ---- persistence ------------------------------------------------------

    private void SaveGame()
    {
        var worlds = new Godot.Collections.Array();
        foreach (PlotState[] world in _states)
        {
            var plots = new Godot.Collections.Array();
            foreach (PlotState s in world)
            {
                plots.Add(new Godot.Collections.Dictionary
                {
                    ["seed"] = s.Seed?.Name ?? "",
                    ["growth"] = s.Growth,
                    ["mutation"] = s.Mutation.Name,
                    ["rolled"] = s.MutationRolled,
                });
            }
            worlds.Add(new Godot.Collections.Dictionary { ["plots"] = plots });
        }

        var selected = new Godot.Collections.Array();
        foreach (int s in _selectedByWorld)
            selected.Add(s);

        var data = new Godot.Collections.Dictionary
        {
            ["coins"] = _coins.ToString("F0", System.Globalization.CultureInfo.InvariantCulture),
            ["world"] = _world,
            ["unlocked"] = _unlocked,
            ["selected"] = selected,
            ["worlds"] = worlds,
            ["time"] = Time.GetUnixTimeFromSystem(),
        };

        using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        f?.StoreString(Json.Stringify(data));
    }

    private void LoadGame()
    {
        if (!FileAccess.FileExists(SavePath))
            return;

        using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (f is null)
            return;

        Variant parsed = Json.ParseString(f.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
            return;

        var data = parsed.AsGodotDictionary();

        Variant cv = data["coins"];
        _coins = cv.VariantType == Variant.Type.String
            ? double.Parse(cv.AsString(), System.Globalization.CultureInfo.InvariantCulture)
            : cv.AsDouble();

        _unlocked = data.ContainsKey("unlocked")
            ? Mathf.Clamp((int)data["unlocked"], 1, GameData.Worlds.Count) : 1;
        _world = data.ContainsKey("world")
            ? Mathf.Clamp((int)data["world"], 0, _unlocked - 1) : 0;

        if (data.ContainsKey("selected"))
        {
            var sel = data["selected"].AsGodotArray();
            for (int i = 0; i < _selectedByWorld.Length && i < sel.Count; i++)
                _selectedByWorld[i] = Mathf.Clamp((int)sel[i], 0, GameData.Worlds[i].Seeds.Count - 1);
        }

        double savedTime = data.ContainsKey("time")
            ? (double)data["time"] : Time.GetUnixTimeFromSystem();
        double elapsed = Mathf.Clamp(
            Time.GetUnixTimeFromSystem() - savedTime, 0, GameData.MaxOfflineSeconds);

        if (!data.ContainsKey("worlds"))
            return;

        var worlds = data["worlds"].AsGodotArray();
        for (int w = 0; w < _states.Length && w < worlds.Count; w++)
        {
            World worldDef = GameData.Worlds[w];
            var plots = worlds[w].AsGodotDictionary()["plots"].AsGodotArray();
            for (int i = 0; i < _states[w].Length && i < plots.Count; i++)
            {
                var pd = plots[i].AsGodotDictionary();
                PlotState st = _states[w][i];
                string name = (string)pd["seed"];
                if (string.IsNullOrEmpty(name)) { st.Clear(); continue; }

                SeedType? seed = worldDef.SeedByName(name);
                if (seed is null) { st.Clear(); continue; }

                st.Seed = seed;
                st.Growth = (float)(double)pd["growth"];
                st.Mutation = Mutation.ByName((string)pd["mutation"]);
                st.MutationRolled = (bool)pd["rolled"];

                Grow(st, (float)elapsed);
                if (st.IsReady && !st.MutationRolled)
                {
                    st.Mutation = Mutation.Roll();
                    st.MutationRolled = true;
                }
            }
        }
    }

    // ---- helpers ----------------------------------------------------------

    private static StyleBoxFlat Card(Color bg, int radius) => new()
    {
        BgColor = bg,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = 6, ContentMarginRight = 6,
        ContentMarginTop = 6, ContentMarginBottom = 6,
    };

    private static string FormatGrow(float seconds)
    {
        if (seconds < 60) return $"{Mathf.RoundToInt(seconds)}s";
        int m = Mathf.RoundToInt(seconds / 60f);
        return $"~{m}m";
    }
}
