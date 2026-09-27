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

	// --- Modo futbol ---
	[Export] public bool ModoFutbol = true;
	[Export] public float ArcoZ = 88f;                        // distancia de los arcos (arena va de -91 a 91)
	[Export] public Vector3 ArcoTam = new Vector3(28, 8, 6);  // ancho, alto, fondo del arco
	[Export] public Vector3 BolaInicio = new Vector3(0, 4, 0);

	private PackedScene[] _carros;
	private string[] _nombres;
	private int _idx = 0;
	private Carro _carro;

	private CamaraChase _cam;
	private Marker3D _inicio;
	private Label _info, _progreso;
	private ProgressBar _turbo;
	private AudioStreamPlayer _sonidoCp;

	// HUD de combate (se crea por codigo, no hay que tocar la escena)
	private ProgressBar _barraVida;
	private StyleBoxFlat _estiloVida;
	private Label _lblMarcador;
	private Label _lblMensaje;
	private double _tMensaje = 0;
	private readonly Dictionary<int, int> _destrucciones = new();

	// Futbol
	private int _golesAzul = 0, _golesRojo = 0;
	private Label _lblGoles;
	private Node3D _bola;
	private double _reintento = 0;

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

		// El HUD y los arcos se crean al final, cuando ya paso el saludo de red,
		// para que un error de escena no impida recibir el carro.
		CrearHudCombate();
		if (ModoFutbol) { CrearArcos(); ActualizarMarcadorGoles(); }
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
		if (!Multiplayer.IsServer() || _jugadores == null) return;

		_jugadoresRed.Add(id);   // HashSet: no duplica

		// (Re)crea y sincroniza TODOS los carros para TODOS. CrearCarro no duplica
		// (revisa si ya existe), asi que repetir es seguro y arregla pedidos perdidos.
		foreach (int a in _jugadoresRed)
		{
			CrearCarro(a);                       // en el servidor
			foreach (int b in _jugadoresRed)
				if (b != 1) RpcId(b, MethodName.CrearCarro, a);   // en cada cliente
		}

		GD.Print($"[SERVIDOR] Jugador {id} registrado. Total: {_jugadoresRed.Count}.");
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
					else if (!Multiplayer.IsServer())
					{
						_reintento -= delta;
						if (_reintento <= 0)
						{
							_reintento = 0.5;
							RpcId(1, MethodName.ServidorJugadorListo);
						}
					}
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

			if (_barraVida != null)
			{
				_barraVida.Value = _carro.Vida;
				if (_estiloVida != null)
				{
					float f = (float)(_carro.Vida / 100.0);
					_estiloVida.BgColor = new Color(1f - f, 0.3f + 0.55f * f, 0.2f);
				}
			}
		}

		if (_lblMensaje != null && _tMensaje > 0)
		{
			_tMensaje -= delta;
			Color c = _lblMensaje.Modulate;
			c.A = (float)Mathf.Clamp(_tMensaje, 0.0, 1.0);
			_lblMensaje.Modulate = c;
		}
	}

	private void CrearHudCombate()
	{
		Node hud = _info != null ? _info.GetParent() : null;
		if (hud == null) return;

		_estiloVida = new StyleBoxFlat();
		_estiloVida.BgColor = new Color(0.15f, 0.85f, 0.25f);
		_estiloVida.SetCornerRadiusAll(6);

		_barraVida = new ProgressBar();
		_barraVida.ShowPercentage = false;
		_barraVida.MinValue = 0;
		_barraVida.MaxValue = 100;
		_barraVida.Value = 100;
		_barraVida.CustomMinimumSize = new Vector2(260, 26);
		_barraVida.Position = new Vector2(20, 90);
		_barraVida.AddThemeStyleboxOverride("fill", _estiloVida);
		hud.AddChild(_barraVida);

		_lblMarcador = new Label();
		_lblMarcador.Position = new Vector2(20, 122);
		_lblMarcador.Text = "Destrucciones: 0";
		_lblMarcador.AddThemeFontSizeOverride("font_size", 20);
		hud.AddChild(_lblMarcador);

		_lblMensaje = new Label();
		_lblMensaje.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_lblMensaje.OffsetTop = 80;
		_lblMensaje.OffsetBottom = 140;
		_lblMensaje.HorizontalAlignment = HorizontalAlignment.Center;
		_lblMensaje.VerticalAlignment = VerticalAlignment.Center;
		_lblMensaje.AddThemeFontSizeOverride("font_size", 36);
		_lblMensaje.Modulate = new Color(1, 1, 1, 0);
		hud.AddChild(_lblMensaje);

		// Marcador de goles arriba al centro.
		_lblGoles = new Label();
		_lblGoles.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_lblGoles.OffsetTop = 8;
		_lblGoles.OffsetBottom = 68;
		_lblGoles.HorizontalAlignment = HorizontalAlignment.Center;
		_lblGoles.AddThemeFontSizeOverride("font_size", 28);
		hud.AddChild(_lblGoles);
	}

	// ================= FUTBOL: arcos, goles y marcador =================

	private void CrearArcos()
	{
		_bola = GetParent().GetNodeOrNull<Node3D>("Obstaculos/Bola");
		CrearUnArco("ArcoNorte", new Vector3(0, ArcoTam.Y / 2f, -ArcoZ), new Color(0.35f, 0.55f, 1f), true);
		CrearUnArco("ArcoSur", new Vector3(0, ArcoTam.Y / 2f, ArcoZ), new Color(1f, 0.4f, 0.4f), false);
	}

	private void CrearUnArco(string nombre, Vector3 pos, Color color, bool norte)
	{
		var area = new Area3D();
		area.Name = nombre;

		var col = new CollisionShape3D();
		col.Shape = new BoxShape3D { Size = ArcoTam };
		area.AddChild(col);

		var mesh = new MeshInstance3D();
		mesh.Mesh = new BoxMesh { Size = ArcoTam };
		var mat = new StandardMaterial3D();
		mat.AlbedoColor = new Color(color.R, color.G, color.B, 0.22f);
		mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
		mesh.MaterialOverride = mat;
		area.AddChild(mesh);

		GetParent().AddChild(area);
		area.GlobalPosition = pos;
		area.BodyEntered += (Node3D cuerpo) => OnBolaEnArco(cuerpo, norte);
	}

	private void OnBolaEnArco(Node cuerpo, bool norte)
	{
		if (_enRed && !Multiplayer.IsServer()) return;   // el servidor decide los goles
		if (cuerpo is not BolaGolpe) return;              // solo la pelota anota
		RegistrarGol(norte);                              // norte = anota Azul
	}

	private void RegistrarGol(bool azulAnota)
	{
		if (azulAnota) _golesAzul++; else _golesRojo++;
		if (_enRed) Rpc(MethodName.MarcadorGol, _golesAzul, _golesRojo, azulAnota);
		else MarcadorGol(_golesAzul, _golesRojo, azulAnota);
		ResetBolaServidor();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	private void MarcadorGol(int azul, int rojo, bool azulAnoto)
	{
		_golesAzul = azul;
		_golesRojo = rojo;
		ActualizarMarcadorGoles();
		Mensaje(azulAnoto ? "¡GOL AZUL!" : "¡GOL ROJO!",
			azulAnoto ? new Color(0.45f, 0.6f, 1f) : new Color(1f, 0.45f, 0.45f));
	}

	private void ResetBolaServidor()
	{
		if (_bola == null || !IsInstanceValid(_bola)) return;
		_bola.GlobalPosition = BolaInicio;
		if (_bola is RigidBody3D rb)
		{
			rb.LinearVelocity = Vector3.Zero;
			rb.AngularVelocity = Vector3.Zero;
		}
	}

	private void ActualizarMarcadorGoles()
	{
		if (_lblGoles == null) return;
		string mvp = CalcularMVP();
		_lblGoles.Text = $"AZUL  {_golesAzul} : {_golesRojo}  ROJO"
			+ (mvp != "" ? $"\nMVP: {mvp}" : "");
	}

	private string CalcularMVP()
	{
		int mejorId = 0, mejor = -1;
		foreach (var kv in _destrucciones)
			if (kv.Value > mejor) { mejor = kv.Value; mejorId = kv.Key; }
		if (mejor <= 0) return "";
		return $"Jugador {mejorId} ({mejor} pts)";
	}

	// Lo llama el carro cuando alguien es destruido (corre en todos los jugadores).
	public void AvisarDemolicion(int idAtacante, int idVictima)
	{
		if (!_destrucciones.ContainsKey(idAtacante)) _destrucciones[idAtacante] = 0;
		_destrucciones[idAtacante]++;

		int yo = _enRed ? Multiplayer.GetUniqueId() : 1;

		if (_lblMarcador != null)
		{
			int mios = _destrucciones.ContainsKey(yo) ? _destrucciones[yo] : 0;
			_lblMarcador.Text = $"Destrucciones: {mios}";
		}

		ActualizarMarcadorGoles();   // refresca el MVP

		if (idVictima == yo)
		{
			Mensaje("¡TE DESTRUYERON!", new Color(1f, 0.35f, 0.35f));
			_cam?.Sacudir(0.4f);
		}
		else if (idAtacante == yo)
		{
			Mensaje("¡DESTRUISTE UN CARRO!", new Color(0.45f, 1f, 0.55f));
			_cam?.Sacudir(0.25f);
		}
		else
		{
			_cam?.Sacudir(0.1f);
		}
	}

	private void Mensaje(string txt, Color color)
	{
		if (_lblMensaje == null) return;
		_lblMensaje.Text = txt;
		color.A = 1f;
		_lblMensaje.Modulate = color;
		_tMensaje = 1.6;
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
