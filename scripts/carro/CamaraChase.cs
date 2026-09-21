using Godot;

public partial class CamaraChase : Camera3D
{
    [Export] public Vector3 Offset = new Vector3(0, 3.0f, 7.0f);
    [Export] public float Suavizado = 6.0f;
    [Export] public float AlturaMira = 1.0f;
    [Export] public float FovBase = 70.0f;
    [Export] public float FovMax = 92.0f;

    public Node3D Objetivo;

    public override void _PhysicsProcess(double delta)
    {
        if (Objetivo == null || !IsInstanceValid(Objetivo)) return;

        float vel = 0f;
        if (Objetivo is VehicleBody3D vb) vel = vb.LinearVelocity.Length();
        float f = Mathf.Clamp(vel / 40f, 0f, 1f);

        Basis b = Objetivo.GlobalTransform.Basis;
        Vector3 off = Offset + new Vector3(0, 0, f * 2.0f);
        Vector3 deseada = Objetivo.GlobalPosition + b * off;

        float t = 1.0f - Mathf.Exp(-Suavizado * (float)delta);
        GlobalPosition = GlobalPosition.Lerp(deseada, t);
        Fov = Mathf.Lerp(Fov, FovBase + (FovMax - FovBase) * f, t);

        LookAt(Objetivo.GlobalPosition + Vector3.Up * AlturaMira, Vector3.Up);
    }
}
