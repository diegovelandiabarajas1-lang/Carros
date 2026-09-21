using Godot;

public partial class Pausa : CanvasLayer
{
    [Export] public NodePath PanelPath;
    [Export] public NodePath BotonContinuarPath;
    [Export] public NodePath BotonReiniciarPath;
    [Export] public NodePath BotonLobbyPath;

    private Control _panel;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _panel = GetNode<Control>(PanelPath);
        _panel.Visible = false;

        GetNode<Button>(BotonContinuarPath).Pressed += Continuar;
        GetNode<Button>(BotonReiniciarPath).Pressed += Reiniciar;
        GetNode<Button>(BotonLobbyPath).Pressed += AlLobby;
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("pausa"))
            Alternar();
    }

    private void Alternar()
    {
        bool pausar = !GetTree().Paused;
        GetTree().Paused = pausar;
        _panel.Visible = pausar;
    }

    private void Continuar()
    {
        GetTree().Paused = false;
        _panel.Visible = false;
    }

    private void Reiniciar()
    {
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    private void AlLobby()
    {
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile("res://Lobby.tscn");
    }
}
