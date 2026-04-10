using DataFusionArena.Shared.Models;
using System.Globalization;

namespace DataFusionArena.Shared.Readers;

/// <summary>
/// Lee un archivo CSV y lo convierte a List&lt;DataItem&gt;.
/// Detecta automáticamente el orden de columnas por encabezado.
/// Si no encuentra columnas con nombres conocidos, usa fallback posicional
/// (columna 0 → ID, columna 1 → nombre, columna 2 → categoría, etc.).
/// </summary>
public static class CsvDataReader
{
    public static List<DataItem> Leer(string rutaArchivo, char separador = ',')
    {
        var lista = new List<DataItem>();

        if (!File.Exists(rutaArchivo))
        {
            Console.WriteLine($"[CSV] ⚠  Archivo no encontrado: {rutaArchivo}");
            return lista;
        }

        try
        {
            var lineas = File.ReadAllLines(rutaArchivo)
                             .Where(l => !string.IsNullOrWhiteSpace(l))
                             .ToArray();

            if (lineas.Length == 0) return lista;

            // Detectar separador automáticamente
            if (!lineas[0].Contains(separador) && lineas[0].Contains(';'))
                separador = ';';
            else if (!lineas[0].Contains(separador) && lineas[0].Contains('\t'))
                separador = '\t';

            // ── Primera línea = encabezados ──────────────────────────────
            var encabezados = lineas[0].Split(separador)
                                       .Select(h => h.Trim().ToLowerInvariant().Replace("\"", ""))
                                       .ToArray();

            var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < encabezados.Length; i++)
                mapa[encabezados[i]] = i;

            // Aliases conocidos
            int idxId = BuscarColumna(mapa, "id", "codigo", "code", "sku", "#");
            int idxNombre = BuscarColumna(mapa, "nombre", "name", "titulo", "title", "producto",
                                          "juego", "descripcion", "description", "player", "jugador",
                                          "employee", "empleado");
            int idxCat = BuscarColumna(mapa, "categoria", "category", "genero", "genre", "tipo",
                                          "type", "grupo", "group", "departamento", "department",
                                          "nivel", "level");
            int idxValor = BuscarColumna(mapa, "valor", "value", "precio", "price", "monto",
                                          "amount", "ventas", "score", "puntos", "points",
                                          "salario", "salary", "total");
            int idxFecha = BuscarColumna(mapa, "fecha", "date", "fecha_lanzamiento", "releasedate",
                                          "fecha_registro", "created_at", "timestamp");

            // ── Fallback posicional si no se encontraron columnas ────────
            // Asigna por posición ignorando las columnas ya mapeadas
            var mapeadas = new HashSet<int>(
                new[] { idxId, idxNombre, idxCat, idxValor, idxFecha }.Where(x => x >= 0));

            if (idxId < 0) idxId = SiguienteLibre(mapeadas, encabezados.Length, 0);
            if (idxNombre < 0) idxNombre = SiguienteLibre(mapeadas, encabezados.Length, 0);
            if (idxCat < 0) idxCat = SiguienteLibre(mapeadas, encabezados.Length, 0);
            if (idxValor < 0) idxValor = SiguienteLibre(mapeadas, encabezados.Length, 0);
            if (idxFecha < 0) idxFecha = SiguienteLibre(mapeadas, encabezados.Length, 0);

            Console.WriteLine($"[CSV] Columnas → ID:{idxId} Nombre:{idxNombre} Cat:{idxCat} Valor:{idxValor} Fecha:{idxFecha}");

            // ── Leer registros ───────────────────────────────────────────
            for (int fila = 1; fila < lineas.Length; fila++)
            {
                try
                {
                    var cols = SepararCsv(lineas[fila], separador);

                    var item = new DataItem { Fuente = "csv" };

                    item.Id = idxId >= 0 ? ParseInt(cols, idxId) : fila;
                    item.Nombre = idxNombre >= 0 ? Limpiar(cols, idxNombre) : $"Fila-{fila}";
                    item.Categoria = idxCat >= 0 ? Limpiar(cols, idxCat) : "Sin categoría";
                    item.Valor = idxValor >= 0 ? ParseDouble(cols, idxValor) : 0;
                    item.Fecha = idxFecha >= 0 ? ParseFecha(cols, idxFecha) : DateTime.Now;

                    // Columnas desconocidas → CamposExtra
                    var usadas = new HashSet<int> { idxId, idxNombre, idxCat, idxValor, idxFecha };
                    for (int c = 0; c < encabezados.Length; c++)
                    {
                        if (usadas.Contains(c)) continue;
                        if (c < cols.Length)
                            item.CamposExtra[encabezados[c]] = cols[c].Trim().Replace("\"", "");
                    }

                    lista.Add(item);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CSV] ⚠  Error en fila {fila + 1}: {ex.Message}");
                }
            }

            Console.WriteLine($"[CSV] ✓  {lista.Count} registros leídos desde {Path.GetFileName(rutaArchivo)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CSV] ✗  Error leyendo archivo: {ex.Message}");
        }

        return lista;
    }

    // ──────────────────────────────────────────────────────────────
    private static string[] SepararCsv(string linea, char sep)
    {
        var campos = new List<string>();
        bool enComillas = false;
        var actual = new System.Text.StringBuilder();

        foreach (char c in linea)
        {
            if (c == '"') { enComillas = !enComillas; continue; }
            if (c == sep && !enComillas) { campos.Add(actual.ToString()); actual.Clear(); }
            else actual.Append(c);
        }
        campos.Add(actual.ToString());
        return campos.ToArray();
    }

    private static int BuscarColumna(Dictionary<string, int> mapa, params string[] alias)
    {
        foreach (var a in alias)
            if (mapa.TryGetValue(a, out int idx)) return idx;
        return -1;
    }

    /// <summary>Devuelve el siguiente índice libre (no mapeado) en el rango dado.</summary>
    private static int SiguienteLibre(HashSet<int> mapeadas, int total, int desde)
    {
        for (int i = desde; i < total; i++)
            if (mapeadas.Add(i)) return i;
        return -1;
    }

    private static string Limpiar(string[] cols, int idx)
        => idx >= 0 && idx < cols.Length ? cols[idx].Trim().Replace("\"", "") : "";

    private static int ParseInt(string[] cols, int idx)
        => idx >= 0 && idx < cols.Length && int.TryParse(cols[idx].Trim(), out int v) ? v : 0;

    private static double ParseDouble(string[] cols, int idx)
        => idx >= 0 && idx < cols.Length && double.TryParse(cols[idx].Trim().Replace("\"", ""),
           NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0;

    private static DateTime ParseFecha(string[] cols, int idx)
        => idx >= 0 && idx < cols.Length && DateTime.TryParse(cols[idx].Trim(), out DateTime d) ? d : DateTime.Now;
}