using Godot;

public partial class Movil : AnimatableBody3D
{
    [Export] public Vector3 Eje = new Vector3(1, 0, 0);
    [Export] public float Amplitud = 12f;
    [Export] public float Velocidad = 1.0f;

    private Vector3 _base;
    private float _t = 0f;
    private bool _simular = true;

    public override void _Ready()
    {
        _base = Position;
        SyncToPhysics = true;
        if (Multiplayer.HasMultiplayerPeer())
            _simular = Multiplayer.IsServer();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_simular) return;
        _t += (float)delta * Velocidad;
        Position = _base + Eje.Normalized() * (Mathf.Sin(_t) * Amplitud);
    }
}
