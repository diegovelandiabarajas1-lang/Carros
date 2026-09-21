using Godot;

public partial class BolaGolpe : RigidBody3D
{
    [Export] public float Fuerza = 1.8f;
    [Export] public float Elevacion = 0.3f;
    [Export] public float MinVelocidad = 2.0f;

    private bool _servidor = true;
    private bool _enRed = false;

    public override void _Ready()
    {
        ContactMonitor = true;
        MaxContactsReported = 8;
        BodyEntered += AlChocar;

        _enRed = Multiplayer.HasMultiplayerPeer();
        _servidor = !_enRed || Multiplayer.IsServer();

        if (_enRed && !_servidor)
        {
            FreezeMode = FreezeModeEnum.Kinematic;
            Freeze = true;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // El servidor le manda la posicion de la bola a todos los clientes.
        if (_enRed && _servidor && Multiplayer.GetPeers().Length > 0)
            Rpc(MethodName.SincBola, GlobalPosition, Rotation);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void SincBola(Vector3 pos, Vector3 rot)
    {
        GlobalPosition = pos;
        Rotation = rot;
    }

    private void AlChocar(Node cuerpo)
    {
        if (cuerpo is not VehicleBody3D carro) return;

        Vector3 vel = carro is Carro c ? c.VelRed : carro.LinearVelocity;
        if (vel.Length() < MinVelocidad) return;

        if (!_enRed)
        {
            AplicarGolpe(carro.GlobalPosition, vel);
            return;
        }

        // Online: solo el dueno del carro reporta su propio golpe al servidor.
        if (!carro.IsMultiplayerAuthority()) return;
        RpcId(1, MethodName.GolpeRed, carro.GlobalPosition, vel);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void GolpeRed(Vector3 posCarro, Vector3 vel)
    {
        if (!Multiplayer.IsServer()) return;
        AplicarGolpe(posCarro, vel);
    }

    private void AplicarGolpe(Vector3 posCarro, Vector3 vel)
    {
        float rapidez = vel.Length();
        if (rapidez < MinVelocidad) return;

        Vector3 dir = GlobalPosition - posCarro;
        dir.Y = 0f;
        if (dir.Length() < 0.01f) dir = new Vector3(vel.X, 0, vel.Z);
        dir = dir.Normalized();
        dir.Y = Elevacion;
        dir = dir.Normalized();

        ApplyCentralImpulse(dir * rapidez * Fuerza * Mass);

        Vector3 lateral = new Vector3(vel.Z, 0, -vel.X).Normalized();
        ApplyTorqueImpulse(lateral * rapidez * 0.5f * Mass);
    }
}
