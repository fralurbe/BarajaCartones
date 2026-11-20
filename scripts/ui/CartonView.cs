using Godot;
using MiGame.Bingo;

namespace MiGame.UI;

/// <summary>
/// Un cartón en pantalla: se puede pasar el ratón por encima, cogerlo,
/// arrastrarlo y soltarlo.
///
/// Estructura de la escena (escenas/carton.tscn):
///   Carton (Control, este script)  -> recibe el ratón
///     Render (SubViewport)          -> "pantalla" invisible donde se dibuja el cartón
///       Cara (CaraCarton)           -> dibuja números y colores
///     Visual (TextureRect)          -> muestra la textura del SubViewport con el shader
///
/// El cartón NO decide adónde va cuando lo sueltas: solo emite la señal
/// Soltado. Quien lo escucha (Juego) decide. Así cada pieza tiene una
/// única responsabilidad.
/// </summary>
public partial class CartonView : Control
{
    // ---- Señales ---------------------------------------------------------
    // En C# una señal se declara con [Signal] y un delegado que termina en
    // "EventHandler". Godot genera un evento con el nombre sin ese sufijo:
    // carton.Soltado += MiMetodo;
    [Signal] public delegate void SoltadoEventHandler(CartonView carton, Vector2 posicionGlobal);
    [Signal] public delegate void HoverCambiadoEventHandler(CartonView carton, bool encima);

    // ---- Parámetros editables en el inspector ([Export]) ------------------
    [Export] public float InclinacionMaxima = 14f;   // grados
    [Export] public float DuracionMovimiento = 0.3f; // segundos
    [Export] public float EscalaArrastre = 0.75f;

    /// <summary>El cartón que se está arrastrando ahora mismo (o null).</summary>
    public static CartonView EnArrastre { get; private set; }

    public CartonBingo Datos { get; private set; }
    public bool Interactivo { get; set; } = true;
    public bool Arrastrando { get; private set; }
    public bool RatonEncima { get; private set; }

    private SubViewport _render;
    private CaraCarton _cara;
    private TextureRect _visual;
    private ShaderMaterial _shader;
    private Color _color;

    private Tween _tween;
    private Vector2 _agarre;       // distancia del ratón al centro al cogerlo
    private Vector2 _velocidad;    // px/s mientras se arrastra
    private Vector2 _inclinacion;  // (rot_x, rot_y) actuales del shader
    private float _brillo;

    public override void _Ready()
    {
        // GetNode busca hijos por su ruta en el árbol de la escena.
        _render = GetNode<SubViewport>("Render");
        _cara = GetNode<CaraCarton>("Render/Cara");
        _visual = GetNode<TextureRect>("Visual");

        // La textura del SubViewport (lo que "ve") se usa como imagen.
        _visual.Texture = _render.GetTexture();

        // El material está marcado "local to scene" en carton.tscn: cada
        // cartón tiene SU PROPIA copia. Si no, todos compartirían el mismo
        // material y al inclinar uno se inclinarían todos.
        _shader = (ShaderMaterial)_visual.Material;
        _shader.SetShaderParameter("aspecto", Size.X / Size.Y);

        // Rotar y escalar alrededor del centro, no de la esquina.
        PivotOffset = Size / 2;

        // Señales que ya trae Control. Lambdas como manejadores.
        MouseEntered += () => CambiarHover(true);
        MouseExited += () => CambiarHover(false);

        if (Datos != null) Redibujar();
    }

    /// <summary>
    /// Se puede llamar antes de añadir el nodo al árbol (antes de _Ready).
    /// En ese caso solo guardamos los datos y _Ready los aplica.
    /// </summary>
    public void Configurar(CartonBingo datos, Color color)
    {
        Datos = datos;
        _color = color;
        if (IsNodeReady()) Redibujar();
    }

    public void Redibujar()
    {
        _cara.Mostrar(Datos, _color);
        // El contenido no cambia cada frame: le pedimos al SubViewport que
        // pinte UNA vez (ahorra trabajo). Hay que repetirlo si cambia algo.
        _render.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    // ---- Movimiento animado ----------------------------------------------

    /// <summary>Centro del cartón en coordenadas globales (de pantalla).</summary>
    public Vector2 CentroGlobal => GetGlobalTransform() * PivotOffset;

    /// <summary>Mueve el cartón con una animación (Tween) hasta esa colocación.</summary>
    public void MoverA(Vector2 centroLocal, float rotacion, float escala)
    {
        // Si había una animación en marcha la paramos; si no, pelearían.
        _tween?.Kill();
        _tween = CreateTween()
            .SetParallel()                          // las tres propiedades a la vez
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        // El nombre de la propiedad es el de Godot (snake_case), no el de C#.
        _tween.TweenProperty(this, "position", centroLocal - PivotOffset, DuracionMovimiento);
        _tween.TweenProperty(this, "rotation", rotacion, DuracionMovimiento);
        _tween.TweenProperty(this, "scale", Vector2.One * escala, DuracionMovimiento);
    }

    /// <summary>Coloca el centro del cartón en un punto global, sin animación.</summary>
    public void SituarCentroGlobal(Vector2 centroGlobal)
    {
        var padre = GetParent<CanvasItem>();
        Vector2 local = padre.GetGlobalTransform().AffineInverse() * centroGlobal;
        Position = local - PivotOffset;
    }

    /// <summary>Cambia de nodo padre sin que el cartón "salte" en pantalla.</summary>
    public void CambiarDePadre(Node nuevoPadre)
    {
        if (GetParent() == nuevoPadre) return;
        if (GetParent() == null)
        {
            nuevoPadre.AddChild(this);
            return;
        }
        Vector2 centro = CentroGlobal;
        Reparent(nuevoPadre, false);
        SituarCentroGlobal(centro);
    }

    // ---- Entrada (ratón) ---------------------------------------------------

    private void CambiarHover(bool encima)
    {
        // Mientras se arrastra OTRO cartón, ignoramos el hover de este.
        if (encima && EnArrastre != null && EnArrastre != this) return;
        if (RatonEncima == encima) return;
        RatonEncima = encima;
        EmitSignal(SignalName.HoverCambiado, this, encima);
    }

    /// <summary>
    /// _GuiInput: solo llega si el ratón está SOBRE este Control
    /// (y ningún otro control encima se lo ha quedado).
    /// </summary>
    public override void _GuiInput(InputEvent evento)
    {
        if (!Interactivo) return;
        if (evento is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            EmpezarArrastre();
            AcceptEvent(); // "este clic es mío", no se propaga a los padres
        }
    }

    /// <summary>
    /// _Input: llega SIEMPRE, esté donde esté el ratón. Lo usamos para
    /// detectar el momento de soltar, porque al mover rápido el ratón puede
    /// salirse un instante del cartón.
    /// </summary>
    public override void _Input(InputEvent evento)
    {
        if (!Arrastrando) return;
        if (evento is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            Arrastrando = false;
            EnArrastre = null;
            GetViewport().SetInputAsHandled();
            EmitSignal(SignalName.Soltado, this, GetGlobalMousePosition());
        }
    }

    private void EmpezarArrastre()
    {
        _tween?.Kill();
        Arrastrando = true;
        EnArrastre = this;
        _agarre = GetGlobalMousePosition() - CentroGlobal;
        // ZIndex decide el ORDEN DE DIBUJO: 1000 lo pone encima de todo.
        // Ojo: no cambia qué control recibe el ratón (eso va por el orden en el árbol).
        ZIndex = 1000;
    }

    // ---- Cada frame ------------------------------------------------------

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Vector2 inclinacionObjetivo = Vector2.Zero;
        Vector2 luz = new(0.5f, 0.5f);

        if (Arrastrando)
        {
            // El punto de agarre se va centrando poco a poco.
            _agarre = _agarre.Lerp(Vector2.Zero, Suavizado(8f, dt));

            // Seguimos al ratón con un poco de retraso (queda más natural).
            Vector2 actual = CentroGlobal;
            Vector2 destino = GetGlobalMousePosition() - _agarre;
            Vector2 nuevo = actual.Lerp(destino, Suavizado(25f, dt));
            _velocidad = dt > 0 ? (nuevo - actual) / dt : Vector2.Zero;
            SituarCentroGlobal(nuevo);

            // Se balancea según la velocidad horizontal y se inclina en 3D
            // en la dirección del movimiento.
            float rotacionObjetivo = Mathf.Clamp(_velocidad.X * 0.0004f, -0.35f, 0.35f);
            Rotation = Mathf.Lerp(Rotation, rotacionObjetivo, Suavizado(10f, dt));
            Scale = Scale.Lerp(Vector2.One * EscalaArrastre, Suavizado(12f, dt));

            inclinacionObjetivo = new Vector2(_velocidad.Y, -_velocidad.X) * 0.02f;
        }
        else if (RatonEncima && Interactivo)
        {
            // Posición del ratón dentro del cartón, de -0.5 a 0.5.
            // GetLocalMousePosition ya tiene en cuenta la rotación y escala.
            Vector2 local = GetLocalMousePosition() / Size - new Vector2(0.5f, 0.5f);
            // El lado donde está el ratón "se hunde".
            inclinacionObjetivo = new Vector2(-local.Y, local.X) * 2f * InclinacionMaxima;
            luz = local + new Vector2(0.5f, 0.5f);
        }

        inclinacionObjetivo = inclinacionObjetivo.Clamp(-Vector2.One * InclinacionMaxima, Vector2.One * InclinacionMaxima);
        _inclinacion = _inclinacion.Lerp(inclinacionObjetivo, Suavizado(12f, dt));
        float brilloObjetivo = (Arrastrando || (RatonEncima && Interactivo)) ? 0.35f : 0f;
        _brillo = Mathf.Lerp(_brillo, brilloObjetivo, Suavizado(10f, dt));

        _shader.SetShaderParameter("rot_x", _inclinacion.X);
        _shader.SetShaderParameter("rot_y", _inclinacion.Y);
        _shader.SetShaderParameter("brillo", _brillo);
        _shader.SetShaderParameter("luz", luz);
    }

    /// <summary>
    /// Factor para Lerp que no depende de los FPS: con "velocidad" alta
    /// se llega antes al objetivo. (Lerp con un número fijo, p. ej. 0.1,
    /// iría más rápido a 144 FPS que a 60.)
    /// </summary>
    private static float Suavizado(float velocidad, float dt) => 1f - Mathf.Exp(-velocidad * dt);
}
