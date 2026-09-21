using Godot;

public partial class Red : Node
{
	public const int PuertoDefecto = 8915;
	public static Red Instancia { get; private set; }
	public bool EsDedicado { get; private set; } = false;

	private double _relojLatido = 0;
	private double _desdeUltimoLatido = 0;
	private bool _enJuegoRed = false;

	public override void _Ready()
	{
		Instancia = this;

		Multiplayer.ServerDisconnected += AlCaerServidor;

		if (!DetectarServidor()) return;

		EsDedicado = true;
		int puerto = LeerPuerto();

		var peer = new WebSocketMultiplayerPeer();
		Error err = peer.CreateServer(puerto, "*");
		if (err != Error.Ok)
		{
			GD.PrintErr($"[SERVIDOR] No pude abrir el puerto {puerto}: {err}");
			GetTree().Quit(1);
			return;
		}
		Multiplayer.MultiplayerPeer = peer;
		GD.Print($"[SERVIDOR] Escuchando WebSocket en el puerto {puerto}. Esperando jugadores...");

		Multiplayer.PeerConnected += CuandoEntraJugador;
		GetTree().ChangeSceneToFile("res://Arena.tscn");
	}

	private bool DetectarServidor()
	{
		if (OS.HasFeature("dedicated_server")) return true;
		foreach (var a in OS.GetCmdlineArgs())
			if (a == "--server" || a == "server") return true;
		return false;
	}

	private int LeerPuerto()
	{
		string env = OS.GetEnvironment("PORT");
		if (!string.IsNullOrEmpty(env) && int.TryParse(env, out int p)) return p;
		return PuertoDefecto;
	}

	private void CuandoEntraJugador(long id)
	{
		GD.Print($"[SERVIDOR] Jugador {id} conectado. Le pido cargar la Arena.");
		RpcId(id, MethodName.CargarEscena, "res://Arena.tscn");
	}

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void CargarEscena(string escena)
	{
		GD.Print($"[CLIENTE] El servidor me pidio cargar {escena}.");
		GetTree().ChangeSceneToFile(escena);
	}

	public override void _Process(double delta)
	{
		if (!Multiplayer.HasMultiplayerPeer()) { _enJuegoRed = false; return; }

		if (Multiplayer.IsServer())
		{
			_relojLatido += delta;
			if (_relojLatido >= 1.0) { _relojLatido = 0; Rpc(MethodName.Latido); }
		}
		else
		{
			_desdeUltimoLatido += delta;
			if (_enJuegoRed && _desdeUltimoLatido > 4.0) AlCaerServidor();
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void Latido()
	{
		_desdeUltimoLatido = 0;
		_enJuegoRed = true;
	}

	private void AlCaerServidor()
	{
		if (EsDedicado) return;
		if (Multiplayer.MultiplayerPeer == null) return;
		GD.Print("[CLIENTE] Se perdio la conexion con el servidor. Vuelvo al lobby.");
		Multiplayer.MultiplayerPeer = null;
		_enJuegoRed = false;
		GetTree().ChangeSceneToFile("res://Lobby.tscn");
	}
}
