using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using BarajaCartones.Bingo;
using BarajaCartones.UI3D;

namespace BarajaCartones;

/// <summary>
/// Versión 3D. La escena (escenas/juego3d.tscn) solo tiene este nodo:
/// TODO lo demás se crea aquí por código, al estilo XNA.
///
/// Árbol que se construye:
///   Juego3D
///     WorldEnvironment   -> cielo, luz ambiental, tonemapping, SSAO
///     Sol, Relleno       -> DirectionalLight3D
///     Camara             -> Camera3D
///     Tapete, Mazo       -> MeshInstance3D
///     Mesa, Descarte, Mano -> zonas
///     Cartones           -> todos los Carton3D cuelgan de aquí
///     Interfaz           -> CanvasLayer: UI 2D por encima del mundo 3D
/// </summary>
public partial class Juego3D : Node3D
{
    private const float AlturaArrastre = 1.0f;          // altura a la que flota el cartón arrastrado
    private static readonly Vector3 PosicionMazo = new(4.7f, 0f, 1.6f);
    private const float AlturaMazo = 0.25f;

    [Export] public int CartonesIniciales = 5;

    private Camera3D _camara;
    private Node3D _cartones;
    private Mano3D _mano;
    private Mesa3D _mesa;
    private Descarte3D _descarte;
    private readonly List<Zona3D> _zonas = new();
    private Button _botonRobar;
    private Label _contador;

    private Carton3D _hover;
    private Carton3D _arrastrado;
    private Vector3 _agarre;

    private readonly Random _rng = new();
    private int _siguienteId = 1;

    public override void _Ready()
    {
        CrearEntorno();
        CrearEscenario();
        CrearZonas();
        CrearInterfaz();
        _ = RepartirInicial();
    }

    // =====================================================================
    // Construcción de la escena
    // =====================================================================

    private void CrearEntorno()
    {
        // El "Environment" controla la luz que no viene de lámparas: el cielo,
        // la luz ambiental, los reflejos y el postprocesado.
        // (Godot.Environment porque System también tiene una clase Environment.)
        var cielo = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.38f, 0.55f, 0.8f),
            SkyHorizonColor = new Color(0.78f, 0.82f, 0.88f),
            GroundHorizonColor = new Color(0.62f, 0.58f, 0.52f),
            GroundBottomColor = new Color(0.25f, 0.22f, 0.2f),
        };
        var entorno = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = cielo },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,   // el cielo ilumina las sombras
            AmbientLightEnergy = 0.45f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Aces,           // colores más "fotográficos"
            SsaoEnabled = true,                                        // oscurece rincones y contactos
            SsaoRadius = 0.6f,
            SsaoIntensity = 1.5f,
        };
        AddChild(new WorldEnvironment { Name = "WorldEnvironment", Environment = entorno });

        // Sol: luz direccional (como el sol: rayos paralelos) con sombras suaves.
        var sol = new DirectionalLight3D
        {
            Name = "Sol",
            LightColor = new Color(1f, 0.97f, 0.92f),
            LightEnergy = 1.0f,
            ShadowEnabled = true,
            LightAngularDistance = 1.5f,          // > 0 = sombras de borde suave
            DirectionalShadowMaxDistance = 30f,
        };
        AddChild(sol);
        sol.RotationDegrees = new Vector3(-58f, -32f, 0f);

        // Luz de relleno desde el lado contrario, sin sombras, para que las
        // zonas en sombra no queden negras.
        var relleno = new DirectionalLight3D
        {
            Name = "Relleno",
            LightColor = new Color(0.8f, 0.87f, 1f),
            LightEnergy = 0.25f,
        };
        AddChild(relleno);
        relleno.RotationDegrees = new Vector3(-35f, 150f, 0f);

        _camara = new Camera3D { Name = "Camara", Fov = 40f, Position = new Vector3(0f, 9f, 8f) };
        AddChild(_camara);
        _camara.LookAt(new Vector3(0f, 0f, 0.4f), Vector3.Up); // LookAt necesita estar ya en el árbol

        // Antialiasing de bordes (MSAA) para la vista 3D.
        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
    }

    private void CrearEscenario()
    {
        // Tapete verde: una caja grande y plana. La cara de arriba está en y = 0.
        AddChild(new MeshInstance3D
        {
            Name = "Tapete",
            Mesh = new BoxMesh { Size = new Vector3(28f, 0.4f, 18f) },
            Position = new Vector3(0f, -0.2f, -2f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.09f, 0.3f, 0.19f),
                Roughness = 1f,
            },
        });

        // Mazo: un taco de cartones del que salen los nuevos. Se puede hacer clic en él.
        AddChild(new MeshInstance3D
        {
            Name = "Mazo",
            Mesh = new BoxMesh { Size = new Vector3(Carton3D.Ancho, AlturaMazo, Carton3D.Alto) },
            Position = PosicionMazo + new Vector3(0f, AlturaMazo / 2f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.6f, 0.13f, 0.15f),
                Roughness = 0.6f,
            },
        });
        AddChild(new Label3D
        {
            Text = "MAZO",
            FontSize = 64,
            PixelSize = 0.0045f,
            RotationDegrees = new Vector3(-90f, 0f, 0f),
            Position = PosicionMazo + new Vector3(0f, AlturaMazo + 0.01f, 0f),
            Modulate = new Color(1f, 1f, 1f, 0.8f),
        });
    }

    private void CrearZonas()
    {
        _mesa = new Mesa3D
        {
            Name = "Mesa",
            Titulo = "MESA",
            Position = new Vector3(-2f, 0f, -1.4f),
            Tamano = new Vector2(9.6f, 4.8f),
            Capacidad = 9,
        };
        _descarte = new Descarte3D
        {
            Name = "Descarte",
            Titulo = "DESCARTE",
            Position = new Vector3(4.7f, 0f, -1.8f),
            Tamano = new Vector2(3.4f, 4.0f),
            Capacidad = int.MaxValue,
        };
        _mano = new Mano3D
        {
            Name = "Mano",
            Position = new Vector3(0f, 1.4f, 3.9f),
            Camara = _camara,
            Capacidad = 8,
        };
        foreach (Zona3D zona in new Zona3D[] { _mesa, _descarte, _mano })
        {
            AddChild(zona);
            _zonas.Add(zona);
        }

        _cartones = new Node3D { Name = "Cartones" };
        AddChild(_cartones);
    }

    private void CrearInterfaz()
    {
        // CanvasLayer: una capa 2D que se dibuja ENCIMA del mundo 3D.
        // Los Control de dentro funcionan igual que en la versión 2D.
        var capa = new CanvasLayer { Name = "Interfaz" };
        AddChild(capa);

        _botonRobar = new Button
        {
            Text = "Robar cartón",
            Position = new Vector2(20f, 20f),
            CustomMinimumSize = new Vector2(190f, 48f),
        };
        _botonRobar.AddThemeFontSizeOverride("font_size", 20);
        _botonRobar.Pressed += RobarCarton;
        capa.AddChild(_botonRobar);

        _contador = new Label { Position = new Vector2(24f, 78f) };
        _contador.AddThemeFontSizeOverride("font_size", 18);
        capa.AddChild(_contador);

        var ayuda = new Label
        {
            Position = new Vector2(24f, 110f),
            Text = "Arrastra cartones a la MESA o al DESCARTE.\nClic en el MAZO para robar.",
            Modulate = new Color(1f, 1f, 1f, 0.75f),
        };
        capa.AddChild(ayuda);
    }

    // =====================================================================
    // Cartones
    // =====================================================================

    private async Task RepartirInicial()
    {
        for (int i = 0; i < CartonesIniciales; i++)
        {
            RobarCarton();
            await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
        }
    }

    private void RobarCarton()
    {
        if (_mano.Llena) return;

        var carton = new Carton3D { Name = $"Carton{_siguienteId}" };
        carton.Configurar(
            CartonBingo.Generar(_siguienteId++, _rng),
            Color.FromHsv((float)_rng.NextDouble(), 0.55f, 0.75f));
        _cartones.AddChild(carton);

        // Aparece encima del mazo y vuela a la mano.
        carton.GlobalPosition = PosicionMazo + new Vector3(0f, AlturaMazo, 0f);
        _mano.Agregar(carton);
        ActualizarInterfaz();
    }

    private void ActualizarInterfaz()
    {
        _botonRobar.Disabled = _mano.Llena;
        _contador.Text = $"Mano {_mano.Cantidad}/{_mano.Capacidad}   ·   Descartados: {_descarte.TotalDescartados}";
    }

    // =====================================================================
    // Ratón: del punto 2D de la pantalla a un rayo en el mundo 3D
    // =====================================================================

    /// <summary>
    /// La cámara convierte el píxel del ratón en un rayo: un punto de origen
    /// y una dirección. Todo lo que el ratón "toca" en 3D sale de este rayo.
    /// </summary>
    private (Vector3 origen, Vector3 direccion) RayoDelRaton()
    {
        Vector2 raton = GetViewport().GetMousePosition();
        return (_camara.ProjectRayOrigin(raton), _camara.ProjectRayNormal(raton));
    }

    /// <summary>Punto donde el rayo del ratón corta un plano horizontal a cierta altura.</summary>
    private Vector3? PuntoEnAltura(float altura)
    {
        var (origen, direccion) = RayoDelRaton();
        return new Plane(Vector3.Up, altura).IntersectsRay(origen, direccion);
    }

    /// <summary>El cartón más cercano a la cámara que está bajo el ratón.</summary>
    private Carton3D CartonBajoRaton()
    {
        var (origen, direccion) = RayoDelRaton();
        Carton3D mejor = null;
        float mejorDistancia = float.MaxValue;
        foreach (Node nodo in _cartones.GetChildren())
        {
            if (nodo is not Carton3D carton || !carton.Interactivo || carton == _arrastrado) continue;
            if (carton.Intersecta(origen, direccion, out float distancia) && distancia < mejorDistancia)
            {
                mejor = carton;
                mejorDistancia = distancia;
            }
        }
        return mejor;
    }

    private bool RatonSobreMazo()
    {
        Vector3? p = PuntoEnAltura(AlturaMazo);
        return p.HasValue
            && Mathf.Abs(p.Value.X - PosicionMazo.X) <= Carton3D.Ancho / 2f
            && Mathf.Abs(p.Value.Z - PosicionMazo.Z) <= Carton3D.Alto / 2f;
    }

    public override void _Process(double delta)
    {
        if (_arrastrado != null)
        {
            // El cartón sigue al punto donde el ratón corta el plano de arrastre.
            _agarre = _agarre.Lerp(Vector3.Zero, 1f - Mathf.Exp(-6f * (float)delta));
            Vector3? p = PuntoEnAltura(AlturaArrastre);
            if (p.HasValue) _arrastrado.ObjetivoArrastre = p.Value - _agarre;
            return;
        }

        Carton3D nuevo = CartonBajoRaton();
        if (nuevo == _hover) return;
        _hover = nuevo;
        foreach (Zona3D zona in _zonas) zona.EstablecerHover(_hover);
    }

    /// <summary>
    /// _UnhandledInput: solo llegan los eventos que la interfaz NO ha usado.
    /// Así, un clic en el botón "Robar cartón" no coge también un cartón
    /// que esté detrás.
    /// </summary>
    public override void _UnhandledInput(InputEvent evento)
    {
        if (evento is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;

        if (_hover != null) EmpezarArrastre(_hover);
        else if (RatonSobreMazo()) RobarCarton();
    }

    /// <summary>
    /// Soltar va en _Input (llega siempre): si soltáramos encima del botón,
    /// la interfaz se quedaría el evento y el cartón se quedaría "pegado".
    /// </summary>
    public override void _Input(InputEvent evento)
    {
        if (_arrastrado != null
            && evento is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            Soltar();
        }
    }

    private void EmpezarArrastre(Carton3D carton)
    {
        _arrastrado = carton;
        _hover = null;
        foreach (Zona3D zona in _zonas) zona.EstablecerHover(null);

        carton.EmpezarArrastre();
        Vector3? p = PuntoEnAltura(AlturaArrastre);
        _agarre = p.HasValue ? p.Value - carton.GlobalPosition : Vector3.Zero;
        _agarre.Y = 0f;
        carton.ObjetivoArrastre = carton.GlobalPosition;
    }

    private void Soltar()
    {
        Carton3D carton = _arrastrado;
        _arrastrado = null;
        carton.TerminarArrastre();

        Zona3D origen = carton.Zona;
        Zona3D destino = null;
        Vector3? puntoMesa = PuntoEnAltura(0f);
        if (puntoMesa.HasValue)
        {
            foreach (Zona3D zona in _zonas)
                if (zona.ContienePunto(puntoMesa.Value)) { destino = zona; break; }
        }

        if (destino == null || destino == origen || destino.Llena)
        {
            origen.Reordenar(); // vuelve a su sitio
            return;
        }

        origen.Quitar(carton);
        destino.Agregar(carton);
        ActualizarInterfaz();
    }
}
