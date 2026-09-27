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

    // --- Combate: vida y demolicion ---
    [Export] public float VidaMax = 100f;
    [Export] public float UmbralDemolicion = 8f;   // velocidad minima (m/s) para hacer dano
    [Export] public float FactorDano = 3.2f;        // dano por cada m/s por encima del umbral
    [Export] public float TiempoReaparecer = 3.0f;

    public float Vida { get; private set; } = 100f;
    public bool Muerto => _muerto;
    private bool _muerto = false;
    private Vector3 _puntoReaparecer = Vector3.Zero;

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

        // Combate: vida inicial, punto donde reaparece, y deteccion de choques.
        Vida = VidaMax;
        _puntoReaparecer = GlobalPosition;
        ContactMonitor = true;
        MaxContactsReported = 8;
        BodyEntered += AlChocar;

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

        // Si estoy destruido, no me dejo controlar hasta reaparecer.
        if (_muerto)
        {
            EngineForce = 0f;
            Steering = 0f;
            Brake = FrenoFuerza;
            return;
        }

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

    // ================= COMBATE: vida y demolicion =================

    // Cuando MI carro choca contra otro carro yendo rapido, le reporto el golpe al servidor.
    private void AlChocar(Node cuerpo)
    {
        if (!_controlo || _muerto) return;       // solo el dueno de este carro reporta
        if (cuerpo is not Carro otro) return;    // solo contra otros carros
        if (otro == this) return;

        float miVel = VelRed.Length();
        if (miVel < UmbralDemolicion) return;

        float dano = (miVel - UmbralDemolicion) * FactorDano;
        if (dano <= 0f) return;

        int idAtacante = GetMultiplayerAuthority();
        if (_enRed)
            otro.RpcId(1, Carro.MethodName.RecibirDano, dano, idAtacante);
        else
            otro.AplicarDano(dano, idAtacante);
    }

    // Llega al servidor: el servidor decide el dano de verdad.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void RecibirDano(float dano, int idAtacante)
    {
        if (_enRed && !Multiplayer.IsServer()) return;
        AplicarDano(dano, idAtacante);
    }

    private void AplicarDano(float dano, int idAtacante)
    {
        if (_muerto) return;
        Vida = Mathf.Max(0f, Vida - dano);

        // avisarle su nueva vida al dueno (para la barra del HUD)
        int dueno = GetMultiplayerAuthority();
        if (_enRed && dueno != 1)
            RpcId(dueno, MethodName.ActualizarVida, Vida);
        else
            ActualizarVida(Vida);

        if (Vida <= 0f)
        {
            _muerto = true;
            if (_enRed)
                Rpc(MethodName.Demoler, idAtacante);
            else
                Demoler(idAtacante);

            GetTree().CreateTimer(TiempoReaparecer).Timeout += Revivir_Servidor;
        }
    }

    private void Revivir_Servidor()
    {
        Vida = VidaMax;
        _muerto = false;
        Vector3 pos = _puntoReaparecer + new Vector3(
            (float)GD.RandRange(-3.0, 3.0), 0.6f, (float)GD.RandRange(-3.0, 3.0));
        if (_enRed)
            Rpc(MethodName.Revivir, pos, VidaMax);
        else
            Revivir(pos, VidaMax);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void ActualizarVida(float v)
    {
        Vida = v;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void Demoler(int idAtacante)
    {
        _muerto = true;
        if (_malla != null) _malla.Visible = false;
        if (_estela != null) _estela.Visible = false;

        Explotar();

        foreach (var n in GetTree().GetNodesInGroup("juego"))
            if (n is Juego j) { j.AvisarDemolicion(idAtacante, GetMultiplayerAuthority()); break; }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void Revivir(Vector3 pos, float v)
    {
        _muerto = false;
        Vida = v;
        if (_malla != null) _malla.Visible = true;

        if (_controlo)
        {
            GlobalPosition = pos;
            LinearVelocity = Vector3.Zero;
            AngularVelocity = Vector3.Zero;
        }
    }

    // Explosion de particulas cuando un carro es destruido (no se dibuja en el servidor).
    private void Explotar()
    {
        if (DisplayServer.GetName() == "headless") return;

        var p = new GpuParticles3D();
        var mat = new ParticleProcessMaterial();
        mat.Direction = new Vector3(0, 1, 0);
        mat.Spread = 180f;
        mat.InitialVelocityMin = 6f;
        mat.InitialVelocityMax = 13f;
        mat.Gravity = new Vector3(0, -9.8f, 0);
        mat.ScaleMin = 0.3f;
        mat.ScaleMax = 0.9f;
        mat.Color = new Color(1f, 0.55f, 0.1f);
        p.ProcessMaterial = mat;
        p.DrawPass1 = new SphereMesh { Radius = 0.22f, Height = 0.44f };
        p.Amount = 40;
        p.Lifetime = 0.8;
        p.OneShot = true;
        p.Explosiveness = 0.95f;
        p.Emitting = true;

        var raiz = GetTree().CurrentScene;
        if (raiz == null) return;
        raiz.AddChild(p);
        p.GlobalPosition = GlobalPosition + Vector3.Up * 0.5f;
        GetTree().CreateTimer(2.0).Timeout += () => { if (IsInstanceValid(p)) p.QueueFree(); };
    }
}
