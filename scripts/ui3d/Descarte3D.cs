using System;
using Godot;

namespace BarajaCartones.UI3D;

/// <summary>Montón de descartes: cartones apilados con un giro "aleatorio" fijo.</summary>
public partial class Descarte3D : Zona3D
{
    public int MaximoVisibles { get; set; } = 8;
    public int TotalDescartados { get; private set; }

    protected override void AlAgregar(Carton3D carton)
    {
        TotalDescartados++;
        carton.Interactivo = false;

        if (Cartones.Count > MaximoVisibles)
        {
            Carton3D viejo = Cartones[0];
            Quitar(viejo);
            viejo.QueueFree();
        }
    }

    protected override Colocacion3D Disposicion(int indice, int total, Carton3D carton)
    {
        var rng = new Random(carton.Datos.Id);
        float giro = Mathf.DegToRad((float)(rng.NextDouble() * 40 - 20));
        var desvio = new Vector3((float)(rng.NextDouble() * 0.5 - 0.25), 0, (float)(rng.NextDouble() * 0.5 - 0.25));

        // Apilados: cada uno un poco más alto que el anterior.
        var posicion = new Vector3(0, 0.001f + indice * Carton3D.Grosor * 1.3f, 0.3f) + desvio;
        return new Colocacion3D(posicion, new Quaternion(Vector3.Up, giro), 0.9f);
    }
}
