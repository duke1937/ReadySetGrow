using Godot;
using System.Collections.Generic;

namespace GrowDaGarden;

/// <summary>
/// Root of the 3D Growden game. Builds the whole scene in code — two open dirt
/// fields you plant on, a fenced farm with a swinging gate, a market stall just
/// outside, and a first-person player you walk around with. Runs the
/// grow / harvest / sell economy and persists progress (incl. offline growth).
/// </summary>
public partial class Game3D : Node3D
{
    private const string SavePath = "user://readysetgrow_save.json";
    private const double AutosaveSeconds = 5.0;

    // First-person tuning.
    private const float WalkSpeed = 5.0f;
    private const float JumpSpeed = 6.5f;   // tall enough to hop up the vine's leaves
    private const float Gravity = 22f;
    private const float MouseSensitivity = 0.0024f;
    private const float ReachDistance = 5.0f;

    // Farm layout.
    private const float FenceX = 9.5f;          // half-extent of the fenced farm
    private const float FenceZ = 9.5f;
    private const float GateZ = 9.5f;           // front fence line (where the gate is)
    private const float GateHalfWidth = 1.6f;   // gate opening spans x in [-1.6, 1.6]
    private const float SpotSpacing = 1.6f;

    private double _coins = Catalog.StartingCoins;
    private int _selected;                       // index into Catalog.Seeds

    // Harvest basket — fills when you harvest, emptied for coins at the market.
    private int _basketCount;
    private double _basketValue;

    private readonly PlotState[] _states = new PlotState[Catalog.PlotCount];
    private Plot3D[] _plots = System.Array.Empty<Plot3D>();

    // --- first-person player ---
    private CharacterBody3D _player = null!;
    private Camera3D _camera = null!;
    private RayCast3D _ray = null!;
    private float _camPitch;
    private bool _walkMode = true;

    // --- gate ---
    private Node3D _gatePivot = null!;
    private bool _gateOpen;
    private float _gateTargetAngle;

    // --- magical tree (premium seed catalog, unlocks at 100B) ---
    private bool _treeUnlocked;
    private readonly List<StandardMaterial3D> _treeGlow = new();
    private List<SeedType> _activeSeeds = new(Catalog.Seeds);

    // --- hidden grove (unlocks at 1 Qi) ---
    private bool _groveUnlocked;
    private readonly List<StandardMaterial3D> _groveGlow = new();

    // --- uni-grape (unlocks by climbing the leaf vine) ---
    private const double VineBarrierCost = 5e24;   // 5 Sp to drop the barrier and climb
    private bool _uniUnlocked;
    private bool _vineBarrierDown;
    private readonly List<StandardMaterial3D> _uniGlow = new();
    private Node3D? _vineBarrier;
    private Vector3 _vineTop;
    private Vector3 _vineBase;

    // --- pets (shop unlocks at 100 Qa) ---
    private bool _petsUnlocked;
    private readonly HashSet<string> _ownedPets = new();
    private double _yieldMult = 1.0;   // sell-value multiplier from owned pets
    private double _growthMult = 1.0;  // growth-speed multiplier from owned pets
    private VBoxContainer _petsList = null!;
    private PanelContainer _petsPanel = null!;
    private readonly List<PetRow> _petRows = new();
    private readonly List<StandardMaterial3D> _petsGlow = new();

    // --- weather events ---
    private string _activeEvent = "";   // "", "Storm", "Rainbow"
    private double _nextEventIn = 120;   // first event after ~2 minutes
    private double _eventRemaining;

    // --- audio ---
    private Sfx _sfx = null!;

    // --- weather visuals ---
    private DirectionalLight3D _sun = null!;
    private ProceduralSkyMaterial _skyMat = null!;
    private Godot.Environment _env = null!;
    private Node3D _stormFx = null!;
    private Node3D _rainbowFx = null!;
    private CpuParticles3D _rainFx = null!;
    private ColorRect _flashRect = null!;
    private float _sunBaseEnergy = 1.15f;
    private float _flash;
    private double _lightningIn;

    // --- current look target ---
    private string _targetKind = "";            // "plot" / "gate" / "market" / ""
    private Plot3D? _targeted;

    // --- UI ---
    private Label _coinsLabel = null!;
    private Label _basketLabel = null!;
    private Label _toast = null!;
    private float _toastTime;
    private VBoxContainer _shopList = null!;
    private Label _shopHeader = null!;
    private readonly List<ShopRow> _shopRows = new();
    private Button _autoBtn = null!;
    private Control _uiRoot = null!;
    private Label _crosshair = null!;
    private Label _prompt = null!;
    private Label _hint = null!;
    private Label _eventBanner = null!;
    private ColorRect _eventTint = null!;
    private Control _menuRoot = null!;
    private bool _menuOpen;
    private Button _restartBtn = null!;
    private double _restartArmed;

    private double _lastShopCoins = -1;
    private int _lastShopSelected = -1;
    private double _saveTimer;

    private bool _auto;
    private double _autoTimer;
    private bool _quizOpen;

    private sealed class ShopRow
    {
        public required SeedType Seed;
        public required Button Btn;
        public required StyleBoxFlat Box;
        public required RichTextLabel Info;
    }

    private sealed class PetRow
    {
        public required Catalog.Pet Pet;
        public required Button Btn;
        public required RichTextLabel Info;
    }

    private List<SeedType> Seeds => _activeSeeds;

    // The shop always lists every catalog; locked tiers are shown greyed out.
    private void RebuildSeedList()
    {
        _activeSeeds = new List<SeedType>(Catalog.Seeds);
        _activeSeeds.AddRange(Catalog.TreeSeeds);
        _activeSeeds.AddRange(Catalog.GroveSeeds);
        _activeSeeds.AddRange(Catalog.UniSeeds);
    }

    private static int BaseCount => Catalog.Seeds.Count;
    private static int TreeEnd => Catalog.Seeds.Count + Catalog.TreeSeeds.Count;
    private static int GroveEnd => TreeEnd + Catalog.GroveSeeds.Count;

    private bool IndexUnlocked(int i)
    {
        if (i < BaseCount) return true;
        if (i < TreeEnd) return _treeUnlocked;
        if (i < GroveEnd) return _groveUnlocked;
        return _uniUnlocked;
    }

    // -1 means "not a coin unlock" (the Uni-Grape is reached by climbing).
    private static double UnlockCostForIndex(int i) =>
        i < BaseCount ? 0 : i < TreeEnd ? Catalog.TreeUnlockCost : i < GroveEnd ? Catalog.GroveUnlockCost : -1;

    private static string LockMessage(int i)
    {
        double c = UnlockCostForIndex(i);
        return c < 0 ? "climb the Uni-Grape vine 🍇" : $"reach {Num.Fmt(c)} 🪙";
    }

    // ---- multi-tile (footprint) helpers -----------------------------------

    private static (int field, int col, int row) PlotCoords(int idx)
    {
        int field = idx / Catalog.PlotsPerField;
        int local = idx % Catalog.PlotsPerField;
        return (field, local % Catalog.FieldCols, local / Catalog.FieldCols);
    }

    private static int PlotIndex(int field, int col, int row) =>
        field * Catalog.PlotsPerField + row * Catalog.FieldCols + col;

    /// <summary>The 4 spot indices of a 2x2 block containing the clicked spot (master first).</summary>
    private static int[] MultiBlock(int idx)
    {
        var (field, col, row) = PlotCoords(idx);
        int mcol = Mathf.Min(col, Catalog.FieldCols - 2);
        int mrow = Mathf.Min(row, Catalog.FieldRows - 2);
        return new[]
        {
            PlotIndex(field, mcol, mrow),
            PlotIndex(field, mcol + 1, mrow),
            PlotIndex(field, mcol, mrow + 1),
            PlotIndex(field, mcol + 1, mrow + 1),
        };
    }

    private void SetupMulti(int masterIdx)
    {
        _plots[masterIdx].PlantOffset = new Vector3(SpotSpacing * 0.5f, 0, SpotSpacing * 0.5f);
        _plots[masterIdx].ExtraScale = 1.9f;
    }

    /// <summary>Clear a plant and any multi-tile slaves it covers.</summary>
    private void ClearPlot(int masterIdx)
    {
        for (int i = 0; i < _states.Length; i++)
        {
            if (_states[i].SlaveOf == masterIdx)
            {
                _states[i].Clear();
                _plots[i].Refresh(0f);
            }
        }
        _states[masterIdx].Clear();
        _plots[masterIdx].PlantOffset = Vector3.Zero;
        _plots[masterIdx].ExtraScale = 1f;
        _plots[masterIdx].Refresh(0f);
    }

    public override void _Ready()
    {
        for (int i = 0; i < _states.Length; i++)
            _states[i] = new PlotState();

        _sfx = new Sfx();
        AddChild(_sfx);

        Catalog.Warmup();

        BuildWorld();
        BuildUi();
        LoadGame();
        RebuildSeedList();
        _selected = Mathf.Clamp(_selected, 0, _activeSeeds.Count - 1);
        RecomputePetBonuses();
        SetTreeGlow(_treeUnlocked);
        SetGroveGlow(_groveUnlocked);
        SetUniGlow(_uniUnlocked);
        if (_vineBarrierDown) DropBarrier();
        SetPetsGlow(_petsUnlocked);
        _petsPanel.Visible = _petsUnlocked;
        if (_petsUnlocked) PopulatePets();
        PopulateShop();
        SetWalkMode(true);
        ShowToast("🌱 Welcome to ReadySetGrow! Open the gate, walk in, and press E on the dirt to plant.", new Color("d9f7a6"));
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;

        // The Main Menu pauses the simulation.
        if (!_menuOpen)
        {
            float growDt = dt * (float)_growthMult;
            foreach (PlotState st in _states)
                Grow(st, growDt, _activeEvent);

            AnimateGate(dt);
            UpdateEvents(delta);
            UpdateWeather(dt, delta);
            if (!_treeUnlocked && _coins >= Catalog.TreeUnlockCost)
                UnlockTree();
            if (!_petsUnlocked && _coins >= Catalog.PetsUnlockCost)
                UnlockPets();
            if (!_groveUnlocked && _coins >= Catalog.GroveUnlockCost)
                UnlockGrove();
            if (!_vineBarrierDown && _coins >= VineBarrierCost)
            {
                DropBarrier();
                _sfx.Play("unlock", -4f);
                ShowToast("🔮 The vine barrier drops — climb up to the Uni-Grape!", new Color("9fd8ff"));
            }
            if (!_uniUnlocked && _player.GlobalPosition.DistanceTo(_vineTop) < 4f)
                UnlockUni();
            UpdateTarget();

            if (_auto && !_quizOpen)
            {
                _autoTimer += delta;
                if (_autoTimer >= 0.4)
                {
                    _autoTimer = 0;
                    AutoStep();
                }
            }
        }

        foreach (Plot3D p in _plots)
            p.Refresh(dt);

        if (_restartArmed > 0)
        {
            _restartArmed -= delta;
            if (_restartArmed <= 0)
                _restartBtn.Text = "🔄 Restart Game";
        }

        _coinsLabel.Text = $"🪙 {Num.Fmt(_coins)}";
        _basketLabel.Text = _basketCount > 0
            ? $"🧺 {_basketCount}  ({Num.Fmt(_basketValue)})"
            : "🧺 empty";
        if (_coins != _lastShopCoins || _selected != _lastShopSelected)
        {
            UpdateShop();
            if (_petsUnlocked) UpdatePets();
            _lastShopCoins = _coins;
            _lastShopSelected = _selected;
        }

        if (_toastTime > 0f)
        {
            _toastTime -= dt;
            _toast.SelfModulate = new Color(1, 1, 1, Mathf.Clamp(_toastTime, 0f, 1f));
        }

        _saveTimer += delta;
        if (_saveTimer >= AutosaveSeconds)
        {
            _saveTimer = 0;
            SaveGame();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        Vector3 v = _player.Velocity;

        if (!_player.IsOnFloor())
            v.Y -= Gravity * dt;
        else if (v.Y < 0)
            v.Y = 0;

        Vector3 wish = Vector3.Zero;
        if (_walkMode && !_quizOpen && !_menuOpen)
        {
            float fwd = (Input.IsPhysicalKeyPressed(Key.W) ? 1f : 0f) - (Input.IsPhysicalKeyPressed(Key.S) ? 1f : 0f);
            float strafe = (Input.IsPhysicalKeyPressed(Key.D) ? 1f : 0f) - (Input.IsPhysicalKeyPressed(Key.A) ? 1f : 0f);

            Basis b = _player.GlobalTransform.Basis;
            Vector3 forward = -b.Z; forward.Y = 0;
            Vector3 right = b.X; right.Y = 0;
            wish = forward.Normalized() * fwd + right.Normalized() * strafe;
            if (wish.LengthSquared() > 1f) wish = wish.Normalized();
            wish *= WalkSpeed;

            if (_player.IsOnFloor() && Input.IsPhysicalKeyPressed(Key.Space))
                v.Y = JumpSpeed;
        }

        v.X = wish.X;
        v.Z = wish.Z;
        _player.Velocity = v;
        _player.MoveAndSlide();
    }

    public override void _ExitTree() => SaveGame();

    // ---- input ------------------------------------------------------------

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mm && _walkMode && !_quizOpen && !_menuOpen)
        {
            _player.RotateY(-mm.Relative.X * MouseSensitivity);
            _camPitch = Mathf.Clamp(_camPitch - mm.Relative.Y * MouseSensitivity, -1.4f, 1.4f);
            _camera.Rotation = new Vector3(_camPitch, 0, 0);
            return;
        }

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (!_walkMode || _quizOpen || _menuOpen)
                return;
            switch (mb.ButtonIndex)
            {
                case MouseButton.Left: Interact(); break;
                case MouseButton.WheelUp: CycleSeed(-1); break;
                case MouseButton.WheelDown: CycleSeed(+1); break;
            }
            return;
        }

        // Hold Left-Shift to free the cursor for the shop; release to walk again.
        if (@event is InputEventKey sk && !sk.Echo
            && (sk.Keycode == Key.Shift || sk.PhysicalKeycode == Key.Shift))
        {
            if (!_quizOpen && !_menuOpen) SetWalkMode(!sk.Pressed);
            return;
        }

        if (@event is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode == Key.F11) { ToggleFullscreen(); return; }
            if (k.Keycode == Key.Escape) { if (!_quizOpen) ToggleMenu(); return; }
            if (_menuOpen || _quizOpen) return;

            if (k.Keycode == Key.Tab) { SetWalkMode(!_walkMode); return; }
            if (!_walkMode) return;
            switch (k.Keycode)
            {
                case Key.E: Interact(); break;
                case Key.H: HarvestAll(); break;
                case Key.G: OnGrowAllPressed(); break;
                case Key.T: ToggleAuto(); break;
            }
        }
    }

    private void SetWalkMode(bool walk)
    {
        _walkMode = walk;
        Input.MouseMode = walk ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        _crosshair.Visible = walk;
        if (!walk) _prompt.Text = "";
        _hint.Text = walk
            ? "WASD move · Mouse look · [E]/Click use · Wheel pick seed · [H] harvest all · Hold [Shift] for shop · [Esc] menu"
            : "Cursor free — click/scroll the shop to pick a seed · release [Shift] (or [Tab]) to walk again · [Esc] menu";
    }

    private static void ToggleFullscreen()
    {
        DisplayServer.WindowMode mode = DisplayServer.WindowGetMode();
        bool full = mode == DisplayServer.WindowMode.Fullscreen
                 || mode == DisplayServer.WindowMode.ExclusiveFullscreen;
        // WindowMode.Fullscreen is Godot's borderless ("windowless") fullscreen.
        DisplayServer.WindowSetMode(full ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
    }

    private void CycleSeed(int dir)
    {
        int n = Seeds.Count;
        for (int s = 0; s < n; s++)
        {
            _selected = (_selected + dir + n) % n;
            if (IndexUnlocked(_selected)) break;
        }
        _sfx.Play("select");
        ShowToast($"Selected {Seeds[_selected].Name}  ({Seeds[_selected].Rarity})", new Color(1, 1, 1, 0.9f));
    }

    // ---- look-target & interaction ---------------------------------------

    private void UpdateTarget()
    {
        _targetKind = "";
        _targeted = null;

        if (_walkMode && !_quizOpen)
        {
            _ray.ForceRaycastUpdate();
            if (_ray.IsColliding() && _ray.GetCollider() is Node c && c.HasMeta("kind"))
            {
                _targetKind = (string)c.GetMeta("kind");
                if (_targetKind == "plot" && c.HasMeta("plot_index"))
                {
                    int idx = (int)c.GetMeta("plot_index");
                    if (idx >= 0 && idx < _plots.Length)
                        _targeted = _plots[idx];
                }
            }
        }

        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        switch (_targetKind)
        {
            case "gate":
                SetPrompt(_gateOpen ? "[E] Close gate" : "[E] Open gate", new Color("eaffd8"));
                return;
            case "market":
                if (_basketCount > 0)
                    SetPrompt($"[E] Sell harvest — {_basketCount} crops for {Num.Fmt(_basketValue)} 🪙", new Color("9be67a"));
                else
                    SetPrompt("Market — your basket is empty. Harvest crops, then come back to sell.", new Color(1, 1, 1, 0.85f));
                return;
            case "tree":
                if (_treeUnlocked)
                    SetPrompt("🌳 Magical Tree — its god-tier seeds are in the shop (hold Shift)", new Color("d8b8ff"));
                else
                    SetPrompt($"🌳 Magical Tree — locked. Reach {Num.Fmt(Catalog.TreeUnlockCost)} 🪙 to unlock (you have {Num.Fmt(_coins)})", new Color("d8b8ff"));
                return;
            case "grove":
                if (_groveUnlocked)
                    SetPrompt("🌌 Hidden Grove — its seeds are in the shop (hold Shift)", new Color("aef0e0"));
                else
                    SetPrompt($"🌌 Hidden Grove — locked. Reach {Num.Fmt(Catalog.GroveUnlockCost)} 🪙 (you have {Num.Fmt(_coins)})", new Color("aef0e0"));
                return;
            case "pets":
                if (_petsUnlocked)
                    SetPrompt("🐾 Pets Shop — adopt pets on the left panel (hold Shift)", new Color("ffd9a8"));
                else
                    SetPrompt($"🐾 Pets Shop — locked. Reach {Num.Fmt(Catalog.PetsUnlockCost)} 🪙 (you have {Num.Fmt(_coins)})", new Color("ffd9a8"));
                return;
            case "unigrape":
                SetPrompt(_uniUnlocked
                    ? "🍇 Uni-Grape — its seeds are in the shop (hold Shift)"
                    : "🍇 The Uni-Grape — stand up here to unlock its seeds!", new Color("d8b8ff"));
                return;
            case "barrier":
                SetPrompt($"🔮 Vine barrier — reach {Num.Fmt(VineBarrierCost)} 🪙 to enter (you have {Num.Fmt(_coins)})", new Color("9fd8ff"));
                return;
            case "plot" when _targeted is not null:
            {
                PlotState st = _targeted.State;
                if (st.IsReady)
                {
                    string mut = st.Mutation.IsNormal ? "" : st.Mutation.Name + " ";
                    SetPrompt($"[E] Harvest {mut}{Plot3D.SizeWord(st.Size)}{st.Seed!.Name}   +{Num.Fmt(st.Payout)} 🪙", new Color("9be67a"));
                }
                else if (st.IsEmpty)
                {
                    SeedType seed = Seeds[_selected];
                    bool afford = _coins >= seed.Cost;
                    SetPrompt($"[E] Plant {seed.Name}   -{Num.Fmt(seed.Cost)} 🪙", afford ? new Color("eaffd8") : new Color("ff8a7a"));
                }
                else
                {
                    float remain = st.Seed!.GrowSeconds - st.Growth;
                    SetPrompt($"{st.Seed.Name} — growing… {FormatTime(remain)}", new Color(1, 1, 1, 0.85f));
                }
                return;
            }
            default:
                _prompt.Text = "";
                return;
        }
    }

    private void SetPrompt(string text, Color color)
    {
        _prompt.Text = text;
        _prompt.AddThemeColorOverride("font_color", color);
    }

    private void Interact()
    {
        switch (_targetKind)
        {
            case "gate": ToggleGate(); break;
            case "market": SellBasket(); break;
            case "tree": UseTree(); break;
            case "grove": UseGrove(); break;
            case "pets": UsePetsShop(); break;
            case "unigrape": UseUni(); break;
            case "barrier": UseBarrier(); break;
            case "plot" when _targeted is not null: UsePlot(_targeted); break;
        }
    }

    private void UseTree()
    {
        if (_treeUnlocked)
        {
            ShowToast("🌳 The Magical Tree's seeds are in the shop — hold Shift to browse.", new Color("d8b8ff"));
        }
        else if (_coins >= Catalog.TreeUnlockCost)
        {
            UnlockTree();
        }
        else
        {
            ShowToast($"Locked — reach {Num.Fmt(Catalog.TreeUnlockCost)} 🪙 to unlock the Magical Tree.", new Color("ff8a7a"));
        }
    }

    private void UnlockTree()
    {
        if (_treeUnlocked) return;
        _treeUnlocked = true;
        RebuildSeedList();
        PopulateShop();
        SetTreeGlow(true);
        _sfx.Play("unlock", -3f);
        ShowToast("✨ MAGICAL TREE UNLOCKED! Divine · Ultra · Titan · Entity · Eternal · Admin seeds added!", new Color("e0b8ff"));
    }

    private void SetTreeGlow(bool on)
    {
        foreach (StandardMaterial3D m in _treeGlow)
        {
            m.EmissionEnabled = true;
            m.Emission = m.AlbedoColor;
            m.EmissionEnergyMultiplier = on ? 2.4f : 0.45f;
        }
    }

    // ---- hidden grove -----------------------------------------------------

    private void UseGrove()
    {
        if (_groveUnlocked)
            ShowToast("🌌 The Hidden Grove's seeds are in the shop — hold Shift to browse.", new Color("aef0e0"));
        else if (_coins >= Catalog.GroveUnlockCost)
            UnlockGrove();
        else
            ShowToast($"Locked — reach {Num.Fmt(Catalog.GroveUnlockCost)} 🪙 for the Hidden Grove.", new Color("ff8a7a"));
    }

    private void UnlockGrove()
    {
        if (_groveUnlocked) return;
        _groveUnlocked = true;
        RebuildSeedList();
        PopulateShop();
        SetGroveGlow(true);
        _sfx.Play("unlock", -3f);
        ShowToast("🌌 HIDDEN GROVE UNLOCKED! Hidden · Alpha · Strange · Celestial · Infinite seeds added!", new Color("aef0e0"));
    }

    private void SetGroveGlow(bool on)
    {
        foreach (StandardMaterial3D m in _groveGlow)
        {
            m.EmissionEnabled = true;
            m.Emission = m.AlbedoColor;
            m.EmissionEnergyMultiplier = on ? 2.2f : 0.4f;
        }
    }

    // ---- uni-grape (reached by climbing) ----------------------------------

    private void UseBarrier()
    {
        if (_coins >= VineBarrierCost)
        {
            DropBarrier();
            _sfx.Play("unlock", -4f);
            ShowToast("🔮 Barrier dropped — climb the vine to the Uni-Grape!", new Color("9fd8ff"));
        }
        else
        {
            _sfx.Play("error");
            ShowToast($"Locked — reach {Num.Fmt(VineBarrierCost)} 🪙 to enter the vine.", new Color("ff8a7a"));
        }
    }

    private void UseUni()
    {
        if (!_uniUnlocked)
            UnlockUni();
        else
            ShowToast("🍇 Uni-Grape seeds are in the shop — hold Shift to browse.", new Color("d8b8ff"));
    }

    private void UnlockUni()
    {
        if (_uniUnlocked) return;
        _uniUnlocked = true;
        RebuildSeedList();
        PopulateShop();
        SetUniGlow(true);
        _sfx.Play("unlock", -3f);
        ShowToast("🍇 UNI-GRAPE REACHED! Dimensional · Galaxy · Solar · Blackhole seeds unlocked!", new Color("d8b8ff"));
    }

    private void SetUniGlow(bool on)
    {
        foreach (StandardMaterial3D m in _uniGlow)
        {
            m.EmissionEnabled = true;
            m.Emission = m.AlbedoColor;
            m.EmissionEnergyMultiplier = on ? 2.4f : 0.6f;
        }
    }

    // ---- pets -------------------------------------------------------------

    private void UsePetsShop()
    {
        if (_petsUnlocked)
            ShowToast("🐾 Pets are on the left panel — hold Shift to adopt them.", new Color("ffd9a8"));
        else if (_coins >= Catalog.PetsUnlockCost)
            UnlockPets();
        else
            ShowToast($"Locked — reach {Num.Fmt(Catalog.PetsUnlockCost)} 🪙 for the Pets Shop.", new Color("ff8a7a"));
    }

    private void UnlockPets()
    {
        if (_petsUnlocked) return;
        _petsUnlocked = true;
        _petsPanel.Visible = true;
        PopulatePets();
        SetPetsGlow(true);
        _sfx.Play("unlock", -3f);
        ShowToast("🐾 PETS SHOP UNLOCKED! Adopt pets on the left to boost yield & growth.", new Color("ffd9a8"));
    }

    private void SetPetsGlow(bool on)
    {
        foreach (StandardMaterial3D m in _petsGlow)
        {
            m.EmissionEnabled = true;
            m.Emission = m.AlbedoColor;
            m.EmissionEnergyMultiplier = on ? 2.0f : 0.4f;
        }
    }

    private void RecomputePetBonuses()
    {
        double yield = 0, speed = 0;
        foreach (string name in _ownedPets)
        {
            Catalog.Pet? p = Catalog.PetByName(name);
            if (p is null) continue;
            if (p.Kind == "yield") yield += p.Percent;
            else speed += p.Percent;
        }
        _yieldMult = 1.0 + yield;
        _growthMult = 1.0 + speed;
    }

    private void BuyPet(Catalog.Pet pet)
    {
        if (_ownedPets.Contains(pet.Name))
            return;
        if (_coins < pet.Cost)
        {
            _sfx.Play("error");
            ShowToast($"Need {Num.Fmt(pet.Cost)} 🪙 to adopt {pet.Name}", new Color("ff8a7a"));
            return;
        }
        _coins -= pet.Cost;
        _ownedPets.Add(pet.Name);
        RecomputePetBonuses();
        _sfx.Play("unlock", -6f);
        string eff = pet.Kind == "yield" ? $"+{pet.Percent * 100:0}% sell value" : $"+{pet.Percent * 100:0}% growth speed";
        ShowToast($"🐾 Adopted {pet.Name}! {eff}", new Color("ffd9a8"));
    }

    private void UsePlot(Plot3D plot)
    {
        // A spot covered by a multi-tile plant acts on its master.
        if (plot.State.SlaveOf >= 0)
            plot = _plots[plot.State.SlaveOf];
        PlotState st = plot.State;

        if (st.IsReady)
        {
            double payout = st.Payout * _yieldMult;
            AddToBasket(payout);
            string mut = st.Mutation.IsNormal ? "" : st.Mutation.Name + " ";
            _sfx.Play(st.Mutation.IsNormal ? "harvest" : "harvestMut");
            ShowToast($"Harvested {mut}{st.Seed!.Name} → basket  (+{Num.Fmt(payout)})", new Color("9be67a"));
            ClearPlot(plot.Index);
            return;
        }

        if (st.IsEmpty)
        {
            SeedType seed = Seeds[_selected];
            if (!IndexUnlocked(_selected))
            {
                _sfx.Play("error");
                ShowToast($"🔒 {seed.Name} is locked — {LockMessage(_selected)}", new Color("ff8a7a"));
                return;
            }
            if (seed.Footprint >= 4)
            {
                PlantMulti(plot.Index, seed);
                return;
            }
            if (_coins >= seed.Cost)
            {
                _coins -= seed.Cost;
                st.Plant(seed, RollSize());
                _sfx.Play("plant");
                ShowToast($"Planted {seed.Name}  -{Num.Fmt(seed.Cost)} 🪙", new Color("d9f7a6"));
            }
            else
            {
                _sfx.Play("error");
                ShowToast($"Need {Num.Fmt(seed.Cost)} 🪙 for {seed.Name} — sell at the market!", new Color("ff8a7a"));
            }
            plot.Refresh(0f);
            return;
        }

        ShowToast("Still growing…", new Color(1, 1, 1, 0.85f));
    }

    private void PlantMulti(int clickedIdx, SeedType seed)
    {
        int[] block = MultiBlock(clickedIdx);
        foreach (int bi in block)
        {
            if (!_states[bi].IsEmpty || _states[bi].SlaveOf >= 0)
            {
                _sfx.Play("error");
                ShowToast($"{seed.Name} needs a clear 2×2 patch of dirt", new Color("ff8a7a"));
                return;
            }
        }
        if (_coins < seed.Cost)
        {
            _sfx.Play("error");
            ShowToast($"Need {Num.Fmt(seed.Cost)} 🪙 for {seed.Name} — sell at the market!", new Color("ff8a7a"));
            return;
        }

        _coins -= seed.Cost;
        int master = block[0];
        _states[master].Plant(seed, RollSize());
        for (int k = 1; k < block.Length; k++)
            _states[block[k]].SlaveOf = master;
        SetupMulti(master);
        foreach (int bi in block)
            _plots[bi].Refresh(0f);
        _sfx.Play("plant");
        ShowToast($"Planted {seed.Name} (4 spaces)  -{Num.Fmt(seed.Cost)} 🪙", new Color("d9f7a6"));
    }

    private void AddToBasket(double payout)
    {
        _basketValue += payout;
        _basketCount++;
    }

    private void SellBasket()
    {
        if (_basketCount == 0)
        {
            _sfx.Play("error");
            ShowToast("Basket empty — go harvest some crops first 🌱", new Color(1, 1, 1, 0.85f));
            return;
        }
        double got = _basketValue;
        int n = _basketCount;
        _coins += got;
        _basketValue = 0;
        _basketCount = 0;
        _sfx.Play("sell");
        ShowToast($"💰 Sold {n} crop{(n == 1 ? "" : "s")} for {Num.Fmt(got)} 🪙", new Color("ffe066"));
    }

    private void ToggleGate()
    {
        _gateOpen = !_gateOpen;
        _gateTargetAngle = _gateOpen ? -Mathf.Pi / 2f : 0f;
        _sfx.Play(_gateOpen ? "gateOpen" : "gateClose");
        ShowToast(_gateOpen ? "Gate opened" : "Gate closed", new Color("d9f7a6"));
    }

    private void AnimateGate(float dt)
    {
        Vector3 r = _gatePivot.Rotation;
        r.Y = Mathf.LerpAngle(r.Y, _gateTargetAngle, Mathf.Clamp(dt * 6f, 0f, 1f));
        _gatePivot.Rotation = r;
    }

    // ---- weather events ---------------------------------------------------

    private const float EventDuration = 30f;

    private void UpdateEvents(double delta)
    {
        if (_activeEvent.Length == 0)
        {
            _nextEventIn -= delta;
            if (_nextEventIn <= 0)
                StartEvent();
        }
        else
        {
            _eventRemaining -= delta;
            if (_eventRemaining <= 0)
                EndEvent();
        }

        if (_activeEvent.Length == 0)
        {
            _eventBanner.Visible = false;
            return;
        }

        _eventBanner.Visible = true;
        int s = Mathf.Max(0, Mathf.CeilToInt((float)_eventRemaining));
        (string text, string col) = _activeEvent switch
        {
            "Storm"   => ($"⛈  STORM  —  crops can turn SHOCKED (48×)!   {s}s", "bcd0ff"),
            "Rainbow" => ($"🌈  RAINBOW  —  crops often turn Rainbow (25×)!   {s}s", "ffc4f0"),
            "Solar"   => ($"☀  SOLAR FLARE  —  Sun-touch (30×) chance!   {s}s", "ffe08a"),
            "Strange" => ($"🌀  STRANGE  —  Weird, brown-green (24×)!   {s}s", "c8d08a"),
            "Ground"  => ($"🟫  GROUND SHIFT  —  Big / Gigantic crops!   {s}s", "d8b48a"),
            _ => ("", "ffffff"),
        };
        _eventBanner.Text = text;
        _eventBanner.AddThemeColorOverride("font_color", new Color(col));
    }

    private static readonly string[] EventTypes = { "Storm", "Rainbow", "Solar", "Strange", "Ground" };

    private void StartEvent()
    {
        _activeEvent = EventTypes[(int)(GD.Randi() % (uint)EventTypes.Length)];
        _eventRemaining = EventDuration;
        SetWeatherVisuals(_activeEvent);
        switch (_activeEvent)
        {
            case "Storm":
                _eventTint.Color = new Color(0.10f, 0.13f, 0.30f, 0.14f);
                _sfx.Play("storm", -4f);
                ShowToast("⛈ STORM rolling in! Crops that ripen now can become SHOCKED (48×)!", new Color("bcd0ff"));
                break;
            case "Rainbow":
                _eventTint.Color = new Color(1f, 0.5f, 0.9f, 0.06f);
                _sfx.Play("rainbow", -5f);
                ShowToast("🌈 RAINBOW! Crops that ripen now often turn Rainbow (25×)!", new Color("ffc4f0"));
                break;
            case "Solar":
                _eventTint.Color = new Color(1f, 0.8f, 0.2f, 0.12f);
                _sfx.Play("solar", -5f);
                ShowToast("☀ SOLAR FLARE! Crops that ripen now can gain Sun-touch (30×)!", new Color("ffe08a"));
                break;
            case "Strange":
                _eventTint.Color = new Color(0.4f, 0.5f, 0.2f, 0.18f);
                _sfx.Play("strange", -5f);
                ShowToast("🌀 STRANGE! Crops grow Weird & brown-green (24×)!", new Color("c8d08a"));
                break;
            case "Ground":
                _eventTint.Color = new Color(0.4f, 0.28f, 0.14f, 0.18f);
                _sfx.Play("ground", -4f);
                ShowToast("🟫 GROUND SHIFT! Every crop that ripens turns Big or Gigantic!", new Color("d8b48a"));
                break;
        }
        _eventTint.Visible = true;
    }

    private void EndEvent()
    {
        _activeEvent = "";
        _nextEventIn = 120f - EventDuration; // ~2 minutes between event starts
        _eventTint.Visible = false;
        SetWeatherVisuals("");
        _sfx.Play("clear");
        ShowToast("☀ The weather clears.", new Color(1, 1, 1, 0.85f));
    }

    private void SetWeatherVisuals(string evt)
    {
        _stormFx.Visible = evt == "Storm";
        _rainFx.Emitting = evt == "Storm";
        _rainbowFx.Visible = evt == "Rainbow";

        switch (evt)
        {
            case "Storm":
                _sunBaseEnergy = 0.4f; _sun.LightColor = new Color("aab2c4"); _env.AmbientLightEnergy = 0.25f;
                SetSky("2a2f3a", "474f5e", "262b26", "39423a"); _lightningIn = 1.0; break;
            case "Rainbow":
                _sunBaseEnergy = 1.3f; _sun.LightColor = Colors.White; _env.AmbientLightEnergy = 0.6f;
                SetSky("5a96e6", "d8f0ff", "3a5a30", "9cc77a"); break;
            case "Solar":
                _sunBaseEnergy = 1.9f; _sun.LightColor = new Color("fff0c0"); _env.AmbientLightEnergy = 0.8f;
                SetSky("e8a040", "ffe0a0", "6a5a30", "c8a050"); break;
            case "Strange":
                _sunBaseEnergy = 0.7f; _sun.LightColor = new Color("aac070"); _env.AmbientLightEnergy = 0.4f;
                SetSky("4a5a2a", "7a8a4a", "3a4a1a", "6a7a3a"); break;
            case "Ground":
                _sunBaseEnergy = 0.8f; _sun.LightColor = new Color("d0a070"); _env.AmbientLightEnergy = 0.45f;
                SetSky("6a4a2a", "b08050", "4a3018", "7a5530"); break;
            default:
                _sunBaseEnergy = 1.15f; _sun.LightColor = Colors.White; _env.AmbientLightEnergy = 0.45f;
                SetSky("4a86d6", "bfe3ff", "3a5a30", "9cc77a"); break;
        }
    }

    private void SetSky(string top, string horizon, string groundBottom, string groundHorizon)
    {
        _skyMat.SkyTopColor = new Color(top);
        _skyMat.SkyHorizonColor = new Color(horizon);
        _skyMat.GroundBottomColor = new Color(groundBottom);
        _skyMat.GroundHorizonColor = new Color(groundHorizon);
    }

    /// <summary>Per-frame weather: lightning flashes, rain following the player, drifting clouds.</summary>
    private void UpdateWeather(float dt, double delta)
    {
        // The flash drives both an in-world light spike and a brief screen flash.
        _sun.LightEnergy = _sunBaseEnergy + _flash * 1.8f;
        if (_flash > 0f)
            _flash = Mathf.MoveToward(_flash, 0f, dt * 4f);
        _flashRect.Color = new Color(1, 1, 1, _flash * 0.45f);

        if (_activeEvent == "Storm")
        {
            _rainFx.GlobalPosition = _player.GlobalPosition + new Vector3(0, 16, 0); // rain over the player
            _stormFx.RotateY(dt * 0.02f);                                            // slow cloud drift
            _lightningIn -= delta;
            if (_lightningIn <= 0)
            {
                _flash = 1f;
                _sfx.Play("thunder", -3f);
                _lightningIn = 1.8 + GD.Randf() * 3.2;
            }
        }
        else if (_activeEvent == "Rainbow")
        {
            _rainbowFx.RotateY(dt * 0.008f);
        }
    }

    // ---- world building ---------------------------------------------------

    private void BuildWorld()
    {
        // Sky + ambient light.
        _skyMat = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color("4a86d6"),
            SkyHorizonColor = new Color("bfe3ff"),
            GroundBottomColor = new Color("3a5a30"),
            GroundHorizonColor = new Color("9cc77a"),
        };
        _env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.45f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            Sky = new Sky { SkyMaterial = _skyMat },
        };
        AddChild(new WorldEnvironment { Environment = _env });

        _sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-52, -48, 0),
            LightEnergy = 1.15f,
            ShadowEnabled = true,
        };
        AddChild(_sun);

        // Grass ground (visual) + a flat floor collider on layer 1 to walk on.
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(140, 140) },
            MaterialOverride = Mat(new Color("5aa04a")),
        });
        var floor = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        floor.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(140, 2, 140) },
            Position = new Vector3(0, -1, 0),
        });
        AddChild(floor);

        BuildFields();
        BuildFence();
        BuildGate();
        BuildMarket();
        BuildMagicalTree();
        BuildHiddenGrove();
        BuildPetsShop();
        BuildVine();
        BuildWeather();
        BuildPlayer();
    }

    /// <summary>A grape vine of 15 giant leaf platforms spiralling up to the Uni-Grape.</summary>
    private void BuildVine()
    {
        var basePos = new Vector3(-13f, 0, 2f);
        _vineBase = basePos;
        const int leaves = 15;
        const float step = 0.62f;
        float topY = 1.0f + leaves * step;

        // Central trunk (visual only — no collision, so you can spiral around it freely).
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.34f, Height = topY + 1.4f },
            Position = basePos + new Vector3(0, (topY + 1.4f) * 0.5f, 0),
            MaterialOverride = Mat(new Color("3f7a3a")),
        });

        var leafMat = Mat(new Color("4aa64a"), 0.8f);
        var stemMat = Mat(new Color("3a8a3a"), 0.8f);
        for (int i = 0; i < leaves; i++)
        {
            float ang = Mathf.DegToRad(i * 55f);
            float y = 1.0f + i * step;
            Vector3 p = basePos + new Vector3(Mathf.Cos(ang) * 1.9f, y, Mathf.Sin(ang) * 1.9f);

            AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 1.5f, BottomRadius = 1.5f, Height = 0.14f },
                Position = p,
                MaterialOverride = leafMat,
            });
            AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 1.9f },
                Position = basePos + new Vector3(Mathf.Cos(ang) * 0.95f, y, Mathf.Sin(ang) * 0.95f),
                RotationDegrees = new Vector3(0, -i * 55f, 90),
                MaterialOverride = stemMat,
            });

            var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };  // solid: you can stand on it
            body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 1.45f, Height = 0.2f }, Position = p });
            AddChild(body);
        }

        // Top platform (a big leaf) you climb onto.
        _vineTop = basePos + new Vector3(0, topY + 0.3f, 0);
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 2.4f, BottomRadius = 2.4f, Height = 0.2f },
            Position = _vineTop,
            MaterialOverride = leafMat,
        });
        var topBody = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        topBody.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 2.4f, Height = 0.3f }, Position = _vineTop });
        AddChild(topBody);

        // The Uni-Grape — a big glowing grape cluster above the platform.
        Vector3 grapePos = _vineTop + new Vector3(0, 1.7f, 0);
        Color[] cols = { new("b15cff"), new("4a7aff"), new("ffb02a"), new("8a30ff") };
        int ci = 0;
        int[] rows = { 4, 4, 3, 2, 1 };
        for (int r = 0; r < rows.Length; r++)
        {
            float ly = 0.7f - r * 0.34f;
            float rad = 0.55f * (1f - r * 0.16f);
            for (int j = 0; j < rows[r]; j++)
            {
                float a = Mathf.DegToRad(360f / rows[r] * j + r * 25f);
                var m = Mat(cols[ci++ % cols.Length], 0.4f);
                _uniGlow.Add(m);
                AddChild(new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = 0.26f, Height = 0.52f },
                    Position = grapePos + new Vector3(Mathf.Cos(a) * rad, ly, Mathf.Sin(a) * rad),
                    MaterialOverride = m,
                });
            }
        }

        AddChild(new Label3D
        {
            Text = "🍇 Dimensional Grapes\nof the Uni-Grape",
            Position = grapePos + new Vector3(0, 1.5f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 80, PixelSize = 0.006f, OutlineSize = 20,
            Modulate = new Color("d8b8ff"), OutlineModulate = new Color(0, 0, 0, 0.85f),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var look = new StaticBody3D { CollisionLayer = 2, CollisionMask = 0 };
        look.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.2f, 2.2f, 2.2f) }, Position = grapePos });
        look.SetMeta("kind", "unigrape");
        AddChild(look);

        AddChild(new Label3D
        {
            Text = "🍇 Climb me!",
            Position = basePos + new Vector3(0, 1.5f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 72, PixelSize = 0.006f, OutlineSize = 18,
            Modulate = new Color("c8ffc8"), OutlineModulate = new Color(0, 0, 0, 0.85f),
        });

        BuildVineBarrier(basePos);
    }

    /// <summary>A force-field dome over the vine until the player has 5 Sp.</summary>
    private void BuildVineBarrier(Vector3 center)
    {
        _vineBarrier = new Node3D();
        AddChild(_vineBarrier);

        const float r = 5.5f;

        // Translucent glowing dome (a hemisphere).
        _vineBarrier.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = r, Height = r * 2f, IsHemisphere = true, RadialSegments = 32, Rings = 12 },
            Position = center,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.4f, 0.7f, 1f, 0.20f),
                Emission = new Color(0.4f, 0.7f, 1f),
                EmissionEnabled = true,
                EmissionEnergyMultiplier = 0.5f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            },
        });

        // A spherical collider blocks the player from entering the dome.
        var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = r - 0.2f }, Position = center });
        _vineBarrier.AddChild(body);

        // Look target for the prompt.
        var look = new StaticBody3D { CollisionLayer = 2, CollisionMask = 0 };
        look.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.5f, 3f, 0.6f) }, Position = center + new Vector3(0, 1.6f, r) });
        look.SetMeta("kind", "barrier");
        _vineBarrier.AddChild(look);

        _vineBarrier.AddChild(new Label3D
        {
            Text = $"🔮 Dome — reach {Num.Fmt(VineBarrierCost)} 🪙 to enter",
            Position = center + new Vector3(0, r + 0.8f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 64, PixelSize = 0.006f, OutlineSize = 18,
            Modulate = new Color("9fd8ff"), OutlineModulate = new Color(0, 0, 0, 0.85f),
        });
    }

    private void DropBarrier()
    {
        _vineBarrierDown = true;
        if (_vineBarrier is not null)
        {
            _vineBarrier.QueueFree();
            _vineBarrier = null;
        }
    }

    /// <summary>A cluster of small glowing trees at the back-right — the Hidden Grove (1 Qi).</summary>
    private void BuildHiddenGrove()
    {
        var basePos = new Vector3(7.2f, 0, -6.8f);
        Color[] cols = { new("2fd0b0"), new("6a8cff"), new("ff4dd2") };

        for (int i = 0; i < 3; i++)
        {
            float a = Mathf.DegToRad(i * 120f);
            Vector3 p = basePos + new Vector3(Mathf.Cos(a) * 1.1f, 0, Mathf.Sin(a) * 1.1f);
            AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.26f, Height = 1.8f },
                Position = p + new Vector3(0, 0.9f, 0),
                MaterialOverride = Mat(new Color("3a2a3a")),
            });
            var m = Mat(cols[i], 0.6f);
            _groveGlow.Add(m);
            AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.9f, Height = 1.8f },
                Position = p + new Vector3(0, 2.1f, 0),
                Scale = new Vector3(1.1f, 1.0f, 1.1f),
                MaterialOverride = m,
            });
        }

        AddChild(new Label3D
        {
            Text = "🌌 Hidden Grove",
            Position = basePos + new Vector3(0, 3.5f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 96,
            PixelSize = 0.006f,
            OutlineSize = 20,
            Modulate = new Color("aef0e0"),
            OutlineModulate = new Color(0, 0, 0, 0.85f),
        });

        var solid = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        solid.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 1.6f, Height = 2f }, Position = basePos + new Vector3(0, 1f, 0) });
        AddChild(solid);

        var look = new StaticBody3D { CollisionLayer = 2, CollisionMask = 0 };
        look.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3.4f, 3f, 3.4f) }, Position = basePos + new Vector3(0, 1.5f, 0) });
        look.SetMeta("kind", "grove");
        AddChild(look);
    }

    /// <summary>A little pet stall outside the fence, left of the gate (100 Qa).</summary>
    private void BuildPetsShop()
    {
        var pos = new Vector3(-5.5f, 0, GateZ + 3.0f);
        var wood = Mat(new Color("6a4a8a"), 0.9f);
        var roof = Mat(new Color("ff9ec4"), 0.7f);

        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(3f, 1.0f, 1.0f) }, Position = pos + new Vector3(0, 0.5f, 0), MaterialOverride = wood });
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(3.3f, 0.12f, 1.3f) }, Position = pos + new Vector3(0, 1.06f, 0), MaterialOverride = Mat(new Color("563a70")) });
        foreach (float dx in new[] { -1.45f, 1.45f })
            AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.14f, 2.2f, 0.14f) }, Position = pos + new Vector3(dx, 1.1f, -0.4f), MaterialOverride = wood });

        var roofMat = roof;
        _petsGlow.Add(roofMat);
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(3.5f, 0.12f, 1.6f) }, Position = pos + new Vector3(0, 2.25f, -0.1f), RotationDegrees = new Vector3(-14, 0, 0), MaterialOverride = roofMat });

        AddChild(new Label3D
        {
            Text = "🐾 PETS",
            Position = pos + new Vector3(0, 2.7f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 96,
            PixelSize = 0.006f,
            OutlineSize = 20,
            Modulate = new Color("ffd9a8"),
            OutlineModulate = new Color(0, 0, 0, 0.85f),
        });

        var solid = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        solid.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3f, 1.1f, 1.0f) }, Position = pos + new Vector3(0, 0.55f, 0) });
        AddChild(solid);

        var look = new StaticBody3D { CollisionLayer = 2, CollisionMask = 0 };
        look.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3.2f, 1.6f, 1.6f) }, Position = pos + new Vector3(0, 1.0f, 0) });
        look.SetMeta("kind", "pets");
        AddChild(look);
    }

    /// <summary>Storm clouds + rain, and rainbow clouds + arcs — both hidden until their event.</summary>
    private void BuildWeather()
    {
        // ----- storm: dark cloud ceiling + rain -----
        _stormFx = new Node3D { Visible = false };
        AddChild(_stormFx);

        for (int i = 0; i < 11; i++)
        {
            float a = Mathf.DegToRad(i * 33f);
            float rad = 4f + (i % 3) * 3.5f;
            Cloud(_stormFx,
                new Vector3(Mathf.Cos(a) * rad, 11f + (i % 3), Mathf.Sin(a) * rad),
                1.6f + (i % 4) * 0.5f, new Color("2c3038"), 0.96f);
        }

        _rainFx = new CpuParticles3D
        {
            Emitting = false,
            Amount = 700,
            Lifetime = 1.1f,
            Position = new Vector3(0, 16, 0),
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(14, 0.5f, 14),
            Direction = new Vector3(0, -1, 0),
            Spread = 3f,
            Gravity = new Vector3(0, -45, 0),
            InitialVelocityMin = 16f,
            InitialVelocityMax = 22f,
            Mesh = new BoxMesh { Size = new Vector3(0.025f, 0.5f, 0.025f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.72f, 0.82f, 1f, 0.55f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            },
        };
        _stormFx.AddChild(_rainFx);

        // ----- rainbow: fluffy white clouds + rainbow arcs -----
        _rainbowFx = new Node3D { Visible = false };
        AddChild(_rainbowFx);

        for (int i = 0; i < 14; i++)
        {
            float a = Mathf.DegToRad(i * 26f + 12f);
            float rad = 6f + (i % 4) * 4f;
            Cloud(_rainbowFx,
                new Vector3(Mathf.Cos(a) * rad, 12f + (i % 4) * 2.5f, Mathf.Sin(a) * rad),
                1.5f + (i % 3) * 0.6f, new Color("ffffff"), 0.95f);
        }

        BuildRainbow(_rainbowFx, new Vector3(-12, -2, -46), 24f, 12f);
        BuildRainbow(_rainbowFx, new Vector3(22, -4, -52), 30f, -20f);
        BuildRainbow(_rainbowFx, new Vector3(-40, -6, 18), 26f, 70f);
    }

    /// <summary>A puffy cloud — a clump of flat unshaded blobs.</summary>
    private void Cloud(Node3D parent, Vector3 pos, float size, Color color, float alpha)
    {
        var cloud = new Node3D { Position = pos };
        parent.AddChild(cloud);
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(color, alpha),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = alpha < 1f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        };
        (Vector3 off, float r)[] blobs =
        {
            (new Vector3(0, 0, 0), 1.0f),
            (new Vector3(-1.0f, -0.1f, 0.2f), 0.7f),
            (new Vector3(1.0f, -0.05f, -0.2f), 0.75f),
            (new Vector3(0.3f, 0.4f, 0.3f), 0.65f),
            (new Vector3(-0.5f, 0.3f, -0.3f), 0.6f),
        };
        foreach ((Vector3 off, float r) in blobs)
            cloud.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = r * size, Height = r * size * 1.4f },
                Position = off * size,
                Scale = new Vector3(1.3f, 0.7f, 1.1f),
                MaterialOverride = mat,
            });
    }

    /// <summary>A rainbow as concentric vertical arcs; its lower half sinks below the ground.</summary>
    private void BuildRainbow(Node3D parent, Vector3 center, float radius, float rotYDeg)
    {
        string[] cols = { "ff3b30", "ff9500", "ffe000", "34c759", "0a84ff", "3a3ad0", "8a4dff" };
        var rb = new Node3D { Position = center, RotationDegrees = new Vector3(90, rotYDeg, 0) };
        parent.AddChild(rb);

        const float band = 0.7f;
        for (int k = 0; k < cols.Length; k++)
        {
            float inner = radius + k * band;
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(cols[k], 0.7f),
                Emission = new Color(cols[k]),
                EmissionEnabled = true,
                EmissionEnergyMultiplier = 0.6f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            };
            rb.AddChild(new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = inner, OuterRadius = inner + band, RingSegments = 48 },
                MaterialOverride = mat,
            });
        }
    }

    /// <summary>A big glowing tree at the back of the farm — unlocks the premium catalog.</summary>
    private void BuildMagicalTree()
    {
        var pos = new Vector3(0, 0, -7.8f);

        // Trunk.
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.35f, BottomRadius = 0.55f, Height = 3.2f },
            Position = pos + new Vector3(0, 1.6f, 0),
            MaterialOverride = Mat(new Color("5a3a22")),
        });

        // Glowing canopy (several overlapping blobs).
        (Vector3 off, float r)[] blobs =
        {
            (new Vector3(0, 3.6f, 0), 1.5f),
            (new Vector3(-1.1f, 3.2f, 0.3f), 1.1f),
            (new Vector3(1.1f, 3.3f, -0.2f), 1.15f),
            (new Vector3(0.2f, 4.3f, 0.4f), 1.0f),
        };
        foreach ((Vector3 off, float r) in blobs)
        {
            var m = Mat(new Color("8a5cff"), 0.7f);
            _treeGlow.Add(m);
            AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = r, Height = r * 2f }, Position = pos + off, MaterialOverride = m });
        }

        // Floating fruit orbs in tier colours.
        Color[] orbCols = { new("ffe066"), new("00e5ff"), new("ff7a1a"), new("b14dff"), new("ffe9a8"), new("ff2d55") };
        for (int i = 0; i < orbCols.Length; i++)
        {
            float a = Mathf.DegToRad(i * 60f);
            var m = Mat(orbCols[i], 0.4f);
            _treeGlow.Add(m);
            AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.22f, Height = 0.44f },
                Position = pos + new Vector3(Mathf.Cos(a) * 1.5f, 3.5f + Mathf.Sin(a) * 0.6f, Mathf.Sin(a) * 1.2f),
                MaterialOverride = m,
            });
        }

        // Sign.
        AddChild(new Label3D
        {
            Text = "✨ Magical Tree ✨",
            Position = pos + new Vector3(0, 5.4f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 110,
            PixelSize = 0.006f,
            OutlineSize = 22,
            Modulate = new Color("e0b8ff"),
            OutlineModulate = new Color(0, 0, 0, 0.85f),
        });

        // Solid trunk (layer 1) so you can't walk through it.
        var solid = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        solid.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.6f, Height = 3.2f }, Position = pos + new Vector3(0, 1.6f, 0) });
        AddChild(solid);

        // Look target (layer 2) for the unlock prompt.
        var look = new StaticBody3D { CollisionLayer = 2, CollisionMask = 0 };
        look.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.6f, 3.2f, 1.6f) }, Position = pos + new Vector3(0, 1.6f, 0) });
        look.SetMeta("kind", "tree");
        AddChild(look);
    }

    /// <summary>Two open tilled-dirt fields, each a grid of planting spots.</summary>
    private void BuildFields()
    {
        _plots = new Plot3D[Catalog.PlotCount];

        float fieldW = (Catalog.FieldCols - 1) * SpotSpacing + 2.0f;
        float fieldD = (Catalog.FieldRows - 1) * SpotSpacing + 2.0f;
        float[] centersX = { -4.6f, 4.6f };

        var dirt = Mat(new Color("5b3d28"), roughness: 1f);
        var furrow = Mat(new Color("4a3120"), roughness: 1f);

        for (int field = 0; field < Catalog.FieldCount; field++)
        {
            float cx = centersX[field];
            float cz = -0.5f;

            // Flat, open dirt patch (very low slab so it reads as ground, not a box).
            AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(fieldW, 0.08f, fieldD) },
                Position = new Vector3(cx, 0.04f, cz),
                MaterialOverride = dirt,
            });

            // A few tilled furrow ridges for a natural, worked look.
            for (int r = 0; r < Catalog.FieldRows; r++)
            {
                float fz = cz + (r - (Catalog.FieldRows - 1) * 0.5f) * SpotSpacing;
                AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(fieldW * 0.92f, 0.05f, 0.14f) },
                    Position = new Vector3(cx, 0.085f, fz),
                    MaterialOverride = furrow,
                });
            }

            // Planting spots over the dirt.
            float ox = (Catalog.FieldCols - 1) * 0.5f;
            float oz = (Catalog.FieldRows - 1) * 0.5f;
            for (int row = 0; row < Catalog.FieldRows; row++)
            {
                for (int col = 0; col < Catalog.FieldCols; col++)
                {
                    int i = field * Catalog.PlotsPerField + row * Catalog.FieldCols + col;
                    var plot = new Plot3D
                    {
                        Index = i,
                        State = _states[i],
                        Position = new Vector3(cx + (col - ox) * SpotSpacing, 0.08f, cz + (row - oz) * SpotSpacing),
                    };
                    _plots[i] = plot;
                    AddChild(plot);
                }
            }
        }
    }

    /// <summary>Brown fence around the farm, with a gap on the front for the gate.</summary>
    private void BuildFence()
    {
        // Back and sides are solid runs; the front is split around the gate gap.
        FenceRun(new Vector3(-FenceX, 0, -FenceZ), new Vector3(FenceX, 0, -FenceZ));   // back
        FenceRun(new Vector3(-FenceX, 0, -FenceZ), new Vector3(-FenceX, 0, FenceZ));   // left
        FenceRun(new Vector3(FenceX, 0, -FenceZ), new Vector3(FenceX, 0, FenceZ));     // right
        FenceRun(new Vector3(-FenceX, 0, GateZ), new Vector3(-GateHalfWidth, 0, GateZ)); // front-left of gate
        FenceRun(new Vector3(GateHalfWidth, 0, GateZ), new Vector3(FenceX, 0, GateZ));   // front-right of gate
    }

    private void FenceRun(Vector3 a, Vector3 b)
    {
        var wood = Mat(new Color("6b4a2c"), roughness: 0.9f);
        var woodDark = Mat(new Color("5a3d24"), roughness: 0.9f);

        Vector3 d = b - a;
        float length = d.Length();
        bool alongX = Mathf.Abs(d.X) > Mathf.Abs(d.Z);
        Vector3 mid = (a + b) * 0.5f;

        // Two horizontal rails.
        Vector3 railSize = alongX ? new Vector3(length, 0.1f, 0.08f) : new Vector3(0.08f, 0.1f, length);
        foreach (float y in new[] { 0.5f, 0.95f })
            AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = railSize }, Position = mid + new Vector3(0, y, 0), MaterialOverride = wood });

        // Posts along the run.
        int posts = Mathf.Max(2, Mathf.RoundToInt(length / 1.9f) + 1);
        for (int i = 0; i < posts; i++)
        {
            Vector3 p = a.Lerp(b, posts == 1 ? 0 : (float)i / (posts - 1));
            AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.16f, 1.3f, 0.16f) },
                Position = new Vector3(p.X, 0.65f, p.Z),
                MaterialOverride = woodDark,
            });
        }

        // Collider so the player can't walk through (layer 1).
        var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        Vector3 colSize = alongX ? new Vector3(length, 1.3f, 0.2f) : new Vector3(0.2f, 1.3f, length);
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = colSize }, Position = mid + new Vector3(0, 0.65f, 0) });
        AddChild(body);
    }

    /// <summary>A swinging gate hinged at the left side of the front gap.</summary>
    private void BuildGate()
    {
        var wood = Mat(new Color("7a5532"), roughness: 0.85f);
        float span = GateHalfWidth * 2f; // gate length

        _gatePivot = new Node3D { Position = new Vector3(-GateHalfWidth, 0, GateZ) };
        AddChild(_gatePivot);

        // Horizontal rails (local x from 0..span).
        foreach (float y in new[] { 0.45f, 0.95f })
            _gatePivot.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(span, 0.12f, 0.08f) },
                Position = new Vector3(span * 0.5f, y, 0),
                MaterialOverride = wood,
            });

        // Vertical pickets + a diagonal brace.
        foreach (float lx in new[] { 0.15f, span * 0.5f, span - 0.15f })
            _gatePivot.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.1f, 1.15f, 0.08f) },
                Position = new Vector3(lx, 0.6f, 0),
                MaterialOverride = wood,
            });

        // Blocker (layer 1) — swings with the gate, so it only blocks when closed.
        var blocker = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        blocker.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(span, 1.3f, 0.16f) }, Position = new Vector3(span * 0.5f, 0.65f, 0) });
        _gatePivot.AddChild(blocker);

        // Look target (layer 2) for the [E] open/close prompt.
        var look = new StaticBody3D { CollisionLayer = 2, CollisionMask = 0 };
        look.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(span, 1.4f, 0.5f) }, Position = new Vector3(span * 0.5f, 0.9f, 0) });
        look.SetMeta("kind", "gate");
        _gatePivot.AddChild(look);
    }

    /// <summary>A market stall just outside the gate where you sell (and buy).</summary>
    private void BuildMarket()
    {
        var marketPos = new Vector3(5.5f, 0, GateZ + 3.0f);

        var wood = Mat(new Color("8a5a34"), roughness: 0.9f);
        var top = Mat(new Color("6a4327"), roughness: 0.9f);
        var awning = Mat(new Color("c0392b"), roughness: 0.8f);

        // Counter + top.
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(3f, 1.0f, 1.0f) }, Position = marketPos + new Vector3(0, 0.5f, 0), MaterialOverride = wood });
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(3.3f, 0.12f, 1.3f) }, Position = marketPos + new Vector3(0, 1.06f, 0), MaterialOverride = top });

        // Roof posts + awning.
        foreach (float dx in new[] { -1.45f, 1.45f })
            AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.14f, 2.2f, 0.14f) }, Position = marketPos + new Vector3(dx, 1.1f, -0.4f), MaterialOverride = wood });
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(3.5f, 0.12f, 1.6f) }, Position = marketPos + new Vector3(0, 2.25f, -0.1f), RotationDegrees = new Vector3(-14, 0, 0), MaterialOverride = awning });

        // "MARKET" sign.
        AddChild(new Label3D
        {
            Text = "MARKET",
            Position = marketPos + new Vector3(0, 2.7f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 96,
            PixelSize = 0.006f,
            OutlineSize = 20,
            Modulate = new Color("ffe066"),
            OutlineModulate = new Color(0, 0, 0, 0.85f),
        });

        // Solid counter so you can't walk through it (layer 1).
        var solid = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        solid.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3f, 1.1f, 1.0f) }, Position = marketPos + new Vector3(0, 0.55f, 0) });
        AddChild(solid);

        // Look target (layer 2) for the sell prompt.
        var look = new StaticBody3D { CollisionLayer = 2, CollisionMask = 0 };
        look.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3.2f, 1.6f, 1.6f) }, Position = marketPos + new Vector3(0, 1.0f, 0) });
        look.SetMeta("kind", "market");
        AddChild(look);
    }

    private void BuildPlayer()
    {
        _player = new CharacterBody3D
        {
            CollisionLayer = 0,
            CollisionMask = 1,
            Position = new Vector3(0, 1.2f, GateZ + 4.5f),   // spawn just outside the gate
        };
        _player.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.7f },
            Position = new Vector3(0, 0.85f, 0),
        });
        _camera = new Camera3D { Fov = 70, Current = true, Position = new Vector3(0, 1.5f, 0) };
        _player.AddChild(_camera);

        _ray = new RayCast3D
        {
            TargetPosition = new Vector3(0, 0, -ReachDistance),
            CollisionMask = 2,
            Enabled = true,
        };
        _camera.AddChild(_ray);

        AddChild(_player);
        _player.LookAt(new Vector3(0, _player.Position.Y, 0), Vector3.Up); // face the gate/farm
    }

    // ---- growth & economy -------------------------------------------------

    /// <summary>A per-plant size, mostly near 1 with the odd small or giant crop.</summary>
    private static float RollSize()
    {
        float r = GD.Randf();
        return 0.7f + 0.85f * r; // ~0.70 .. 1.55
    }

    private static void Grow(PlotState st, float dt, string? evt = null)
    {
        if (st.Seed is null || st.IsReady)
            return;

        st.Growth += dt;
        if (st.IsReady && !st.MutationRolled)
        {
            st.Mutation = Mutation.Roll(evt, st.Seed.Rarity);
            st.MutationRolled = true;
        }
    }

    private void HarvestAll()
    {
        double total = 0;
        int count = 0;
        foreach (Plot3D p in _plots)
        {
            if (p.State.IsReady)
            {
                total += p.State.Payout * _yieldMult;
                count++;
                ClearPlot(p.Index);
            }
        }

        if (count == 0)
            ShowToast("Nothing ripe to harvest yet", new Color(1, 1, 1, 0.8f));
        else
        {
            _basketValue += total;
            _basketCount += count;
            _sfx.Play("harvest");
            ShowToast($"Harvested {count} → basket  (+{Num.Fmt(total)})  ·  sell at the market", new Color("9be67a"));
        }
    }

    private void AutoStep()
    {
        // Auto handles the whole loop, selling straight to coins so it can reinvest.
        double earned = 0;
        int harvested = 0;
        foreach (Plot3D p in _plots)
        {
            if (p.State.IsReady)
            {
                earned += p.State.Payout * _yieldMult;
                harvested++;
                ClearPlot(p.Index);
            }
        }
        if (earned > 0)
            _coins += earned;

        int empties = 0;
        foreach (Plot3D p in _plots)
            if (p.State.IsEmpty && p.State.SlaveOf < 0) empties++;

        if (empties > 0)
        {
            double perPlot = _coins / empties;
            SeedType chosen = Seeds[0];
            int chosenIdx = 0;
            for (int i = 0; i < Seeds.Count; i++)
            {
                // Auto only buys unlocked single-tile seeds it can afford.
                if (!IndexUnlocked(i) || Seeds[i].Footprint > 1) continue;
                if (Seeds[i].Cost <= perPlot)
                {
                    chosen = Seeds[i];
                    chosenIdx = i;
                }
            }

            foreach (Plot3D p in _plots)
            {
                if (!p.State.IsEmpty || p.State.SlaveOf >= 0) continue;
                if (_coins < chosen.Cost) break;
                _coins -= chosen.Cost;
                _selected = chosenIdx;
                p.State.Plant(chosen, RollSize());
            }
        }

        if (harvested > 0)
            ShowToast($"🤖 sold {harvested}  +{Num.Fmt(earned)} 🪙", new Color("9be67a"));
    }

    private void ToggleAuto()
    {
        _auto = !_auto;
        UpdateAutoButton();
        ShowToast(_auto ? "Auto-play on — sit back!" : "Auto-play off — your turn 🌱",
            new Color("d9f7a6"));
    }

    private void UpdateAutoButton() => _autoBtn.Text = _auto ? "🤖 Auto: ON" : "🤖 Auto: OFF";

    // ---- main menu (Esc) --------------------------------------------------

    private void ToggleMenu()
    {
        if (_menuOpen) CloseMenu();
        else OpenMenu();
    }

    private void OpenMenu()
    {
        _menuOpen = true;
        _menuRoot.Visible = true;
        _crosshair.Visible = false;
        _prompt.Text = "";
        UpdateAutoButton();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void CloseMenu()
    {
        _menuOpen = false;
        _menuRoot.Visible = false;
        _restartArmed = 0;
        _restartBtn.Text = "🔄 Restart Game";
        SetWalkMode(true);
    }

    private void OnRestartPressed()
    {
        if (_restartArmed <= 0)
        {
            _restartArmed = 4.0;   // require a second click within 4s to confirm
            _restartBtn.Text = "⚠ Click again to confirm reset";
        }
        else
        {
            _restartArmed = 0;
            _restartBtn.Text = "🔄 Restart Game";
            ResetGame();
        }
    }

    private void ResetGame()
    {
        _coins = Catalog.StartingCoins;
        _basketCount = 0;
        _basketValue = 0;
        _treeUnlocked = _groveUnlocked = _petsUnlocked = _uniUnlocked = false;
        _vineBarrierDown = false;
        if (_vineBarrier is null) BuildVineBarrier(_vineBase);
        _ownedPets.Clear();
        RecomputePetBonuses();
        _selected = 0;
        _auto = false;
        UpdateAutoButton();

        for (int i = 0; i < _states.Length; i++)
        {
            _states[i].Clear();
            _plots[i].PlantOffset = Vector3.Zero;
            _plots[i].ExtraScale = 1f;
            _plots[i].Refresh(0f);
        }

        _activeEvent = "";
        _nextEventIn = 120;
        _eventRemaining = 0;
        _eventBanner.Visible = false;
        SetWeatherVisuals("");

        SetTreeGlow(false);
        SetGroveGlow(false);
        SetUniGlow(false);
        SetPetsGlow(false);
        _petsPanel.Visible = false;

        RebuildSeedList();
        PopulateShop();
        _lastShopCoins = -1;
        _lastShopSelected = -1;

        SaveGame();
        _sfx.Play("unlock", -6f);
        ShowToast("🔄 Game reset — fresh start!", new Color("d9f7a6"));
        CloseMenu();
    }

    // ---- Grow All math quiz ----------------------------------------------

    private void OnGrowAllPressed()
    {
        if (_quizOpen)
            return;

        bool anyGrowing = false;
        foreach (Plot3D p in _plots)
            if (!p.State.IsEmpty && !p.State.IsReady) { anyGrowing = true; break; }

        if (!anyGrowing)
        {
            ShowToast("Plant some seeds first! 🌱", new Color(1, 1, 1, 0.85f));
            return;
        }

        bool wasWalking = _walkMode;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _crosshair.Visible = false;
        _quizOpen = true;

        var quiz = new MathQuiz
        {
            OnComplete = () => { _quizOpen = false; GrowAllNow(); SetWalkMode(wasWalking); },
            OnCancel = () => { _quizOpen = false; SetWalkMode(wasWalking); },
        };
        _uiRoot.AddChild(quiz);
    }

    private void GrowAllNow()
    {
        int grew = 0;
        foreach (Plot3D p in _plots)
        {
            PlotState st = p.State;
            if (!st.IsEmpty && !st.IsReady)
            {
                st.Growth = st.Seed!.GrowSeconds;
                if (!st.MutationRolled)
                {
                    st.Mutation = Mutation.Roll(_activeEvent, st.Seed.Rarity);
                    st.MutationRolled = true;
                }
                grew++;
            }
        }
        if (grew > 0) _sfx.Play("grow");
        ShowToast(grew > 0 ? $"🌱 Grew {grew} crop{(grew == 1 ? "" : "s")}! Harvest away!"
                           : "All grown!", new Color("9be67a"));
    }

    // ---- UI ---------------------------------------------------------------

    private void BuildUi()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        _uiRoot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_uiRoot);

        // Weather tint — added first so it sits behind the panels (tints the world view only).
        _eventTint = new ColorRect { Color = new Color(0, 0, 0, 0), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _uiRoot.AddChild(_eventTint);
        _eventTint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        BuildTopBar();
        BuildShop();
        BuildPetsPanel();
        BuildCrosshairAndPrompt();
        BuildHint();

        _eventBanner = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        _eventBanner.AddThemeFontSizeOverride("font_size", 22);
        _eventBanner.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        _eventBanner.AddThemeConstantOverride("shadow_offset_x", 1);
        _eventBanner.AddThemeConstantOverride("shadow_offset_y", 1);
        _uiRoot.AddChild(_eventBanner);
        _eventBanner.AnchorLeft = 0; _eventBanner.AnchorRight = 1;
        _eventBanner.AnchorTop = 0; _eventBanner.AnchorBottom = 0;
        _eventBanner.OffsetTop = 86; _eventBanner.OffsetBottom = 116;

        _toast = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _toast.AddThemeFontSizeOverride("font_size", 20);
        _toast.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
        _toast.AddThemeConstantOverride("shadow_offset_x", 1);
        _toast.AddThemeConstantOverride("shadow_offset_y", 1);
        _uiRoot.AddChild(_toast);
        _toast.AnchorLeft = 0; _toast.AnchorRight = 1;
        _toast.AnchorTop = 1; _toast.AnchorBottom = 1;
        _toast.OffsetTop = -100; _toast.OffsetBottom = -66;
        _toast.SelfModulate = new Color(1, 1, 1, 0);

        BuildMainMenu();

        // Lightning flash overlay (topmost, transparent until a strike).
        _flashRect = new ColorRect { Color = new Color(1, 1, 1, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        _uiRoot.AddChild(_flashRect);
        _flashRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    private void BuildCrosshairAndPrompt()
    {
        _crosshair = new Label
        {
            Text = "+",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _crosshair.AddThemeFontSizeOverride("font_size", 26);
        _crosshair.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.8f));
        _crosshair.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
        _crosshair.AddThemeConstantOverride("shadow_offset_x", 1);
        _crosshair.AddThemeConstantOverride("shadow_offset_y", 1);
        _uiRoot.AddChild(_crosshair);
        _crosshair.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        _prompt = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _prompt.AddThemeFontSizeOverride("font_size", 19);
        _prompt.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        _prompt.AddThemeConstantOverride("shadow_offset_x", 1);
        _prompt.AddThemeConstantOverride("shadow_offset_y", 1);
        _uiRoot.AddChild(_prompt);
        _prompt.AnchorLeft = 0; _prompt.AnchorRight = 1;
        _prompt.AnchorTop = 0.5f; _prompt.AnchorBottom = 0.5f;
        _prompt.OffsetTop = 34; _prompt.OffsetBottom = 64;
    }

    private void BuildTopBar()
    {
        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", Card(new Color(0.18f, 0.29f, 0.16f, 0.92f), 14));
        _uiRoot.AddChild(bar);
        bar.AnchorLeft = 0; bar.AnchorRight = 1; bar.AnchorTop = 0; bar.AnchorBottom = 0;
        bar.OffsetLeft = 12; bar.OffsetRight = -12; bar.OffsetTop = 12; bar.OffsetBottom = 78;

        var inner = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right" })
            inner.AddThemeConstantOverride(m, 16);
        bar.AddChild(inner);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        inner.AddChild(row);

        var title = new Label { Text = "🌱 ReadySetGrow", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 26);
        title.AddThemeColorOverride("font_color", new Color("eaffd8"));
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(title);

        _basketLabel = new Label { Text = "🧺 empty", VerticalAlignment = VerticalAlignment.Center };
        _basketLabel.AddThemeFontSizeOverride("font_size", 18);
        _basketLabel.AddThemeColorOverride("font_color", new Color("c8f0a0"));
        row.AddChild(_basketLabel);

        _coinsLabel = new Label { Text = $"🪙 {Num.Fmt(_coins)}", VerticalAlignment = VerticalAlignment.Center };
        _coinsLabel.AddThemeFontSizeOverride("font_size", 24);
        _coinsLabel.AddThemeColorOverride("font_color", new Color("ffe066"));
        row.AddChild(_coinsLabel);

        row.AddChild(MakeButton("🌱 Grow All", OnGrowAllPressed));
        row.AddChild(MakeButton("Harvest All", HarvestAll));
        row.AddChild(MakeButton("☰ Menu", OpenMenu));
    }

    private void BuildShop()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Card(new Color(0.13f, 0.18f, 0.10f, 0.94f), 16));
        _uiRoot.AddChild(panel);
        panel.AnchorLeft = 1; panel.AnchorRight = 1; panel.AnchorTop = 0; panel.AnchorBottom = 1;
        panel.OffsetLeft = -384; panel.OffsetRight = -12; panel.OffsetTop = 90; panel.OffsetBottom = -12;
        panel.GrowHorizontal = Control.GrowDirection.Begin;   // never grow off the right edge
        panel.ClipContents = true;

        var margin = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(m, 14);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);

        _shopHeader = new Label { ClipText = true };
        _shopHeader.AddThemeFontSizeOverride("font_size", 20);
        _shopHeader.AddThemeColorOverride("font_color", new Color("eaffd8"));
        box.AddChild(_shopHeader);

        var hint = new Label
        {
            Text = "Buy a seed here (click, or scroll the wheel while walking). Plant it with E, then sell the harvest at the market.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        hint.AddThemeFontSizeOverride("font_size", 13);
        hint.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.6f));
        box.AddChild(hint);

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        box.AddChild(scroll);

        _shopList = new VBoxContainer();
        _shopList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _shopList.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_shopList);
    }

    private void BuildHint()
    {
        _hint = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _hint.AddThemeFontSizeOverride("font_size", 13);
        _hint.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.6f));
        _hint.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
        _hint.AddThemeConstantOverride("shadow_offset_x", 1);
        _hint.AddThemeConstantOverride("shadow_offset_y", 1);
        _uiRoot.AddChild(_hint);
        _hint.AnchorLeft = 0; _hint.AnchorRight = 1; _hint.AnchorTop = 1; _hint.AnchorBottom = 1;
        _hint.OffsetLeft = 18; _hint.OffsetRight = -392; _hint.OffsetTop = -30; _hint.OffsetBottom = -8;
    }

    private void PopulateShop()
    {
        foreach (Node c in _shopList.GetChildren())
            c.QueueFree();
        _shopRows.Clear();

        _shopHeader.Text = $"🛒 Market — Seed Shop  ·  {Seeds.Count} seeds";

        string lastRarity = "";
        for (int i = 0; i < Seeds.Count; i++)
        {
            SeedType seed = Seeds[i];
            int index = i;

            if (i == BaseCount) { AddShopDivider("✨  MAGICAL TREE  ✨", "e0b8ff"); lastRarity = ""; }
            else if (i == TreeEnd) { AddShopDivider("🌌  HIDDEN GROVE  🌌", "aef0e0"); lastRarity = ""; }
            else if (i == GroveEnd) { AddShopDivider("🍇  UNI-GRAPE  🍇", "c89aff"); lastRarity = ""; }

            if (seed.Rarity != lastRarity)
            {
                lastRarity = seed.Rarity;
                var section = new Label { Text = seed.Rarity == "Centurnial" ? "🎁 CENTURNIAL SEED PACK" : seed.Rarity.ToUpper() };
                section.AddThemeFontSizeOverride("font_size", 13);
                section.AddThemeColorOverride("font_color", seed.RarityColor);
                _shopList.AddChild(section);
            }

            var btn = new Button
            {
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
                CustomMinimumSize = new Vector2(0, 50),
            };
            btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
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
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            info.AddThemeFontSizeOverride("normal_font_size", 14);
            info.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            info.OffsetLeft = 12; info.OffsetTop = 5;
            info.OffsetRight = -10; info.OffsetBottom = -5;
            btn.AddChild(info);

            _shopRows.Add(new ShopRow { Seed = seed, Btn = btn, Box = sbNormal, Info = info });
        }

        UpdateShop();   // fill row text now (don't wait for the next coin-change refresh)
    }

    private void AddShopDivider(string text, string colorHex)
    {
        var d = new Label { Text = text };
        d.AddThemeFontSizeOverride("font_size", 15);
        d.AddThemeColorOverride("font_color", new Color(colorHex));
        _shopList.AddChild(d);
    }

    private void SelectSeed(int index)
    {
        if (!IndexUnlocked(index))
        {
            _sfx.Play("error");
            ShowToast($"🔒 Locked — {LockMessage(index)} to use {Seeds[index].Name}", new Color("ff8a7a"));
            return;
        }
        _selected = index;
        _sfx.Play("select");
        ShowToast($"Selected {Seeds[index].Name}", new Color(1, 1, 1, 0.85f));
    }

    private void UpdateShop()
    {
        // _shopRows[k] corresponds to Seeds[k] (one row per seed, in order).
        for (int k = 0; k < _shopRows.Count; k++)
        {
            ShopRow row = _shopRows[k];
            bool unlocked = IndexUnlocked(k);
            bool selected = _selected == k;
            bool afford = unlocked && _coins >= row.Seed.Cost;

            row.Box.BorderColor = selected ? new Color("ffe066") : new Color(0, 0, 0, 0.25f);
            int bw = selected ? 3 : 1;
            row.Box.BorderWidthTop = row.Box.BorderWidthBottom =
                row.Box.BorderWidthLeft = row.Box.BorderWidthRight = bw;
            row.Btn.Modulate = !unlocked ? new Color(1, 1, 1, 0.4f) : afford ? Colors.White : new Color(1, 1, 1, 0.55f);

            string rc = row.Seed.RarityColor.ToHtml(false);
            if (!unlocked)
            {
                row.Info.Text =
                    $"[b]{row.Seed.Name}[/b]  [color=#{rc}]{row.Seed.Rarity}[/color]\n" +
                    $"[color=#9aa6b2]🔒 {LockMessage(k)}[/color]";
            }
            else
            {
                string costCol = afford ? "ffe066" : "ff8a7a";
                string foot = row.Seed.Footprint >= 4 ? "  ·  4 spaces" : "";
                row.Info.Text =
                    $"[b]{row.Seed.Name}[/b]  [color=#{rc}]{row.Seed.Rarity}[/color]\n" +
                    $"[color=#{costCol}]{Num.Fmt(row.Seed.Cost)}🪙[/color]  " +
                    $"sells {Num.Fmt(row.Seed.BaseValue)}  ·  {FormatGrow(row.Seed.GrowSeconds)}{foot}";
            }
        }
    }

    private void BuildPetsPanel()
    {
        _petsPanel = new PanelContainer { Visible = false };
        _petsPanel.AddThemeStyleboxOverride("panel", Card(new Color(0.16f, 0.12f, 0.18f, 0.94f), 16));
        _uiRoot.AddChild(_petsPanel);
        _petsPanel.AnchorLeft = 0; _petsPanel.AnchorRight = 0; _petsPanel.AnchorTop = 0; _petsPanel.AnchorBottom = 1;
        _petsPanel.OffsetLeft = 12; _petsPanel.OffsetRight = 312; _petsPanel.OffsetTop = 90; _petsPanel.OffsetBottom = -40;
        _petsPanel.GrowHorizontal = Control.GrowDirection.End;   // never grow off the left edge
        _petsPanel.ClipContents = true;

        var margin = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(m, 14);
        _petsPanel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);

        var header = new Label { Text = "🐾 Pets Shop", ClipText = true };
        header.AddThemeFontSizeOverride("font_size", 20);
        header.AddThemeColorOverride("font_color", new Color("ffd9a8"));
        box.AddChild(header);

        var hint = new Label
        {
            Text = "Adopt pets for permanent boosts. Yield pets raise sell value; speed pets grow crops faster. Bonuses stack.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        hint.AddThemeFontSizeOverride("font_size", 13);
        hint.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.6f));
        box.AddChild(hint);

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        box.AddChild(scroll);

        _petsList = new VBoxContainer();
        _petsList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _petsList.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_petsList);
    }

    private void PopulatePets()
    {
        foreach (Node c in _petsList.GetChildren())
            c.QueueFree();
        _petRows.Clear();

        string lastTier = "";
        foreach (Catalog.Pet pet in Catalog.Pets)
        {
            if (pet.Tier != lastTier)
            {
                lastTier = pet.Tier;
                var section = new Label { Text = pet.Tier.ToUpper() };
                section.AddThemeFontSizeOverride("font_size", 13);
                section.AddThemeColorOverride("font_color", RarityColorOf(pet.Tier));
                _petsList.AddChild(section);
            }

            var btn = new Button { MouseDefaultCursorShape = Control.CursorShape.PointingHand, CustomMinimumSize = new Vector2(0, 50) };
            btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var sb = Card(new Color("3a2e44"), 10); sb.ContentMarginLeft = 12;
            btn.AddThemeStyleboxOverride("normal", sb);
            btn.AddThemeStyleboxOverride("hover", Card(new Color("48395a"), 10));
            btn.AddThemeStyleboxOverride("pressed", Card(new Color("2e2436"), 10));
            btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            Catalog.Pet captured = pet;
            btn.Pressed += () => BuyPet(captured);
            _petsList.AddChild(btn);

            var info = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            info.AddThemeFontSizeOverride("normal_font_size", 14);
            info.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            info.OffsetLeft = 12; info.OffsetTop = 4; info.OffsetRight = -10; info.OffsetBottom = -4;
            btn.AddChild(info);

            _petRows.Add(new PetRow { Pet = pet, Btn = btn, Info = info });
        }
        UpdatePets();
    }

    private void UpdatePets()
    {
        foreach (PetRow row in _petRows)
        {
            bool owned = _ownedPets.Contains(row.Pet.Name);
            bool afford = _coins >= row.Pet.Cost;
            row.Btn.Modulate = owned ? new Color(0.8f, 1f, 0.8f) : afford ? Colors.White : new Color(1, 1, 1, 0.5f);

            string tc = RarityColorOf(row.Pet.Tier).ToHtml(false);
            string eff = row.Pet.Kind == "yield"
                ? $"+{row.Pet.Percent * 100:0}% sell value"
                : $"+{row.Pet.Percent * 100:0}% growth speed";

            if (owned)
                row.Info.Text = $"[b]{row.Pet.Name}[/b]  [color=#{tc}]{row.Pet.Tier}[/color]\n[color=#9be67a]✓ Owned[/color]  ·  {eff}";
            else
            {
                string costCol = afford ? "ffe066" : "ff8a7a";
                row.Info.Text = $"[b]{row.Pet.Name}[/b]  [color=#{tc}]{row.Pet.Tier}[/color]\n{eff}  ·  [color=#{costCol}]{Num.Fmt(row.Pet.Cost)}🪙[/color]";
            }
        }
    }

    private static Color RarityColorOf(string tier) => tier switch
    {
        "Legendary" => new Color("ffb000"),
        "Secret"    => new Color("ff3df0"),
        "Divine"    => new Color("ffe06a"),
        "Ultra"     => new Color("00e5ff"),
        "Titan"     => new Color("ff7a1a"),
        "Entity"    => new Color("b14dff"),
        "Eternal"   => new Color("ffe9a8"),
        _            => new Color("c8c8c8"),
    };

    private void BuildMainMenu()
    {
        _menuRoot = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _uiRoot.AddChild(_menuRoot);
        _menuRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.62f), MouseFilter = Control.MouseFilterEnum.Stop };
        _menuRoot.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var center = new CenterContainer();
        _menuRoot.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(360, 0) };
        panel.AddThemeStyleboxOverride("panel", Card(new Color(0.14f, 0.20f, 0.12f, 0.98f), 18));
        center.AddChild(panel);

        var margin = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(m, 22);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        margin.AddChild(box);

        var title = new Label { Text = "🌱 ReadySetGrow", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 30);
        title.AddThemeColorOverride("font_color", new Color("eaffd8"));
        box.AddChild(title);

        var sub = new Label { Text = "Menu", HorizontalAlignment = HorizontalAlignment.Center };
        sub.AddThemeFontSizeOverride("font_size", 14);
        sub.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.55f));
        box.AddChild(sub);

        box.AddChild(MenuButton("▶  Resume", CloseMenu));
        _autoBtn = MenuButton("🤖 Auto: OFF", ToggleAuto);
        box.AddChild(_autoBtn);
        _restartBtn = MenuButton("🔄 Restart Game", OnRestartPressed);
        box.AddChild(_restartBtn);
        box.AddChild(MenuButton("❌ Quit", () => GetTree().Quit()));

        var hint = new Label { Text = "Esc to resume  ·  F11 fullscreen", HorizontalAlignment = HorizontalAlignment.Center };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.5f));
        box.AddChild(hint);
    }

    private Button MenuButton(string text, System.Action onPressed)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 46), MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        b.AddThemeFontSizeOverride("font_size", 18);
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        b.Pressed += onPressed;
        return b;
    }

    private void ShowToast(string text, Color color)
    {
        _toast.Text = text;
        _toast.AddThemeColorOverride("font_color", color);
        _toast.SelfModulate = new Color(1, 1, 1, 1);
        _toastTime = 2.6f;
    }

    private Button MakeButton(string text, System.Action onPressed)
    {
        var b = new Button { Text = text, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        b.AddThemeFontSizeOverride("font_size", 16);
        b.Pressed += onPressed;
        return b;
    }

    // ---- persistence ------------------------------------------------------

    private void SaveGame()
    {
        var plots = new Godot.Collections.Array();
        foreach (PlotState s in _states)
        {
            plots.Add(new Godot.Collections.Dictionary
            {
                ["seed"] = s.Seed?.Name ?? "",
                ["growth"] = s.Growth,
                ["mutation"] = s.Mutation.Name,
                ["rolled"] = s.MutationRolled,
                ["size"] = s.Size,
                ["slaveOf"] = s.SlaveOf,
            });
        }

        var pets = new Godot.Collections.Array();
        foreach (string n in _ownedPets)
            pets.Add(n);

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var data = new Godot.Collections.Dictionary
        {
            ["coins"] = _coins.ToString("F0", inv),
            ["basketCount"] = _basketCount,
            ["basketValue"] = _basketValue.ToString("F0", inv),
            ["treeUnlocked"] = _treeUnlocked,
            ["groveUnlocked"] = _groveUnlocked,
            ["uniUnlocked"] = _uniUnlocked,
            ["vineBarrierDown"] = _vineBarrierDown,
            ["petsUnlocked"] = _petsUnlocked,
            ["ownedPets"] = pets,
            ["selected"] = _selected,
            ["plots"] = plots,
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

        if (data.ContainsKey("coins"))
            _coins = ParseNum(data["coins"]);
        if (data.ContainsKey("basketCount"))
            _basketCount = (int)data["basketCount"];
        if (data.ContainsKey("basketValue"))
            _basketValue = ParseNum(data["basketValue"]);
        if (data.ContainsKey("treeUnlocked"))
            _treeUnlocked = (bool)data["treeUnlocked"];
        if (data.ContainsKey("groveUnlocked"))
            _groveUnlocked = (bool)data["groveUnlocked"];
        if (data.ContainsKey("uniUnlocked"))
            _uniUnlocked = (bool)data["uniUnlocked"];
        if (data.ContainsKey("vineBarrierDown"))
            _vineBarrierDown = (bool)data["vineBarrierDown"];
        if (data.ContainsKey("petsUnlocked"))
            _petsUnlocked = (bool)data["petsUnlocked"];
        if (data.ContainsKey("ownedPets"))
        {
            foreach (Variant pv in data["ownedPets"].AsGodotArray())
            {
                string nm = pv.AsString();
                if (Catalog.PetByName(nm) is not null)
                    _ownedPets.Add(nm);
            }
        }
        if (data.ContainsKey("selected"))
            _selected = (int)data["selected"]; // clamped in _Ready once the seed list is built

        double savedTime = data.ContainsKey("time") ? (double)data["time"] : Time.GetUnixTimeFromSystem();
        double elapsed = Mathf.Clamp(
            Time.GetUnixTimeFromSystem() - savedTime, 0, Catalog.MaxOfflineSeconds);

        if (!data.ContainsKey("plots"))
            return;

        var plots = data["plots"].AsGodotArray();
        for (int i = 0; i < _states.Length && i < plots.Count; i++)
        {
            var pd = plots[i].AsGodotDictionary();
            PlotState st = _states[i];
            string name = (string)pd["seed"];
            int slaveOf = pd.ContainsKey("slaveOf") ? (int)pd["slaveOf"] : -1;

            if (string.IsNullOrEmpty(name)) { st.Clear(); st.SlaveOf = slaveOf; continue; }

            SeedType? seed = Catalog.SeedByNameAny(name);
            if (seed is null) { st.Clear(); st.SlaveOf = slaveOf; continue; }

            st.Seed = seed;
            st.Growth = (float)(double)pd["growth"];
            st.Mutation = Mutation.ByName((string)pd["mutation"]);
            st.MutationRolled = (bool)pd["rolled"];
            st.Size = pd.ContainsKey("size") ? (float)(double)pd["size"] : 1f;
            st.SlaveOf = slaveOf;

            Grow(st, (float)elapsed);
            if (st.IsReady && !st.MutationRolled)
            {
                st.Mutation = Mutation.Roll(null, st.Seed.Rarity);
                st.MutationRolled = true;
            }
        }

        // Re-establish multi-tile visuals for any loaded footprint-4 master plants.
        for (int i = 0; i < _states.Length; i++)
            if (_states[i].Seed is { Footprint: >= 4 })
                SetupMulti(i);
    }

    // ---- helpers ----------------------------------------------------------

    private static double ParseNum(Variant v) =>
        v.VariantType == Variant.Type.String
            ? double.Parse(v.AsString(), System.Globalization.CultureInfo.InvariantCulture)
            : v.AsDouble();

    private static StandardMaterial3D Mat(Color c, float roughness = 0.95f) =>
        new() { AlbedoColor = c, Roughness = roughness, Metallic = 0f };

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
