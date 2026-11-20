using Godot;

namespace BarajaCartones.UI3D;

/// <summary>
/// La mano en 3D: cartones flotando delante de la cámara, inclinados hacia
/// ella y abiertos en abanico. Misma idea que en 2D (t = distancia al
/// centro), pero ahora el abanico es una rotación alrededor de la normal
/// del cartón.
/// </summary>
public partial class Mano3D : Zona3D
{
    public Camera3D Camara { get; set; }

    public float Separacion { get; set; } = 1.45f;
    public float AnchoMaximo { get; set; } = 7f;
    public float Curvatura { get; set; } = 0.06f;
    public float GradosAbanico { get; set; } = 5f;
    public float Inclinacion { get; set; } = 48f;   // grados hacia la cámara
    public float Escala { get; set; } = 0.8f;
    public float EscalaHover { get; set; } = 1.0f;
    public float Acercar { get; set; } = 1.2f;      // cuánto se acerca a la cámara con el ratón encima
    public float Empuje { get; set; } = 0.45f;

    /// <summary>Se considera "soltar en la mano" cualquier punto de la parte delantera de la mesa.</summary>
    public override bool ContienePunto(Vector3 puntoGlobal) => puntoGlobal.Z > GlobalPosition.Z - 1.6f;

    protected override Colocacion3D Disposicion(int indice, int total, Carton3D carton)
    {
        float separacion = total > 1 ? Mathf.Min(Separacion, AnchoMaximo / (total - 1)) : 0f;
        float t = indice - (total - 1) / 2f;

        // Rotación: primero el giro del abanico (alrededor de su propia
        // normal, Y local) y después inclinarlo hacia la cámara (eje X).
        // Con quaterniones, "A * B" = aplica B y luego A.
        var inclinar = new Quaternion(Vector3.Right, Mathf.DegToRad(Inclinacion));
        var abanico = new Quaternion(Vector3.Up, Mathf.DegToRad(-t * GradosAbanico));
        Quaternion rotacion = inclinar * abanico;

        var posicion = new Vector3(t * separacion, -t * t * Curvatura, 0f);
        // Cada cartón un poco por encima del anterior (en su normal) para
        // que se solapen sin "pelearse" por el mismo plano (z-fighting).
        posicion += rotacion * Vector3.Up * (indice * 0.015f);

        float escala = Escala;
        int indiceHover = Hover != null ? Cartones.IndexOf(Hover) : -1;
        if (indiceHover >= 0)
        {
            if (indice == indiceHover)
            {
                // Se acerca a la cámara EN LÍNEA RECTA hacia ella: así sigue
                // bajo el ratón (no parpadea) y se ve más grande.
                rotacion = inclinar;
                Vector3 global = ToGlobal(posicion);
                Vector3 haciaCamara = (Camara.GlobalPosition - global).Normalized();
                posicion += haciaCamara * Acercar;
                escala = EscalaHover;
            }
            else
            {
                posicion.X += Mathf.Sign(indice - indiceHover) * Empuje;
            }
        }

        return new Colocacion3D(posicion, rotacion, escala);
    }
}
