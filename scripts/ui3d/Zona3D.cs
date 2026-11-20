using System.Collections.Generic;
using Godot;

namespace BarajaCartones.UI3D;

/// <summary>Dónde debe estar un cartón, en coordenadas LOCALES de la zona.</summary>
public readonly record struct Colocacion3D(Vector3 Posicion, Quaternion Rotacion, float Escala);

/// <summary>
/// Clase base de Mano3D, Mesa3D y Descarte3D. Igual que en 2D, cada zona
/// solo decide CÓMO se colocan sus cartones (Disposicion).
///
/// Diferencia con 2D: los cartones NO son hijos de la zona (todos cuelgan
/// del nodo "Cartones"). La zona solo guarda una lista y les dice adónde
/// ir. Así no hay que cambiar de padre al mover un cartón entre zonas.
///
/// La zona es un rectángulo sobre la mesa (plano XZ) de tamaño Tamano,
/// centrado en la posición del nodo.
/// </summary>
public partial class Zona3D : Node3D
{
    public Vector2 Tamano { get; set; } = new(4f, 4f);
    public string Titulo { get; set; } = "";
    public int Capacidad { get; set; } = 8;

    protected readonly List<Carton3D> Cartones = new();
    public Carton3D Hover { get; private set; }

    public int Cantidad => Cartones.Count;
    public bool Llena => Cartones.Count >= Capacidad;

    public override void _Ready()
    {
        if (string.IsNullOrEmpty(Titulo)) return;

        // Un rectángulo un poco más claro sobre el tapete...
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(Tamano.X, 0.004f, Tamano.Y) },
            Position = new Vector3(0, 0.002f, 0),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0, 0.05f, 0.02f, 0.25f), // oscurece un poco el tapete
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 1f,
            },
        });

        // ...y un texto 3D tumbado sobre la mesa.
        AddChild(new Label3D
        {
            Text = Titulo,
            FontSize = 64,
            PixelSize = 0.005f,
            HorizontalAlignment = HorizontalAlignment.Left,
            RotationDegrees = new Vector3(-90, 0, 0), // tumbado: mira hacia arriba
            Position = new Vector3(-Tamano.X / 2f + 0.2f, 0.01f, -Tamano.Y / 2f + 0.3f),
            Modulate = new Color(1, 1, 1, 0.55f),
        });
    }

    /// <summary>¿Está este punto de la mesa dentro de la zona?</summary>
    public virtual bool ContienePunto(Vector3 puntoGlobal)
    {
        Vector3 local = ToLocal(puntoGlobal);
        return Mathf.Abs(local.X) <= Tamano.X / 2f && Mathf.Abs(local.Z) <= Tamano.Y / 2f;
    }

    public void Agregar(Carton3D carton)
    {
        Cartones.Add(carton);
        carton.Zona = this;
        AlAgregar(carton);
        Reordenar();
    }

    public void Quitar(Carton3D carton)
    {
        if (!Cartones.Remove(carton)) return;
        if (Hover == carton) Hover = null;
        carton.Zona = null;
        Reordenar();
    }

    /// <summary>Juego3D avisa de qué cartón tiene el ratón encima (puede no ser de esta zona).</summary>
    public void EstablecerHover(Carton3D carton)
    {
        Carton3D nuevo = carton != null && carton.Zona == this ? carton : null;
        if (nuevo == Hover) return;
        Hover = nuevo;
        Reordenar();
    }

    public void Reordenar()
    {
        for (int i = 0; i < Cartones.Count; i++)
        {
            Carton3D carton = Cartones[i];
            if (carton.Arrastrando) continue;
            Colocacion3D c = Disposicion(i, Cartones.Count, carton);
            // De local (zona) a global. Las zonas no están giradas, así que
            // la rotación local sirve tal cual.
            carton.MoverA(ToGlobal(c.Posicion), c.Rotacion, c.Escala);
        }
    }

    protected virtual Colocacion3D Disposicion(int indice, int total, Carton3D carton)
        => new(Vector3.Zero, Quaternion.Identity, 1f);

    protected virtual void AlAgregar(Carton3D carton) { }
}
