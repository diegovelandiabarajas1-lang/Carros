using Godot;

public partial class Giratorio : AnimatableBody3D
{
    [Export] public float Velocidad = 1.2f;

    private bool _simular = true;

    public override void _Ready()
    {
        SyncToPhysics = true;
        if (Multiplayer.HasMultiplayerPeer())
            _simular = Multiplayer.IsServer();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_simular) return;
        RotateY((float)delta * Velocidad);
    }
}
