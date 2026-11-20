using System;
using Godot;

namespace MiGame.UI;

/// <summary>
/// El descarte: un montón desordenado. Los cartones descartados ya no se
/// pueden coger. Solo se dejan visibles los últimos; los más antiguos se
/// borran del árbol con QueueFree() para no acumular nodos.
/// </summary>
public partial class Descarte : ZonaCartones
{
    [Export] public float Escala = 0.4f;
    [Export] public int MaximoVisibles = 6;

    public int TotalDescartados { get; private set; }

    protected override void AlAgregar(CartonView carton)
    {
        TotalDescartados++;
        carton.Interactivo = false;
        // MOUSE_FILTER_IGNORE: el ratón "atraviesa" este control.
        carton.MouseFilter = MouseFilterEnum.Ignore;
        carton.Modulate = new Color(0.85f, 0.85f, 0.85f);

        if (Cartones.Count > MaximoVisibles)
        {
            CartonView viejo = Cartones[0];
            Quitar(viejo);
            viejo.QueueFree(); // se borra al final del frame
        }
    }

    protected override Colocacion Disposicion(int indice, int total, CartonView carton)
    {
        // Desorden "fijo": usamos el Id como semilla, así cada cartón
        // tiene siempre el mismo giro y no baila al recolocar.
        var rng = new Random(carton.Datos.Id);
        float rotacion = Mathf.DegToRad((float)(rng.NextDouble() * 24 - 12));
        var desvio = new Vector2((float)(rng.NextDouble() * 30 - 15), (float)(rng.NextDouble() * 30 - 15));
        var centro = Size / 2f + new Vector2(0, 20) + desvio;
        return new Colocacion(centro, rotacion, Escala, indice);
    }
}
