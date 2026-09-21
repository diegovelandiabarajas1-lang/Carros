using Godot;

public partial class Lobby : Node3D
{
	[Export] public NodePath VitrinaPath;
	[Export] public NodePath CarroVorticePath;
	[Export] public NodePath CarroDominusPath;
	[Export] public NodePath CarroStormbladePath;
	[Export] public NodePath BotonJugarPath;
	[Export] public NodePath BotonVorticePath;
	[Export] public NodePath BotonDominusPath;
	[Export] public NodePath BotonStormbladePath;
	[Export] public NodePath BotonMultijugadorPath;
	[Export] public float VelocidadGiro = 0.5f;

	private Node3D _vitrina;
	private Node3D[] _carros;
	private Button _jugar;
	private AudioStreamPlayer _click;
	private bool _cayendo = false;
	private float _vel = 0f, _t = 0f;

	public override void _Ready()
	{
		bool dedicado = Red.Instancia != null && Red.Instancia.EsDedicado;
		if (!dedicado)
		{
			var conexion = Multiplayer.MultiplayerPeer;
			if (conexion != null)
			{
				conexion.Close();
				Multiplayer.MultiplayerPeer = null;
			}
		}

		_vitrina = GetNode<Node3D>(VitrinaPath);
		_carros = new[]
		{
			GetNodeOrNull<Node3D>(CarroVorticePath),
			GetNodeOrNull<Node3D>(CarroDominusPath),
			GetNodeOrNull<Node3D>(CarroStormbladePath),
		};
		_jugar = GetNode<Button>(BotonJugarPath);
		_jugar.Pressed += AlJugar;
		GetNode<Button>(BotonVorticePath).Pressed += () => Elegir(0);
		GetNode<Button>(BotonDominusPath).Pressed += () => Elegir(1);
		GetNode<Button>(BotonStormbladePath).Pressed += () => Elegir(2);
		var bm = GetNodeOrNull<Button>(BotonMultijugadorPath);
		if (bm != null) bm.Pressed += () => GetTree().ChangeSceneToFile("res://RedMenu.tscn");

		_click = new AudioStreamPlayer();
		AddChild(_click);
		_click.Stream = GD.Load<AudioStream>("res://sonidos/click.wav");

		MostrarSeleccion(Estado.Carro);
	}

	private void MostrarSeleccion(int i)
	{
		Estado.Carro = i;
		for (int k = 0; k < _carros.Length; k++)
			if (_carros[k] != null) _carros[k].Visible = (k == i);
	}

	private void Elegir(int i)
	{
		MostrarSeleccion(i);
		_click?.Play();
	}

	public override void _Process(double delta)
	{
		float d = (float)delta;

		if (!_cayendo)
		{
			if (_vitrina != null) _vitrina.RotateY(d * VelocidadGiro);
			return;
		}

		_vel += 30f * d;
		_vitrina.Position += Vector3.Down * _vel * d;
		_vitrina.RotateX(d * 2.2f);
		_vitrina.RotateY(d * 1.5f);

		_t += d;
		if (_t > 1.2f)
			GetTree().ChangeSceneToFile("res://Arena.tscn");
	}

	private void AlJugar()
	{
		if (_cayendo) return;
		_click?.Play();
		_cayendo = true;
		_jugar.Disabled = true;
		_jugar.Visible = false;
	}
}
