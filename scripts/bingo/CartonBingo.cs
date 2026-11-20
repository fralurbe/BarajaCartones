using System;
using System.Collections.Generic;
using System.Linq;

namespace BarajaCartones.Bingo;

/// <summary>
/// Datos de un cartón de bingo de 90 bolas (3 filas x 9 columnas).
///
/// IMPORTANTE: esta clase NO usa nada de Godot (no hay "using Godot").
/// Es C# puro. Así la lógica es independiente de cómo se dibuje:
/// la versión 2D y una futura versión 3D usarán exactamente esta clase.
///
/// Reglas del cartón español de 90 bolas:
///  - 15 números en total, 5 por fila.
///  - Cada columna tiene entre 1 y 3 números.
///  - Columna 0: 1-9, columna 1: 10-19, ... columna 8: 80-90.
///  - Dentro de una columna, los números van de menor a mayor hacia abajo.
/// </summary>
public class CartonBingo
{
    public const int Filas = 3;
    public const int Columnas = 9;
    public const int NumerosPorFila = 5;

    public int Id { get; }

    // 0 = casilla vacía.
    private readonly int[,] _numeros = new int[Filas, Columnas];
    private readonly bool[,] _marcados = new bool[Filas, Columnas];

    private CartonBingo(int id) => Id = id;

    public int Numero(int fila, int columna) => _numeros[fila, columna];
    public bool EstaMarcado(int fila, int columna) => _marcados[fila, columna];

    /// <summary>Marca el número si está en el cartón. Devuelve true si estaba.</summary>
    public bool Marcar(int numero)
    {
        for (int f = 0; f < Filas; f++)
            for (int c = 0; c < Columnas; c++)
                if (_numeros[f, c] == numero)
                {
                    _marcados[f, c] = true;
                    return true;
                }
        return false;
    }

    /// <summary>Rango de números posibles de una columna.</summary>
    public static (int min, int max) RangoColumna(int columna) => columna switch
    {
        0 => (1, 9),
        8 => (80, 90),
        _ => (columna * 10, columna * 10 + 9),
    };

    public static CartonBingo Generar(int id, Random rng)
    {
        var carton = new CartonBingo(id);

        // 1) Cuántos números tiene cada columna: todas al menos 1 (9 en total),
        //    y repartimos los 6 que faltan hasta 15 sin pasar de 3 por columna.
        int[] porColumna = Enumerable.Repeat(1, Columnas).ToArray();
        int faltan = Filas * NumerosPorFila - Columnas;
        while (faltan > 0)
        {
            int c = rng.Next(Columnas);
            if (porColumna[c] < Filas)
            {
                porColumna[c]++;
                faltan--;
            }
        }

        // 2) En qué filas va cada número. Cada fila necesita 5.
        //    Algoritmo voraz: para cada columna elegimos las filas a las que
        //    MÁS números les faltan (desempate aleatorio). Con estas reglas
        //    siempre da un reparto válido.
        int[] faltanEnFila = Enumerable.Repeat(NumerosPorFila, Filas).ToArray();
        var filasDeColumna = new List<int>[Columnas];
        var ordenColumnas = Enumerable.Range(0, Columnas)
            .OrderByDescending(c => porColumna[c])
            .ThenBy(_ => rng.Next());

        foreach (int c in ordenColumnas)
        {
            var filas = Enumerable.Range(0, Filas)
                .OrderByDescending(f => faltanEnFila[f])
                .ThenBy(_ => rng.Next())
                .Take(porColumna[c])
                .OrderBy(f => f)
                .ToList();
            foreach (int f in filas) faltanEnFila[f]--;
            filasDeColumna[c] = filas;
        }

        // 3) Elegir los números de cada columna (sin repetir, ordenados).
        for (int c = 0; c < Columnas; c++)
        {
            var (min, max) = RangoColumna(c);
            var numeros = Enumerable.Range(min, max - min + 1)
                .OrderBy(_ => rng.Next())
                .Take(porColumna[c])
                .OrderBy(n => n)
                .ToList();
            for (int i = 0; i < numeros.Count; i++)
                carton._numeros[filasDeColumna[c][i], c] = numeros[i];
        }

        return carton;
    }
}
