using Godot;

public partial class Carro : VehicleBody3D
{
    [Export] public float MotorFuerza = 1000f;
    [Export] public float TurboFuerza = 1600f;
    [Export] public float FrenoFuerza = 14f;
    [Export] public float GiroMax = 0.6f;
    [Export] public float GiroVelocidad = 4.0f;
    [Export] public float AgarreNormal = 3.5f;
    [Export] public float AgarreDerrape = 2.6f;

    [Export] public float SaltoVel = 7.0f;
    [Export] public float DashVel = 11.0f;
    [Export] public float DashElevacion = 4.0f;

    [Export] public float AnguloPitch = -5.0f;
    [Export] public float AnguloYaw = 45.0f;
    [Export] public float EnergiaGiro = 20.0f;
    [Export] public float GiroKp = 25.0f;
    [Export] public float GiroKd = 9.0f;
    [Export] public float GiroTorqueMax = 40.0f;

    [Export] public float TurboAireFuerza = 30.0f;
    [Export] public float AirePitchFuerza = 8.0f;
    [Export] public float AireYawFuerza = 8.0f;
    [Export] public float AireAmortiguacion = 2.5f;

    [Export] public float AgacheProfundidad = 0.15f;
    [Export] public float AgacheDuracion = 0.06f;

    public float Boost { get; private set; } = 1f;
    public bool UsandoTurbo { get; private set; } = false;

    public Vector3 VelRed = Vector3.Zero;
    private Vector3 _posAnterior = Vector3.Zero;
    private bool _posInicializada = false;

    private float _giroActual = 0f;
    private VehicleWheel3D _delanteraIzq, _delanteraDer, _traseraIzq, _traseraDer;
    private AudioStreamPlayer _motor, _turbo;
    private bool _turboSonaba = false;
    private bool _dobleDisponible = false;

    private bool _girando = false;
    private float _energia = 0f;
    private Basis _giroObjetivo = Basis.Identity;

    private Node3D _malla;
    private Vector3 _mallaBase = Vector3.Zero;
    private Tween _tweenSalto;
    private Sprite3D _estela;

    private bool _enRed = false;
    private bool _controlo = true;

    public override void _Ready()
    {
        AddToGroup("carro");

        _enRed = Multiplayer.HasMultiplayerPeer();
        if (_enRed)
        {
            if (long.TryParse(Name, out long id))
                SetMultiplayerAuthority((int)id);
            _controlo = IsMultiplayerAuthority();
            if (!_controlo)
            {
                FreezeMode = FreezeModeEnum.Kinematic;
                Freeze = true;
            }
        }

        _delanteraIzq = GetNodeOrNull<VehicleWheel3D>("RuedaDelanteraIzq");
        _delanteraDer = GetNodeOrNull<VehicleWheel3D>("RuedaDelanteraDer");
        _traseraIzq = GetNodeOrNull<VehicleWheel3D>("RuedaTraseraIzq");
        _traseraDer = GetNodeOrNull<VehicleWheel3D>("RuedaTraseraDer");

        _malla = GetNodeOrNull<Node3D>("Malla");
        if (_malla != null) _mallaBase = _malla.Position;

        _motor = new AudioStreamPlayer();
        AddChild(_motor);
        var s = GD.Load<AudioStreamWav>("res://sonidos/motor.wav");
        if (s != null)
        {
            s.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            _motor.Stream = s;
            _motor.VolumeDb = -14f;
            _motor.Play();
        }

        _turbo = new AudioStreamPlayer();
        AddChild(_turbo);
        _turbo.Stream = GD.Load<AudioStream>("res://sonidos/turbo.wav");
        _turbo.VolumeDb = -4f;

        if (DisplayServer.GetName() != "headless")
            CrearEstela();
    }

    private void CrearEstela()
    {
        _estela = new Sprite3D();
        _estela.Texture = GD.Load<Texture2D>("res://nitro.png");
        _estela.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
        _estela.PixelSize = 0.0028f;
        _estela.Shaded = false;
        _estela.Position = new Vector3(0f, 0.4f, 3.2f);
        _estela.RotationDegrees = new Vector3(0f, -90f, 0f);
        _estela.Visible = false;
        AddChild(_estela);
    }

    public override void _PhysicsProcess(double delta)
    {
        float d = (float)delta;

        if (_enRed && !_controlo)
        {
            Vector3 p = GlobalPosition;
            if (_posInicializada && d > 0f)
            {
                Vector3 mov = p - _posAnterior;
                if (mov.LengthSquared() > 1e-8f) VelRed = mov / d;
            }
            _posAnterior = p;
            _posInicializada = true;
            return;
        }

        VelRed = LinearVelocity;
        _posAnterior = GlobalPosition;
        _posInicializada = true;

        float acelerar = Input.GetActionStrength("acelerar") - Input.GetActionStrength("retroceder");
        float dir = Input.GetActionStrength("izquierda") - Input.GetActionStrength("derecha");
        bool enPiso = EnPiso();

        UsandoTurbo = Input.IsActionPressed("turbo") && Boost > 0f && (!enPiso || acelerar > 0.1f);
        float fuerza = MotorFuerza + (UsandoTurbo ? TurboFuerza : 0f);
        EngineForce = -acelerar * fuerza;

        if (UsandoTurbo) Boost = Mathf.Max(0f, Boost - d * 0.5f);
        else Boost = Mathf.Min(1f, Boost + d * 0.2f);

        if (UsandoTurbo && !_turboSonaba) _turbo?.Play();
        _turboSonaba = UsandoTurbo;
        if (_estela != null) _estela.Visible = UsandoTurbo;

        _giroActual = Mathf.MoveToward(_giroActual, dir * GiroMax, GiroVelocidad * d);
        Steering = _giroActual;

        bool freno = Input.IsActionPressed("freno");
        Brake = freno ? FrenoFuerza : 0f;
        float agarre = freno ? AgarreDerrape : AgarreNormal;
        if (_traseraIzq != null) _traseraIzq.WheelFrictionSlip = agarre;
        if (_traseraDer != null) _traseraDer.WheelFrictionSlip = agarre;

        if (_motor != null)
        {
            float v = LinearVelocity.Length();
            _motor.PitchScale = 0.6f + Mathf.Min(v / 40f, 1.7f) + (UsandoTurbo ? 0.2f : 0f);
        }

        if (enPiso) { _dobleDisponible = true; _girando = false; }

        if (Input.IsActionJustPressed("saltar"))
        {
            if (enPiso)
            {
                SaltoDesdePiso();
            }
            else if (_dobleDisponible)
            {
                DobleSalto();
                _dobleDisponible = false;
            }
        }

        if (!enPiso)
        {
            if (_girando && _energia > 0f)
                AplicarGiro(d);
            else
                ControlAereo();

            if (UsandoTurbo)
                ApplyCentralForce(-GlobalTransform.Basis.Z * TurboAireFuerza * Mass);
        }
    }

    private bool EnPiso()
    {
        return (_delanteraIzq != null && _delanteraIzq.IsInContact())
            || (_delanteraDer != null && _delanteraDer.IsInContact())
            || (_traseraIzq != null && _traseraIzq.IsInContact())
            || (_traseraDer != null && _traseraDer.IsInContact());
    }

    private void SaltoDesdePiso()
    {
        if (_malla == null)
        {
            ApplyCentralImpulse(Vector3.Up * SaltoVel * Mass);
            return;
        }
        _tweenSalto?.Kill();
        _tweenSalto = CreateTween();
        _tweenSalto.TweenProperty(_malla, "position", _mallaBase - new Vector3(0, AgacheProfundidad, 0), AgacheDuracion)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _tweenSalto.TweenCallback(Callable.From(() => ApplyCentralImpulse(Vector3.Up * SaltoVel * Mass)));
        _tweenSalto.TweenProperty(_malla, "position", _mallaBase, 0.16f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void ControlAereo()
    {
        float pitchIn = Input.GetActionStrength("acelerar") - Input.GetActionStrength("retroceder");
        float yawIn = Input.GetActionStrength("izquierda") - Input.GetActionStrength("derecha");
        Basis b = GlobalTransform.Basis;
        Vector3 torque = b.X * (-pitchIn * AirePitchFuerza) + b.Y * (-yawIn * AireYawFuerza);
        ApplyTorque(torque * Mass);
        ApplyTorque(-AngularVelocity * AireAmortiguacion * Mass);
    }

    private void DobleSalto()
    {
        bool w = Input.IsActionPressed("acelerar");
        bool s = Input.IsActionPressed("retroceder");
        bool a = Input.IsActionPressed("izquierda");
        bool dd = Input.IsActionPressed("derecha");

        Basis b = GlobalTransform.Basis.Orthonormalized();
        Vector3 dirImp = Vector3.Zero;
        if (w) dirImp += -b.Z;
        if (s) dirImp += b.Z;
        if (a) dirImp += -b.X;
        if (dd) dirImp += b.X;

        if (dirImp.Length() > 0.1f)
        {
            dirImp = dirImp.Normalized();
            ApplyCentralImpulse((dirImp * DashVel + Vector3.Up * DashElevacion) * Mass);
        }
        else
        {
            ApplyCentralImpulse(Vector3.Up * SaltoVel * Mass);
        }

        float pitch = Mathf.DegToRad(AnguloPitch);
        float yaw = 0f;
        if ((w || s) && (a || dd))
            yaw = (a ? 1f : -1f) * Mathf.DegToRad(AnguloYaw);

        _giroObjetivo = b * Basis.FromEuler(new Vector3(pitch, yaw, 0f));
        _energia = EnergiaGiro;
        _girando = true;
    }

    private void AplicarGiro(float d)
    {
        Basis actual = GlobalTransform.Basis.Orthonormalized();
        Basis err = _giroObjetivo * actual.Inverse();
        Quaternion q = err.GetRotationQuaternion().Normalized();

        float w = Mathf.Clamp(q.W, -1f, 1f);
        float ang = 2f * Mathf.Acos(w);
        Vector3 eje = new Vector3(q.X, q.Y, q.Z);
        if (ang > Mathf.Pi) ang -= Mathf.Tau;
        eje = eje.LengthSquared() > 1e-6f ? eje.Normalized() : Vector3.Up;
        Vector3 correccion = eje * ang;

        if (correccion.Length() < 0.05f && AngularVelocity.Length() < 0.3f)
        {
            _girando = false;
            return;
        }

        Vector3 tor = correccion * GiroKp - AngularVelocity * GiroKd;
        float mag = tor.Length();
        if (mag > GiroTorqueMax) tor *= GiroTorqueMax / mag;

        ApplyTorqueImpulse(tor * Mass * d);

        _energia -= tor.Length() * d;
        if (_energia <= 0f) _girando = false;
    }
}
