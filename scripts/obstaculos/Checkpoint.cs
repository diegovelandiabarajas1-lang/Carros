using Godot;

public partial class Checkpoint : Area3D
{
    [Export] public int Indice = 0;

    public override void _Ready()
    {
        AddToGroup("checkpoint");
        BodyEntered += AlEntrar;
    }

    private void AlEntrar(Node3D cuerpo)
    {
        if (!cuerpo.IsInGroup("carro")) return;
        var juego = GetTree().GetFirstNodeInGroup("juego") as Juego;
        juego?.PasarCheckpoint(Indice, cuerpo);
    }
}
