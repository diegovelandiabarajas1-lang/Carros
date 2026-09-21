using Godot;

public partial class Giratorio : AnimatableBody3D
{
    [Export] public float Velocidad = 1.2f;

    private bool _simular = true;
    private bool _enRed = false;

    public override void _Ready()
    {
        SyncToPhysics = true;
        _enRed = Multiplayer.HasMultiplayerPeer();
        if (_enRed)
            _simular = Multiplayer.IsServer();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_simular) return;

        RotateY((float)delta * Velocidad);

        // El servidor le manda el giro a todos los clientes.
        if (_enRed && Multiplayer.GetPeers().Length > 0)
            Rpc(MethodName.SincGiro, Rotation.Y);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void SincGiro(float y)
    {
        Rotation = new Vector3(Rotation.X, y, Rotation.Z);
    }
}
