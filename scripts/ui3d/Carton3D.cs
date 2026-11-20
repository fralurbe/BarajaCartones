using System.Threading.Tasks;
using Godot;
using BarajaCartones.Bingo;
using BarajaCartones.UI;

namespace BarajaCartones.UI3D;

/// <summary>
/// Un cartón en 3D. Todo se construye por código en _Ready (no hay .tscn):
///
///   Carton3D (Node3D)
///     Cuerpo (MeshInstance3D, BoxMesh)    -> el grosor del cartón
///     Cara   (MeshInstance3D, PlaneMesh)  -> la cara con los números (textura)
///     Render (SubViewport)                -> solo al principio: dibuja la textura
///       CaraCarton (Control)              -> el MISMO script de dibujo que en 2D
///
/// El cartón está "tumbado": su cara mira hacia arriba (+Y). Mide
/// Ancho x Alto en el plano XZ.
///
/// Truco: la textura se dibuja una vez en el SubViewport, se copia a una
/// imagen normal con mipmaps (para que el texto no "parpadee" al verse
/// pequeño o inclinado) y el SubViewport se borra. Se "hornea".
/// </summary>
public partial class Carton3D : Node3D
{
    public const float Alto = 1f;
    public static readonly float Ancho = CaraCarton.Tamano.X / CaraCarton.Tamano.Y; // ~2.39
    public const float Grosor = 0.02f;

    public CartonBingo Datos { get; private set; }
    public Zona3D Zona { get; set; }
    public bool Interactivo { get; set; } = true;
    public bool Arrastrando { get; private set; }

    /// <summary>Punto al que va el cartón mientras se arrastra (lo pone Juego3D).</summary>
    public Vector3 ObjetivoArrastre { get; set; }

    private Color _color;
    private SubViewport _render;
    private StandardMaterial3D _materialCara;
    private Tween _tween;
    private Vector3 _velocidad;

    public void Configurar(CartonBingo datos, Color color)
    {
        Datos = datos;
        _color = color;
    }

    public override void _Ready()
    {
        // 1) Cuerpo: una caja fina. Un poco más pequeña que la cara para que
        //    no asome por las esquinas redondeadas.
        var cuerpo = new MeshInstance3D
        {
            Name = "Cuerpo",
            Mesh = new BoxMesh { Size = new Vector3(Ancho - 0.06f, Grosor, Alto - 0.06f) },
            Position = new Vector3(0, Grosor / 2f, 0),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = _color.Darkened(0.35f),
                Roughness = 0.7f,
            },
        };
        AddChild(cuerpo);

        // 2) SubViewport (el RenderTarget2D de XNA) con el dibujo 2D de siempre.
        //    Lo pintamos al doble de resolución para que se vea nítido de cerca.
        _render = new SubViewport
        {
            Name = "Render",
            Size = (Vector2I)(CaraCarton.Tamano * 2),
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };
        var dibujo = new CaraCarton { Size = CaraCarton.Tamano };
        _render.AddChild(dibujo);
        AddChild(_render);
        // CanvasTransform solo se puede poner cuando el SubViewport ya está
        // en el árbol (antes no tiene "lienzo" asociado y Godot da error).
        _render.CanvasTransform = Transform2D.Identity.Scaled(new Vector2(2, 2));
        dibujo.Mostrar(Datos, _color);

        // 3) Cara: un plano con la textura. AlphaScissor recorta las esquinas
        //    redondeadas (píxeles transparentes = no se pintan).
        _materialCara = new StandardMaterial3D
        {
            AlbedoTexture = _render.GetTexture(),
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
            AlphaScissorThreshold = 0.5f,
            Roughness = 0.45f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        var cara = new MeshInstance3D
        {
            Name = "Cara",
            Mesh = new PlaneMesh { Size = new Vector2(Ancho, Alto) }, // PlaneMesh ya mira hacia +Y
            Position = new Vector3(0, Grosor + 0.0005f, 0),
            MaterialOverride = _materialCara,
        };
        AddChild(cara);

        _ = HornearTextura();
    }

    /// <summary>
    /// Espera a que el SubViewport haya pintado, copia el resultado a una
    /// ImageTexture con mipmaps y borra el SubViewport (ya no hace falta).
    /// </summary>
    private async Task HornearTextura()
    {
        // FramePostDraw: señal del servidor de render "acabo de dibujar un frame".
        // Esperamos dos por seguridad.
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

        Image imagen = _render.GetTexture().GetImage();
        imagen.GenerateMipmaps();
        _materialCara.AlbedoTexture = ImageTexture.CreateFromImage(imagen);

        _render.QueueFree();
        _render = null;
    }

    // ---- Movimiento --------------------------------------------------------

    /// <summary>Anima el cartón hasta una posición/rotación/escala (coordenadas globales).</summary>
    public void MoverA(Vector3 posicion, Quaternion rotacion, float escala, float duracion = 0.35f)
    {
        _tween?.Kill();
        _tween = CreateTween()
            .SetParallel()
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        // En 3D la rotación se anima mejor como Quaternion: interpola por el
        // camino más corto (con ángulos de Euler pueden salir giros raros).
        _tween.TweenProperty(this, "global_position", posicion, duracion);
        _tween.TweenProperty(this, "quaternion", rotacion, duracion);
        _tween.TweenProperty(this, "scale", Vector3.One * escala, duracion);
    }

    public void EmpezarArrastre()
    {
        _tween?.Kill();
        Arrastrando = true;
        ObjetivoArrastre = GlobalPosition;
    }

    public void TerminarArrastre() => Arrastrando = false;

    public override void _Process(double delta)
    {
        if (!Arrastrando) return;
        float dt = (float)delta;
        if (dt <= 0) return;

        // Seguir al objetivo con algo de retraso.
        Vector3 nueva = GlobalPosition.Lerp(ObjetivoArrastre, Suavizado(18f, dt));
        _velocidad = _velocidad.Lerp((nueva - GlobalPosition) / dt, Suavizado(10f, dt));
        GlobalPosition = nueva;

        // Inclinación REAL en 3D según la velocidad (antes lo fingía el shader).
        var inclinacion = Quaternion.FromEuler(new Vector3(
            Mathf.Clamp(_velocidad.Z * 0.05f, -0.5f, 0.5f),
            0f,
            Mathf.Clamp(-_velocidad.X * 0.05f, -0.5f, 0.5f)));
        Quaternion = Quaternion.Slerp(inclinacion, Suavizado(12f, dt));
        Scale = Scale.Lerp(Vector3.One * 1.1f, Suavizado(10f, dt));
    }

    // ---- Selección con el ratón ---------------------------------------------

    /// <summary>
    /// ¿Corta el rayo (origen, dirección) a la cara de este cartón?
    /// Pasamos el rayo al espacio LOCAL del cartón: ahí el cartón es un
    /// rectángulo sencillo en el plano y = Grosor, sin rotación ni escala.
    /// Devuelve la distancia desde el origen del rayo al punto de corte.
    /// </summary>
    public bool Intersecta(Vector3 origen, Vector3 direccion, out float distancia)
    {
        distancia = 0f;
        Transform3D aLocal = GlobalTransform.AffineInverse();
        Vector3 o = aLocal * origen;            // un punto: se transforma entero
        Vector3 d = aLocal.Basis * direccion;   // una dirección: solo rotación/escala

        if (Mathf.Abs(d.Y) < 1e-6f) return false; // rayo paralelo al cartón
        float t = (Grosor - o.Y) / d.Y;
        if (t < 0f) return false;                 // el cartón está detrás

        Vector3 p = o + d * t;
        if (Mathf.Abs(p.X) > Ancho / 2f || Mathf.Abs(p.Z) > Alto / 2f) return false;

        distancia = origen.DistanceTo(GlobalTransform * p);
        return true;
    }

    private static float Suavizado(float velocidad, float dt) => 1f - Mathf.Exp(-velocidad * dt);
}
