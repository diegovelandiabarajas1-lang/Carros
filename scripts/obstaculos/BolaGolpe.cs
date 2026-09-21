using Godot;

public partial class BolaGolpe : RigidBody3D
{
    [Export] public float Fuerza = 1.8f;
    [Export] public float Elevacion = 0.3f;
    [Export] public float MinVelocidad = 2.0f;

    private bool _simular = true;

    public override void _Ready()
    {
        ContactMonitor = true;
        MaxContactsReported = 4;
        BodyEntered += AlChocar;

        if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer())
        {
            _simular = false;
            FreezeMode = FreezeModeEnum.Kinematic;
            Freeze = true;
        }
    }

    private void AlChocar(Node cuerpo)
    {
        if (!_simular) return;
        if (cuerpo is not VehicleBody3D carro) return;

        Vector3 vel = carro is Carro c ? c.VelRed : carro.LinearVelocity;
        float rapidez = vel.Length();
        if (rapidez < MinVelocidad) return;

        Vector3 dir = GlobalPosition - carro.GlobalPosition;
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
