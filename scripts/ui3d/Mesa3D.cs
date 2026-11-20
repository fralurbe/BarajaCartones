using Godot;

namespace BarajaCartones.UI3D;

/// <summary>La mesa: cartones tumbados en una rejilla de 3 columnas.</summary>
public partial class Mesa3D : Zona3D
{
    public int Columnas { get; set; } = 3;
    public Vector2 Espaciado { get; set; } = new(2.95f, 1.3f);
    public float MargenSuperior { get; set; } = 0.75f;

    protected override Colocacion3D Disposicion(int indice, int total, Carton3D carton)
    {
        int columna = indice % Columnas;
        int fila = indice / Columnas;
        int enEstaFila = Mathf.Min(Columnas, total - fila * Columnas);

        float x = (columna - (enEstaFila - 1) / 2f) * Espaciado.X;
        float z = -Tamano.Y / 2f + MargenSuperior + Espaciado.Y / 2f + fila * Espaciado.Y;

        bool hover = carton == Hover;
        // Con el ratón encima se levanta un poco: con la luz del sol se ve
        // su sombra en el tapete.
        var posicion = new Vector3(x, hover ? 0.15f : 0.001f, z);
        return new Colocacion3D(posicion, Quaternion.Identity, hover ? 1.04f : 1f);
    }
}
