using Godot;
using System.Collections.Generic;

namespace GrowDaGarden;

// LAN co-op: one player hosts a shared world and gets a room code; friends type
// the code to join on the same network. The host is authoritative — it runs the
// whole simulation and broadcasts the world; clients render it, walk around, and
// send plant/harvest/sell/gate requests back to the host.
public partial class Game3D
{
    private enum NetMode { Lobby, Solo, Host, Client }
    private NetMode _net = NetMode.Lobby;

    private bool IsClient => _net == NetMode.Client;
    private bool IsHost => _net == NetMode.Host;
    private bool NetSimActive => _net == NetMode.Solo || _net == NetMode.Host;

    private bool _netWired;
    private double _snapTimer;
    private double _avatarTimer;

    private Control _lobby = null!;
    private LineEdit _codeEntry = null!;
    private Label _lobbyMsg = null!;
    private Label _roomLabel = null!;

    private readonly Dictionary<long, Node3D> _avatars = new();

    // ---- lobby ------------------------------------------------------------

    private void NetStart()
    {
        BuildLobby();
        if (!_netWired)
        {
            Multiplayer.PeerConnected += id => GD.Print($"peer connected {id}");
            Multiplayer.PeerDisconnected += OnPeerLeft;
            Multiplayer.ConnectedToServer += OnJoinedHost;
            Multiplayer.ConnectionFailed += OnJoinFailed;
            _netWired = true;
        }
        EnterLobby();
    }

    private void EnterLobby()
    {
        _net = NetMode.Lobby;
        _lobby.Visible = true;
        _crosshair.Visible = false;
        _prompt.Text = "";
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void BuildLobby()
    {
        _lobby = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        _uiRoot.AddChild(_lobby);
        _lobby.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var dim = new ColorRect { Color = new Color(0.05f, 0.09f, 0.05f, 0.96f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _lobby.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _lobby.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(440, 0) };
        panel.AddThemeStyleboxOverride("panel", Card(new Color(0.14f, 0.20f, 0.12f, 1f), 18));
        center.AddChild(panel);

        var margin = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(m, 24);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        margin.AddChild(box);

        var title = new Label { Text = "🌱 ReadySetGrow", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 30);
        title.AddThemeColorOverride("font_color", new Color("eaffd8"));
        box.AddChild(title);

        var sub = new Label { Text = "Play on your own, or farm together over your local network.", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        sub.AddThemeFontSizeOverride("font_size", 13);
        sub.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.6f));
        box.AddChild(sub);

        box.AddChild(LobbyButton("▶  Play Solo", StartSolo));
        box.AddChild(LobbyButton("🌐  Host a World (LAN)", StartHost));

        var joinRow = new HBoxContainer();
        joinRow.AddThemeConstantOverride("separation", 8);
        box.AddChild(joinRow);
        _codeEntry = new LineEdit { PlaceholderText = "room code", CustomMinimumSize = new Vector2(0, 44) };
        _codeEntry.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _codeEntry.AddThemeFontSizeOverride("font_size", 18);
        joinRow.AddChild(_codeEntry);
        var joinBtn = LobbyButton("Join", () => StartJoin(_codeEntry.Text));
        joinBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        joinRow.AddChild(joinBtn);

        _lobbyMsg = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _lobbyMsg.AddThemeFontSizeOverride("font_size", 14);
        _lobbyMsg.AddThemeColorOverride("font_color", new Color("ffd9a8"));
        _lobbyMsg.CustomMinimumSize = new Vector2(0, 20);
        box.AddChild(_lobbyMsg);

        // Persistent room-code banner (visible while hosting).
        _roomLabel = new Label { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _roomLabel.AddThemeFontSizeOverride("font_size", 16);
        _roomLabel.AddThemeColorOverride("font_color", new Color("9be67a"));
        _roomLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
        _roomLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _roomLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _uiRoot.AddChild(_roomLabel);
        _roomLabel.AnchorLeft = 0; _roomLabel.AnchorRight = 1; _roomLabel.AnchorTop = 1; _roomLabel.AnchorBottom = 1;
        _roomLabel.OffsetTop = -52; _roomLabel.OffsetBottom = -34; _roomLabel.OffsetLeft = 18;
    }

    private Button LobbyButton(string text, System.Action pressed)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 46), MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        b.AddThemeFontSizeOverride("font_size", 18);
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        b.Pressed += pressed;
        return b;
    }

    private void ExitLobby(string toast, Color color)
    {
        _lobby.Visible = false;
        SetWalkMode(true);
        ShowToast(toast, color);
    }

    // ---- start host / join / solo -----------------------------------------

    private void StartSolo()
    {
        _net = NetMode.Solo;
        ExitLobby("🌱 Welcome to ReadySetGrow! Open the gate, walk in, and press E to plant.", new Color("d9f7a6"));
    }

    private void StartHost()
    {
        var peer = new ENetMultiplayerPeer();
        Error err = peer.CreateServer(Net.Port, 7);
        if (err != Error.Ok)
        {
            _lobbyMsg.Text = "Couldn't host (port busy?). Try again.";
            return;
        }
        Multiplayer.MultiplayerPeer = peer;
        _net = NetMode.Host;

        string code = Net.Encode(Net.LocalIp());
        _roomLabel.Text = $"🌐 Hosting — Room code: {code}   (friends type this to join)";
        _roomLabel.Visible = true;
        ExitLobby($"🌐 Hosting! Room code: {code}", new Color("9be67a"));
    }

    private void StartJoin(string codeText)
    {
        string ip = Net.Decode(codeText);
        if (ip.Length == 0)
        {
            _lobbyMsg.Text = "That code doesn't look right.";
            return;
        }
        var peer = new ENetMultiplayerPeer();
        Error err = peer.CreateClient(ip, Net.Port);
        if (err != Error.Ok)
        {
            _lobbyMsg.Text = "Couldn't start connecting. Check the code.";
            return;
        }
        Multiplayer.MultiplayerPeer = peer;
        _lobbyMsg.Text = $"Connecting to {ip}…";
    }

    private void OnJoinedHost()
    {
        _net = NetMode.Client;
        ExitLobby("🌐 Joined the world! Farm together.", new Color("9be67a"));
    }

    private void OnJoinFailed()
    {
        Multiplayer.MultiplayerPeer = null;
        _lobbyMsg.Text = "Couldn't reach that world. Is the host on and on the same network?";
    }

    private void OnPeerLeft(long id)
    {
        if (_avatars.TryGetValue(id, out Node3D? a)) { a.QueueFree(); _avatars.Remove(id); }
    }

    // ---- per-frame networking ---------------------------------------------

    private void NetProcess(double delta)
    {
        if (_net != NetMode.Host && _net != NetMode.Client)
            return;

        _avatarTimer += delta;
        if (_avatarTimer >= 0.05)
        {
            _avatarTimer = 0;
            Rpc("SyncAvatar", Multiplayer.GetUniqueId(), _player.GlobalPosition, _player.Rotation.Y);
        }

        if (_net == NetMode.Host)
        {
            _snapTimer += delta;
            if (_snapTimer >= 0.12)
            {
                _snapTimer = 0;
                Rpc("ApplySnapshotRpc", BuildSnapshot());
            }
        }
    }

    private void UpsertAvatar(long id, Vector3 pos, float yaw)
    {
        if (id == Multiplayer.GetUniqueId())
            return;   // never show our own avatar
        if (!_avatars.TryGetValue(id, out Node3D? a))
        {
            a = new Node3D();
            AddChild(a);
            var col = Color.FromHsv((id * 0.137f) % 1f, 0.6f, 0.95f);
            var mat = new StandardMaterial3D { AlbedoColor = col, Roughness = 0.6f };
            a.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.7f }, Position = new Vector3(0, 0.85f, 0), MaterialOverride = mat });
            a.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f }, Position = new Vector3(0, 1.55f, 0.22f), MaterialOverride = mat });
            a.AddChild(new Label3D { Text = "Friend", Position = new Vector3(0, 2.1f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 48, PixelSize = 0.005f, OutlineSize = 12, Modulate = col, OutlineModulate = new Color(0, 0, 0, 0.85f) });
            _avatars[id] = a;
        }
        a.GlobalPosition = pos;
        a.Rotation = new Vector3(0, yaw, 0);
    }

    // ---- RPCs -------------------------------------------------------------

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    public void SyncAvatar(long id, Vector3 pos, float yaw)
    {
        UpsertAvatar(id, pos, yaw);
        if (IsHost)
            Rpc("SyncAvatar", id, pos, yaw);   // relay so clients see each other too
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    public void ReqUsePlot(int plotIndex, int seedIndex)
    {
        if (!IsHost || plotIndex < 0 || plotIndex >= _plots.Length) return;
        if (seedIndex < 0 || seedIndex >= _activeSeeds.Count) return;
        DoUsePlot(plotIndex, seedIndex);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    public void ReqHarvestAll() { if (IsHost) DoHarvestAll(); }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    public void ReqSell() { if (IsHost) DoSell(); }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    public void ReqGate() { if (IsHost) DoToggleGate(); }

    // ---- world snapshot (host -> clients) ---------------------------------

    private Godot.Collections.Dictionary BuildSnapshot()
    {
        var plots = new Godot.Collections.Array();
        foreach (PlotState s in _states)
        {
            var muts = new Godot.Collections.Array();
            foreach (Mutation m in s.Mutations) muts.Add(m.Name);
            plots.Add(new Godot.Collections.Dictionary
            {
                ["seed"] = s.Seed?.Name ?? "",
                ["growth"] = s.Growth,
                ["size"] = s.Size,
                ["slaveOf"] = s.SlaveOf,
                ["muts"] = muts,
            });
        }
        var won = new Godot.Collections.Array();
        foreach (int w in _packWon) won.Add(w);

        return new Godot.Collections.Dictionary
        {
            ["coins"] = _coins,
            ["basketCount"] = _basketCount,
            ["basketValue"] = _basketValue,
            ["event"] = _activeEvent,
            ["eventLeft"] = _eventRemaining,
            ["gateOpen"] = _gateOpen,
            ["tree"] = _treeUnlocked,
            ["grove"] = _groveUnlocked,
            ["uni"] = _uniUnlocked,
            ["barrier"] = _vineBarrierDown,
            ["pets"] = _petsUnlocked,
            ["packGone"] = _packGone,
            ["gifts"] = _giftsLeft,
            ["won"] = won,
            ["plots"] = plots,
        };
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void ApplySnapshotRpc(Godot.Collections.Dictionary d)
    {
        if (!IsClient) return;
        ApplySnapshot(d);
    }

    private void ApplySnapshot(Godot.Collections.Dictionary d)
    {
        _coins = (double)d["coins"];
        _basketCount = (int)d["basketCount"];
        _basketValue = (double)d["basketValue"];
        _activeEvent = (string)d["event"];
        _eventRemaining = (double)d["eventLeft"];
        _gateOpen = (bool)d["gateOpen"];
        _gateTargetAngle = _gateOpen ? -Mathf.Pi / 2f : 0f;
        _treeUnlocked = (bool)d["tree"];
        _groveUnlocked = (bool)d["grove"];
        _uniUnlocked = (bool)d["uni"];
        _petsUnlocked = (bool)d["pets"];
        _packGone = (bool)d["packGone"];
        _giftsLeft = (int)d["gifts"];

        _packWon.Clear();
        foreach (Variant w in d["won"].AsGodotArray())
            _packWon.Add((int)w);

        var plots = d["plots"].AsGodotArray();
        for (int i = 0; i < _states.Length && i < plots.Count; i++)
        {
            var pd = plots[i].AsGodotDictionary();
            PlotState st = _states[i];
            string name = (string)pd["seed"];
            st.Clear();
            st.SlaveOf = (int)pd["slaveOf"];
            if (!string.IsNullOrEmpty(name))
            {
                SeedType? seed = Catalog.SeedByNameAny(name);
                if (seed is not null)
                {
                    st.Seed = seed;
                    st.Growth = (float)(double)pd["growth"];
                    st.Size = (float)(double)pd["size"];
                    foreach (Variant mv in pd["muts"].AsGodotArray())
                        st.AddMutation(Mutation.ByName(mv.AsString()));
                }
            }
            _plots[i].PlantOffset = Vector3.Zero;
            _plots[i].ExtraScale = 1f;
        }
        for (int i = 0; i < _states.Length; i++)
            if (_states[i].Seed is { Footprint: >= 2 } ms)
                SetupMulti(i, ms.Footprint);
        foreach (Plot3D p in _plots) p.Refresh(0f);

        SetTreeGlow(_treeUnlocked);
        SetGroveGlow(_groveUnlocked);
        SetUniGlow(_uniUnlocked);
        SetWeatherVisuals(_activeEvent);
        _vineBarrierDown = (bool)d["barrier"];
        if (_vineBarrierDown && _vineBarrier is not null) DropBarrier();
        if (_packGone && _packStall is not null) { _packStall.QueueFree(); _packStall = null; }
        if (!_packGone && _packStall is null && _giftsLeft > 0 && _coins >= Catalog.PackCost) BuildPackStall();

        _lastShopCoins = -1;   // force shop greying/text refresh next frame
    }
}
