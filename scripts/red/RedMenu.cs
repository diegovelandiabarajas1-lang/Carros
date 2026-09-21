using Godot;

public partial class RedMenu : Control
{
    const int PUERTO = 8915;

    [Export] public NodePath CampoDireccionPath;
    [Export] public NodePath EstadoPath;
    [Export] public NodePath BotonHostearPath;
    [Export] public NodePath BotonUnirsePath;
    [Export] public NodePath BotonVolverPath;
    [Export] public NodePath CajaHostPath;
    [Export] public NodePath BotonArenaPath;
    [Export] public NodePath BotonPistaPath;
    [Export] public NodePath BotonEmpezarPath;
    [Export] public NodePath EtiquetaMapaPath;

    private LineEdit _dir;
    private Label _estado, _etqMapa;
    private Control _cajaHost;
    private string _escena = "res://Arena.tscn";

    public override void _Ready()
    {
        _dir = GetNode<LineEdit>(CampoDireccionPath);
        _estado = GetNode<Label>(EstadoPath);
        _cajaHost = GetNode<Control>(CajaHostPath);
        _etqMapa = GetNode<Label>(EtiquetaMapaPath);

        GetNode<Button>(BotonHostearPath).Pressed += AlHostear;
        GetNode<Button>(BotonUnirsePath).Pressed += AlUnirse;
        GetNode<Button>(BotonVolverPath).Pressed += AlVolver;
        GetNode<Button>(BotonArenaPath).Pressed += () => ElegirMapa("res://Arena.tscn", "Arena");
        GetNode<Button>(BotonPistaPath).Pressed += () => ElegirMapa("res://Main.tscn", "Pista");
        GetNode<Button>(BotonEmpezarPath).Pressed += AlEmpezar;

        _cajaHost.Visible = false;

        Multiplayer.PeerConnected += (long id) =>
            Estado($"Se conecto un jugador. En total: {Multiplayer.GetPeers().Length + 1}.");
        Multiplayer.PeerDisconnected += (long id) => Estado("Un jugador se desconecto.");
        Multiplayer.ConnectedToServer += () => Estado("¡Conectado al host! Esperando que el host inicie...");
        Multiplayer.ConnectionFailed += () => Estado("No se pudo conectar. Revisa la direccion y el host.");
        Multiplayer.ServerDisconnected += () => Estado("El host cerro la partida.");

        Estado("Sin conexion. Hostea o unete a una partida.");
    }

    private void Estado(string t) { if (_estado != null) _estado.Text = t; }

    private void AlHostear()
    {
        var peer = new WebSocketMultiplayerPeer();
        Error err = peer.CreateServer(PUERTO);
        if (err != Error.Ok) { Estado("Error al crear el servidor: " + err); return; }
        Multiplayer.MultiplayerPeer = peer;
        _cajaHost.Visible = true;
        ElegirMapa(_escena, _escena.Contains("Arena") ? "Arena" : "Pista");
        Estado($"Eres el HOST (puerto {PUERTO}). Elige mapa y dale Empezar cuando todos esten.");
    }

    private void AlUnirse()
    {
        string entrada = (_dir != null) ? _dir.Text.Trim() : "";
        if (string.IsNullOrWhiteSpace(entrada)) entrada = "127.0.0.1";
        string url = (entrada.StartsWith("ws://") || entrada.StartsWith("wss://"))
            ? entrada : $"ws://{entrada}:{PUERTO}";

        var peer = new WebSocketMultiplayerPeer();
        Error err = peer.CreateClient(url);
        if (err != Error.Ok) { Estado("Error al conectar: " + err); return; }
        Multiplayer.MultiplayerPeer = peer;
        _cajaHost.Visible = false;
        Estado($"Conectando a {url}...");
    }

    private void ElegirMapa(string escena, string nombre)
    {
        _escena = escena;
        if (_etqMapa != null) _etqMapa.Text = "Mapa: " + nombre;
    }

    private void AlEmpezar()
    {
        if (!Multiplayer.IsServer()) return;
        Rpc(MethodName.IniciarPartida, _escena);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
    private void IniciarPartida(string escena)
    {
        GetTree().ChangeSceneToFile(escena);
    }

    private void AlVolver()
    {
        if (Multiplayer.MultiplayerPeer != null)
            Multiplayer.MultiplayerPeer = null;
        GetTree().ChangeSceneToFile("res://Lobby.tscn");
    }
}
