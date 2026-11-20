using Godot;

namespace BarajaCartones.UI;

/// <summary>
/// La mano: cartones en abanico, en la parte de abajo de la pantalla.
///
/// Para cada cartón calculamos t = su distancia al centro de la mano
/// (..., -2, -1, 0, 1, 2, ...). Con t sacamos:
///   - X: t * separación
///   - Y: t² * curvatura   (parábola: los de los extremos bajan)
///   - rotación: t * grados (los de los extremos se abren)
/// </summary>
public partial class Mano : ZonaCartones
{
    [Export] public float Separacion = 200f;
    [Export] public float AnchoMaximo = 1050f;
    [Export] public float Curvatura = 8f;
    [Export] public float GradosPorCarton = 3.5f;
    [Export] public float EscalaNormal = 0.7f;
    [Export] public float EscalaHover = 0.95f;
    [Export] public float Empuje = 70f;          // cuánto se apartan los vecinos del que tiene el ratón
    [Export] public float MargenInferior = 15f;  // px entre el cartón central y el borde de abajo

    protected override Colocacion Disposicion(int indice, int total, CartonView carton)
    {
        // Si hay muchos cartones, se juntan para no pasar del ancho máximo.
        float separacion = total > 1 ? Mathf.Min(Separacion, AnchoMaximo / (total - 1)) : 0f;
        float t = indice - (total - 1) / 2f;

        float altoMitad = carton.Size.Y / 2f;
        float baseY = Size.Y - altoMitad * EscalaNormal - MargenInferior;

        var centro = new Vector2(Size.X / 2f + t * separacion, baseY + t * t * Curvatura);
        float rotacion = Mathf.DegToRad(t * GradosPorCarton);
        float escala = EscalaNormal;
        int z = indice;

        int indiceHover = Hover != null ? Cartones.IndexOf(Hover) : -1;
        if (indiceHover >= 0)
        {
            if (indice == indiceHover)
            {
                // El cartón crece hacia ARRIBA manteniendo el borde inferior
                // en el mismo sitio. Así el ratón sigue encima y no hay
                // parpadeo (si subiera de golpe, el ratón "saldría" del
                // cartón, bajaría, volvería a entrar...).
                escala = EscalaHover;
                rotacion = 0f;
                centro.Y -= altoMitad * (EscalaHover - EscalaNormal);
                z = 100;
            }
            else
            {
                centro.X += Mathf.Sign(indice - indiceHover) * Empuje;
            }
        }

        return new Colocacion(centro, rotacion, escala, z);
    }
}
