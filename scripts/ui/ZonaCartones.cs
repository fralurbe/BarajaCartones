using System.Collections.Generic;
using Godot;

namespace MiGame.UI;

/// <summary>Dónde debe estar un cartón dentro de su zona.</summary>
public readonly record struct Colocacion(Vector2 Centro, float Rotacion, float Escala, int Z);

/// <summary>
/// Clase base de las zonas que contienen cartones (Mano, Mesa, Descarte).
///
/// Lo común (añadir, quitar, recolocar con animación, saber qué cartón
/// tiene el ratón encima) está aquí. Cada zona hija solo decide CÓMO se
/// colocan sus cartones sobrescribiendo Disposicion().
///
/// Herencia normal de C#: Mano, Mesa y Descarte heredan de esta clase,
/// que a su vez hereda de Control (un nodo de interfaz de Godot).
/// </summary>
public partial class ZonaCartones : Control
{
    [Export] public int Capacidad = 8;

    protected readonly List<CartonView> Cartones = new();
    protected CartonView Hover { get; private set; }

    public int Cantidad => Cartones.Count;
    public bool Llena => Cartones.Count >= Capacidad;

    public bool ContienePunto(Vector2 puntoGlobal) => GetGlobalRect().HasPoint(puntoGlobal);

    public void Agregar(CartonView carton)
    {
        carton.CambiarDePadre(this);
        Cartones.Add(carton);
        carton.HoverCambiado += AlCambiarHover;
        AlAgregar(carton);
        Reordenar();
    }

    public void Quitar(CartonView carton)
    {
        if (!Cartones.Remove(carton)) return;
        carton.HoverCambiado -= AlCambiarHover;
        if (Hover == carton) Hover = null;
        Reordenar();
    }

    /// <summary>Recoloca todos los cartones (con animación).</summary>
    public void Reordenar()
    {
        for (int i = 0; i < Cartones.Count; i++)
        {
            CartonView carton = Cartones[i];
            if (carton.Arrastrando) continue; // el que se arrastra va con el ratón
            Colocacion c = Disposicion(i, Cartones.Count, carton);
            carton.ZIndex = c.Z;
            carton.MoverA(c.Centro, c.Rotacion, c.Escala);
        }
    }

    /// <summary>Cada zona dice dónde va el cartón número "indice" de "total".</summary>
    protected virtual Colocacion Disposicion(int indice, int total, CartonView carton)
        => new(Size / 2, 0f, 1f, indice);

    /// <summary>Gancho opcional para las zonas hijas.</summary>
    protected virtual void AlAgregar(CartonView carton) { }

    private void AlCambiarHover(CartonView carton, bool encima)
    {
        if (encima) Hover = carton;
        else if (Hover == carton) Hover = null;
        Reordenar();
    }
}
