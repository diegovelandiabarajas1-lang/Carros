using Godot;

public partial class Movil : AnimatableBody3D
{
    [Export] public Vector3 Eje = new Vector3(1, 0, 0);
    [Export] public float Amplitud = 12f;
    [Export] public float Velocidad = 1.0f;

    private Vector3 _base;
    private float _t = 0f;
    private bool _simular = true;
    private bool _enRed = false;
    private float _acum = 0f;
    private Vector3 _objetivo;

    public override void _Ready()
    {
        _base = Position;
        _objetivo = Position;
        SyncToPhysics = true;
        _enRed = Multiplayer.HasMultiplayerPeer();
        if (_enRed)
            _simular = Multiplayer.IsServer();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_simular)
        {
            _t += (float)delta * Velocidad;
            Position = _base + Eje.Normalized() * (Mathf.Sin(_t) * Amplitud);

            // El servidor manda la posicion ~12 veces por segundo (no 60).
            if (_enRed && Multiplayer.GetPeers().Length > 0)
            {
                _acum += (float)delta;
                if (_acum >= 0.08f)
                {
                    _acum = 0f;
                    Rpc(MethodName.SincPos, Position);
                }
            }
        }
        else
        {
            // Cliente: suaviza hacia la ultima posicion recibida.
            Position = Position.Lerp(_objetivo, 1f - Mathf.Exp(-12f * (float)delta));
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void SincPos(Vector3 p)
    {
        _objetivo = p;
    }
}
