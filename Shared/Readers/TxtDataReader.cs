using DataFusionArena.Shared.Models;
using System.Globalization;

namespace DataFusionArena.Shared.Readers;

/// <summary>
/// Lee un archivo de texto plano con campos separados por | o tabulador.
/// También acepta texto libre línea por línea (sin estructura fija).
/// </summary>
public static class TxtDataReader
{
    public static List<DataItem> Leer(string rutaArchivo)
    {
        var lista = new List<DataItem>();

        if (!File.Exists(rutaArchivo))
        {
            Console.WriteLine($"[TXT] ⚠  Archivo no encontrado: {rutaArchivo}");
            return lista;
        }

        try
        {
            var lineas = File.ReadAllLines(rutaArchivo)
                             .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'))
                             .ToArray();

            if (lineas.Length == 0) return lista;

            // Detectar separador: | o \t o espacio
            char sep = '|';
            if (!lineas[0].Contains('|'))
                sep = lineas[0].Contains('\t') ? '\t' : ' ';

            // Detectar si tiene encabezado
            bool tieneEncabezado = lineas[0].Split(sep)[0].Trim().ToLower() is "id" or "#";
            int inicio = tieneEncabezado ? 1 : 0;

            int[] mapa = tieneEncabezado
                ? MapearColumnas(lineas[0], sep)
                : new[] { 0, 1, 2, 3, 4 };   // orden por defecto: id, nombre, cat, valor, fecha

            for (int i = inicio; i < lineas.Length; i++)
            {
                try
                {
                    var cols = lineas[i].Split(sep).Select(c => c.Trim()).ToArray();
                    if (cols.Length < 2) continue;

                    var item = new DataItem { Fuente = "txt" };

                    item.Id        = Leer<int>(cols, mapa[0], int.TryParse) ?? (i - inicio + 1);
                    item.Nombre    = cols.Length > mapa[1] ? cols[mapa[1]] : $"Registro-{i}";
                    item.Categoria = cols.Length > mapa[2] ? cols[mapa[2]] : "General";
                    item.Valor     = Leer<double>(cols, mapa[3],
                                        (string s, out double d) => double.TryParse(s, NumberStyles.Any,
                                        CultureInfo.InvariantCulture, out d)) ?? 0;
                    item.Fecha     = Leer<DateTime>(cols, mapa[4], DateTime.TryParse) ?? DateTime.Now;

                    lista.Add(item);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TXT] ⚠  Error en línea {i + 1}: {ex.Message}");
                }
            }

            Console.WriteLine($"[TXT] ✓  {lista.Count} registros leídos desde {Path.GetFileName(rutaArchivo)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TXT] ✗  Error leyendo TXT: {ex.Message}");
        }

        return lista;
    }

    // ──────────────────────────────────────────────────────────────
    private static int[] MapearColumnas(string encabezado, char sep)
    {
        var cols = encabezado.Split(sep).Select(c => c.Trim().ToLower()).ToArray();
        return new[]
        {
            BuscarIdx(cols, "id", "#"),
            BuscarIdx(cols, "nombre", "name", "jugador", "player"),
            BuscarIdx(cols, "categoria", "category", "tipo", "nivel"),
            BuscarIdx(cols, "valor", "value", "puntos", "score"),
            BuscarIdx(cols, "fecha", "date")
        };
    }

    private static int BuscarIdx(string[] cols, params string[] alias)
    {
        foreach (var a in alias)
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] == a) return i;
        return -1;
    }

    private delegate bool TryParseDelegate<T>(string s, out T result);

    private static T? Leer<T>(string[] cols, int idx, TryParseDelegate<T> parser) where T : struct
    {
        if (idx < 0 || idx >= cols.Length) return null;
        return parser(cols[idx], out T v) ? v : (T?)null;
    }
}
