using Godot;
using BarajaCartones.Bingo;

namespace BarajaCartones.UI;

/// <summary>
/// Dibuja la "cara" del cartón: fondo, cabecera y la rejilla 3x9.
///
/// Usa dibujo inmediato: se sobrescribe _Draw() y se llaman funciones
/// Draw*. Godot llama a _Draw() solo cuando hace falta (al aparecer o
/// tras QueueRedraw()), no en cada frame.
///
/// Este nodo vive DENTRO de un SubViewport (ver carton.tscn). No se ve
/// directamente en pantalla: el SubViewport lo pinta en una textura, y
/// esa textura es la que se muestra con el shader de inclinación.
/// </summary>
public partial class CaraCarton : Control
{
    public const int Celda = 44;
    public const int Margen = 12;
    public const int Cabecera = 32;

    // Tamaño total: 12 + 9*44 + 12 = 420  x  32 + 3*44 + 12 = 176
    public static readonly Vector2 Tamano = new(
        Margen * 2 + CartonBingo.Columnas * Celda,
        Cabecera + CartonBingo.Filas * Celda + Margen);

    private CartonBingo _carton;
    private Color _color = Colors.SteelBlue;

    public void Mostrar(CartonBingo carton, Color color)
    {
        _carton = carton;
        _color = color;
        QueueRedraw(); // pide que se vuelva a llamar a _Draw()
    }

    public override void _Draw()
    {
        if (_carton == null) return;

        Font fuente = ThemeDB.FallbackFont; // la fuente por defecto de Godot

        // Fondo con esquinas redondeadas. StyleBoxFlat es el mismo recurso
        // que usan los paneles y botones de la interfaz.
        var fondo = new StyleBoxFlat
        {
            BgColor = _color,
            BorderColor = _color.Darkened(0.45f),
        };
        fondo.SetCornerRadiusAll(14);
        fondo.SetBorderWidthAll(4);
        DrawStyleBox(fondo, new Rect2(Vector2.Zero, Tamano));

        DrawString(fuente, new Vector2(Margen + 4, 24), $"CARTÓN Nº {_carton.Id:000}",
            HorizontalAlignment.Left, -1, 18, Colors.White);

        const int tamTexto = 22;
        var origen = new Vector2(Margen, Cabecera);
        for (int f = 0; f < CartonBingo.Filas; f++)
        {
            for (int c = 0; c < CartonBingo.Columnas; c++)
            {
                var celda = new Rect2(origen + new Vector2(c * Celda, f * Celda), new Vector2(Celda, Celda))
                    .Grow(-2); // deja 2 px de separación entre casillas

                int n = _carton.Numero(f, c);
                if (n == 0)
                {
                    DrawRect(celda, _color.Darkened(0.25f));
                    continue;
                }

                DrawRect(celda, new Color(0.97f, 0.95f, 0.88f));

                // DrawString recibe la posición de la LÍNEA BASE del texto,
                // por eso calculamos la Y con el ascent/descent de la fuente.
                float baseY = celda.Position.Y
                    + (celda.Size.Y + fuente.GetAscent(tamTexto) - fuente.GetDescent(tamTexto)) / 2f;
                DrawString(fuente, new Vector2(celda.Position.X, baseY), n.ToString(),
                    HorizontalAlignment.Center, celda.Size.X, tamTexto, Colors.Black);

                if (_carton.EstaMarcado(f, c))
                    DrawCircle(celda.GetCenter(), Celda * 0.38f, new Color(0.9f, 0.1f, 0.2f, 0.55f));
            }
        }
    }
}
