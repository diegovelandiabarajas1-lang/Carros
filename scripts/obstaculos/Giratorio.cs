using Godot;

public partial class Giratorio : AnimatableBody3D
{
    [Export] public float Velocidad = 1.2f;

    private bool _simular = true;
    private bool _enRed = false;
    private float _acum = 0f;
    private float _objY = 0f;

    public override void _Ready()
    {
        SyncToPhysics = true;
        _objY = Rotation.Y;
        _enRed = Multiplayer.HasMultiplayerPeer();
        if (_enRed)
            _simular = Multiplayer.IsServer();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_simular)
        {
            RotateY((float)delta * Velocidad);

            // El servidor manda el giro ~12 veces por segundo (no 60).
            if (_enRed && Multiplayer.GetPeers().Length > 0)
            {
                _acum += (float)delta;
                if (_acum >= 0.08f)
                {
                    _acum = 0f;
                    Rpc(MethodName.SincGiro, Rotation.Y);
                }
            }
        }
        else
        {
            // Cliente: suaviza el giro hacia el ultimo recibido.
            float ry = Mathf.LerpAngle(Rotation.Y, _objY, 1f - Mathf.Exp(-12f * (float)delta));
            Rotation = new Vector3(Rotation.X, ry, Rotation.Z);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void SincGiro(float y)
    {
        _objY = y;
    }
}
