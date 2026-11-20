using Godot;

namespace MiGame.UI;

/// <summary>La mesa: los cartones jugados se colocan en una rejilla.</summary>
public partial class Mesa : ZonaCartones
{
    [Export] public int ColumnasRejilla = 4;
    [Export] public Vector2 Espaciado = new(290f, 140f);
    [Export] public float Escala = 0.62f;
    [Export] public float EscalaHover = 0.68f;
    [Export] public float MargenSuperior = 50f;

    protected override Colocacion Disposicion(int indice, int total, CartonView carton)
    {
        int columna = indice % ColumnasRejilla;
        int fila = indice / ColumnasRejilla;

        // Centrar horizontalmente la fila (aunque esté incompleta).
        int enEstaFila = Mathf.Min(ColumnasRejilla, total - fila * ColumnasRejilla);
        float x = Size.X / 2f + (columna - (enEstaFila - 1) / 2f) * Espaciado.X;
        float y = MargenSuperior + Espaciado.Y / 2f + fila * Espaciado.Y;

        bool hover = carton == Hover;
        return new Colocacion(new Vector2(x, y), 0f, hover ? EscalaHover : Escala, hover ? 100 : indice);
    }
}
