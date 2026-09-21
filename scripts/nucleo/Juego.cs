using Godot;
using System.Collections.Generic;

public partial class Juego : Node3D
{
	[Export] public PackedScene CarroVortice;
	[Export] public PackedScene CarroDominus;
	[Export] public PackedScene CarroStormblade;
	[Export] public NodePath PuntoInicioPath;
	[Export] public NodePath CamaraPath;
	[Export] public NodePath LabelInfoPath;
	[Export] public NodePath LabelProgresoPath;
	[Export] public NodePath BarraTurboPath;
	[Export] public NodePath SpawnerPath;
	[Export] public NodePath JugadoresPath;

	private PackedScene[] _carros;
	private string[] _nombres;
	private int _idx = 0;
	private Carro _carro;

	private CamaraChase _cam;
	private Marker3D _inicio;
	private Label _info, _progreso;
	private ProgressBar _turbo;
	private AudioStreamPlayer _sonidoCp;

	private int _totalCp = 0;
	private int _siguiente = 0;
	private int _vuelta = 0;

	private bool _enRed = false;
	private Node3D _jugadores;
	private bool _suscritoRed = false;
	private readonly HashSet<int> _jugadoresRed = new();

	public override void _Ready()
	{
		AddToGroup("juego");

		var lista = new List<PackedScene>();
		var nombres = new List<string>();
		if (CarroVortice != null) { lista.Add(CarroVortice); nombres.Add("Vortice"); }
		if (CarroDominus != null) { lista.Add(CarroDominus); nombres.Add("Dominus"); }
		if (CarroStormblade != null) { lista.Add(CarroStormblade); nombres.Add("Stormblade"); }
		_carros = lista.ToArray();
		_nombres = nombres.ToArray();

		_cam = GetNode<CamaraChase>(CamaraPath);
		_inicio = GetNode<Marker3D>(PuntoInicioPath);
		_info = GetNode<Label>(LabelInfoPath);
		_progreso = GetNode<Label>(LabelProgresoPath);
		_turbo = GetNodeOrNull<ProgressBar>(BarraTurboPath);

		_sonidoCp = new AudioStreamPlayer();
		AddChild(_sonidoCp);
		_sonidoCp.Stream = GD.Load<AudioStream>("res://sonidos/checkpoint.wav");

		_totalCp = GetTree().GetNodesInGroup("checkpoint").Count;

		_enRed = Multiplayer.HasMultiplayerPeer();
		if (_enRed)
		{
			_jugadores = GetNode<Node3D>(JugadoresPath);

			if (Multiplayer.IsServer())
			{
				Multiplayer.PeerDisconnected += OnPeerDisconnected;
				_suscritoRed = true;

				bool dedicado = Red.Instancia != null && Red.Instancia.EsDedicado;
				if (!dedicado)
					RegistrarJugador(1);
			}
			else
			{
				RpcId(1, MethodName.ServidorJugadorListo);
			}
		}
		else
		{
			int elegido = Mathf.Clamp(Estado.Carro, 0, _carros.Length - 1);
			AparecerCarro(elegido);
		}

		ActualizarProgreso();
	}

	private void AparecerCarro(int idx)
	{
		Transform3D t = _inicio.GlobalTransform;
		if (_carro != null && IsInstanceValid(_carro))
		{
			t = _carro.GlobalTransform;
			_carro.QueueFree();
		}
		_idx = idx;
		_carro = _carros[idx].Instantiate<Carro>();
		AddChild(_carro);
		_carro.GlobalTransform = t;
		_carro.LinearVelocity = Vector3.Zero;
		_carro.AngularVelocity = Vector3.Zero;
		_cam.Objetivo = _carro;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void ServidorJugadorListo()
	{
		if (!Multiplayer.IsServer()) return;
		RegistrarJugador(Multiplayer.GetRemoteSenderId());
	}

	private void RegistrarJugador(int id)
	{
		if (!Multiplayer.IsServer()) return;
		if (_jugadoresRed.Contains(id)) return;

		foreach (int y in _jugadoresRed)
			EnviarCrear(id, y);

		_jugadoresRed.Add(id);

		CrearCarro(id);
		foreach (int z in _jugadoresRed)
			if (z != 1) EnviarCrear(z, id);

		GD.Print($"[SERVIDOR] Jugador {id} registrado. Total con carro: {_jugadoresRed.Count}.");
	}

	private void EnviarCrear(int destino, int idCarro)
	{
		if (destino == 1) CrearCarro(idCarro);
		else RpcId(destino, MethodName.CrearCarro, idCarro);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void CrearCarro(int id)
	{
		if (_jugadores == null) return;
		if (_jugadores.HasNode(id.ToString())) return;

		var carro = CarroVortice.Instantiate<Carro>();
		carro.Name = id.ToString();
		int i = System.Math.Abs(id) % 6;
		carro.Position = _inicio.GlobalPosition + new Vector3(i * 4 - 6, 0.5f, 0);
		_jugadores.AddChild(carro);
	}

	private void OnPeerDisconnected(long id)
	{
		if (!Multiplayer.IsServer()) return;
		_jugadoresRed.Remove((int)id);
		QuitarCarro((int)id);
		foreach (int z in _jugadoresRed)
			if (z != 1) RpcId(z, MethodName.QuitarCarro, (int)id);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void QuitarCarro(int id)
	{
		var n = _jugadores?.GetNodeOrNull(id.ToString());
		if (n != null) n.QueueFree();
	}

	public override void _ExitTree()
	{
		if (_suscritoRed)
		{
			Multiplayer.PeerDisconnected -= OnPeerDisconnected;
			_suscritoRed = false;
		}
	}

	private void ReiniciarLocal()
	{
		if (_carro == null || !IsInstanceValid(_carro)) return;
		_carro.LinearVelocity = Vector3.Zero;
		_carro.AngularVelocity = Vector3.Zero;
		_carro.GlobalTransform = _inicio.GlobalTransform;
		_siguiente = 0;
		ActualizarProgreso();
	}

	public override void _Process(double delta)
	{
		if (_enRed)
		{
			if ((_carro == null || !IsInstanceValid(_carro)) && _jugadores != null)
			{
				var mio = _jugadores.GetNodeOrNull<Carro>(Multiplayer.GetUniqueId().ToString());
				if (mio != null) { _carro = mio; _cam.Objetivo = mio; }
			}
			if (Input.IsActionJustPressed("reiniciar")) ReiniciarLocal();
		}
		else
		{
			if (Input.IsActionJustPressed("cambiar_carro"))
				AparecerCarro((_idx + 1) % _carros.Length);
			if (Input.IsActionJustPressed("reiniciar"))
				ReiniciarLocal();
		}

		if (_carro != null && IsInstanceValid(_carro))
		{
			if (_info != null)
			{
				float kmh = _carro.LinearVelocity.Length() * 3.6f;
				string nombre = (_idx >= 0 && _idx < _nombres.Length) ? _nombres[_idx] : "Carro";
				_info.Text = $"{nombre}\n{kmh,4:0} km/h";
			}
			if (_turbo != null)
				_turbo.Value = _carro.Boost * 100.0;
		}
	}

	public void PasarCheckpoint(int indice, Node3D cuerpo)
	{
		if (_carro == null || cuerpo != _carro) return;
		if (indice != _siguiente) return;

		_siguiente++;
		_sonidoCp?.Play();
		if (_siguiente >= _totalCp)
		{
			_siguiente = 0;
			_vuelta++;
		}
		ActualizarProgreso();
	}

	private void ActualizarProgreso()
	{
		if (_progreso == null) return;
		if (_totalCp == 0)
			_progreso.Text = _enRed ? "DERBY EN RED\n¡A chocar!" : "MODO DERBY\n¡A chocar todo!";
		else
			_progreso.Text = $"Vuelta {_vuelta}\nCheckpoint {_siguiente}/{_totalCp}";
	}
}
