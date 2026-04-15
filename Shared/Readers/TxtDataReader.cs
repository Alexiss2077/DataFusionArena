using DataFusionArena.Shared.Models;
using System.Globalization;

namespace DataFusionArena.Shared.Readers;

public static class TxtDataReader
{
    public static List<string> UltimasColumnas { get; private set; } = new();
    public static Dictionary<string, string> MapeoColumnas { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public static List<DataItem> Leer(string rutaArchivo)
    {
        var lista = new List<DataItem>();
        UltimasColumnas = new List<string>();
        MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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

            char sep = '|';
            if (!lineas[0].Contains('|'))
                sep = lineas[0].Contains('\t') ? '\t' : ' ';

            bool tieneEncabezado = lineas[0].Split(sep)[0].Trim().ToLower() is "id" or "#";
            int inicio = tieneEncabezado ? 1 : 0;

            int[] mapa;
            if (tieneEncabezado)
            {
                mapa = MapearColumnas(lineas[0], sep);
                DetectarMetadatos(lineas[0], sep, mapa);
            }
            else
            {
                mapa = new[] { 0, 1, 2, 3, 4 };
            }

            for (int i = inicio; i < lineas.Length; i++)
            {
                try
                {
                    var cols = lineas[i].Split(sep).Select(c => c.Trim()).ToArray();
                    if (cols.Length < 2) continue;

                    var item = new DataItem { Fuente = "txt" };

                    item.Id = Leer<int>(cols, mapa[0], int.TryParse) ?? (i - inicio + 1);
                    item.Nombre = cols.Length > mapa[1] && mapa[1] >= 0 ? cols[mapa[1]] : $"Registro-{i}";
                    item.Categoria = cols.Length > mapa[2] && mapa[2] >= 0 ? cols[mapa[2]] : "General";
                    item.Valor = Leer<double>(cols, mapa[3],
                                        (string s, out double d) => double.TryParse(s, NumberStyles.Any,
                                        CultureInfo.InvariantCulture, out d)) ?? 0;
                    item.Fecha = Leer<DateTime>(cols, mapa[4], DateTime.TryParse) ?? DateTime.Now;

                    if (tieneEncabezado)
                    {
                        var headers = lineas[0].Split(sep).Select(h => h.Trim()).ToArray();
                        var usados = new HashSet<int> { mapa[0], mapa[1], mapa[2], mapa[3], mapa[4] };
                        for (int c = 0; c < headers.Length && c < cols.Length; c++)
                        {
                            if (usados.Contains(c)) continue;
                            item.CamposExtra[headers[c].ToLowerInvariant()] = cols[c];
                        }
                    }

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

    private static void DetectarMetadatos(string encabezado, char sep, int[] mapa)
    {
        var cols = encabezado.Split(sep).Select(c => c.Trim()).ToArray();

        UltimasColumnas = cols.ToList();
        MapeoColumnas.Clear();

        if (mapa[0] >= 0 && mapa[0] < cols.Length) MapeoColumnas[cols[mapa[0]]] = "id";
        if (mapa[1] >= 0 && mapa[1] < cols.Length) MapeoColumnas[cols[mapa[1]]] = "nombre";
        if (mapa[2] >= 0 && mapa[2] < cols.Length) MapeoColumnas[cols[mapa[2]]] = "categoria";
        if (mapa[3] >= 0 && mapa[3] < cols.Length) MapeoColumnas[cols[mapa[3]]] = "valor";
        if (mapa[4] >= 0 && mapa[4] < cols.Length) MapeoColumnas[cols[mapa[4]]] = "fecha";
    }

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