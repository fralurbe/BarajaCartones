using System;
using System.Threading.Tasks;
using Godot;
using BarajaCartones.Bingo;
using BarajaCartones.UI;

namespace BarajaCartones;

/// <summary>
/// Nodo raíz de la escena principal. Hace de "director":
///  - crea cartones (instanciando la escena carton.tscn),
///  - escucha cuándo se suelta un cartón y decide a qué zona va.
/// </summary>
public partial class Juego : Control
{
    // [Export] de tipo PackedScene: en el inspector se arrastra aquí
    // carton.tscn (ya está asignado en juego.tscn).
    [Export] public PackedScene EscenaCarton;
    [Export] public int CartonesIniciales = 5;

    private Mano _mano;
    private Mesa _mesa;
    private Descarte _descarte;
    private Button _botonRobar;
    private Label _contadorDescarte;

    private readonly Random _rng = new();
    private int _siguienteId = 1;

    public override void _Ready()
    {
        _mano = GetNode<Mano>("Mano");
        _mesa = GetNode<Mesa>("Mesa");
        _descarte = GetNode<Descarte>("Descarte");
        _botonRobar = GetNode<Button>("BotonRobar");
        _contadorDescarte = GetNode<Label>("Descarte/Contador");

        _botonRobar.Pressed += RobarCarton;

        // "_ =" porque no esperamos a que termine: corre por su cuenta.
        _ = RepartirInicial();
    }

    /// <summary>
    /// Reparte los cartones uno a uno con una pequeña pausa.
    /// ToSignal + await: espera a que un temporizador emita "timeout"
    /// sin bloquear el juego.
    /// </summary>
    private async Task RepartirInicial()
    {
        for (int i = 0; i < CartonesIniciales; i++)
        {
            RobarCarton();
            await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
        }
    }

    private void RobarCarton()
    {
        if (_mano.Llena) return;

        var datos = CartonBingo.Generar(_siguienteId++, _rng);
        var color = Color.FromHsv((float)_rng.NextDouble(), 0.55f, 0.75f);

        // Instantiate crea una copia nueva de la escena carton.tscn.
        var carton = EscenaCarton.Instantiate<CartonView>();
        carton.Configurar(datos, color);
        carton.Soltado += AlSoltarCarton;

        // Que salga del botón de robar, pequeño, y vuele a la mano.
        _mano.AddChild(carton);
        carton.SituarCentroGlobal(_botonRobar.GetGlobalRect().GetCenter());
        carton.Scale = Vector2.One * 0.2f;
        _mano.Agregar(carton);

        ActualizarInterfaz();
    }

    private void AlSoltarCarton(CartonView carton, Vector2 posicion)
    {
        var origen = carton.GetParent<ZonaCartones>();

        ZonaCartones destino = null;
        if (_mesa.ContienePunto(posicion)) destino = _mesa;
        else if (_descarte.ContienePunto(posicion)) destino = _descarte;
        else if (_mano.ContienePunto(posicion)) destino = _mano;

        if (destino == null || destino == origen || destino.Llena)
        {
            // No vale: vuelve a su sitio con la animación de Reordenar.
            origen.Reordenar();
            return;
        }

        origen.Quitar(carton);
        destino.Agregar(carton);
        ActualizarInterfaz();
    }

    private void ActualizarInterfaz()
    {
        _botonRobar.Disabled = _mano.Llena;
        _botonRobar.Text = $"Robar cartón\n({_mano.Cantidad}/{_mano.Capacidad})";
        _contadorDescarte.Text = $"Descartados: {_descarte.TotalDescartados}";
    }
}
