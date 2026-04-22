using DataFusionArena.Shared.Models;
using System.Globalization;

namespace DataFusionArena.Shared.Readers;

public static class CsvDataReader
{
    public static List<string> UltimasColumnas { get; private set; } = new();
    public static Dictionary<string, string> MapeoColumnas { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

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

            if (!lineas[0].Contains(separador) && lineas[0].Contains(';'))
                separador = ';';
            else if (!lineas[0].Contains(separador) && lineas[0].Contains('\t'))
                separador = '\t';

            var encabezados = lineas[0].Split(separador)
                                       .Select(h => h.Trim().ToLowerInvariant().Replace("\"", ""))
                                       .ToArray();

            var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < encabezados.Length; i++)
                mapa[encabezados[i]] = i;

            UltimasColumnas = lineas[0].Split(separador)
                                       .Select(h => h.Trim().Replace("\"", ""))
                                       .ToList();

            int idxId = BuscarColumna(mapa, "id", "car_id", "codigo", "code", "sku", "#");
            int idxNombre = BuscarColumna(mapa, "nombre", "name", "titulo", "title", "producto",
                                          "juego", "descripcion", "description", "player", "jugador",
                                          "employee", "empleado", "brand", "marca");
            int idxCat = BuscarColumna(mapa, "categoria", "category", "genero", "genre", "tipo",
                                          "type", "grupo", "group", "departamento", "department",
                                          "nivel", "level", "fuel_type", "transmission");
            int idxValor = BuscarColumna(mapa, "valor", "value", "precio", "price", "monto",
                                          "amount", "ventas", "score", "puntos", "points",
                                          "salario", "salary", "total", "price");

            int idxFecha = BuscarColumnaExacta(mapa,
                "fecha", "date", "fecha_lanzamiento", "releasedate",
                "fecha_registro", "created_at", "updated_at", "timestamp", "anio", "model_year");

            var mapeadas = new HashSet<int>(
                new[] { idxId, idxNombre, idxCat, idxValor, idxFecha }.Where(x => x >= 0));

            // Solo aplicar fallback ciego a id y nombre (posicionales).
            // Categoría y valor NO usan fallback posicional: asignar una columna
            // numérica aleatoria a categoría produce resultados incorrectos.
            if (idxId < 0) idxId = SiguienteLibre(mapeadas, encabezados.Length, 0);
            if (idxNombre < 0) idxNombre = SiguienteLibre(mapeadas, encabezados.Length, 0);
            // idxCat: si no coincidió por alias, se deja en -1 → "Sin categoría"
            // idxValor: si no coincidió por alias, intentar la primera columna numérica disponible
            if (idxValor < 0) idxValor = BuscarPrimeraNumerica(lineas, encabezados, separador, mapeadas);

            MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (idxId >= 0 && idxId < encabezados.Length) MapeoColumnas[encabezados[idxId]] = "Id";
            if (idxNombre >= 0 && idxNombre < encabezados.Length) MapeoColumnas[encabezados[idxNombre]] = "Nombre";
            if (idxCat >= 0 && idxCat < encabezados.Length) MapeoColumnas[encabezados[idxCat]] = "Categoria";
            if (idxValor >= 0 && idxValor < encabezados.Length) MapeoColumnas[encabezados[idxValor]] = "Valor";
            if (idxFecha >= 0 && idxFecha < encabezados.Length) MapeoColumnas[encabezados[idxFecha]] = "Fecha";

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
                    item.Fecha = idxFecha >= 0 ? ParseFechaEspecial(cols, idxFecha, encabezados[idxFecha]) : DateTime.Now;

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

    private static DateTime ParseFechaEspecial(string[] cols, int idx, string nombreColumna)
    {
        if (idx < 0 || idx >= cols.Length) return DateTime.Now;

        string valor = cols[idx].Trim();
        string colLower = nombreColumna.ToLower();

        if (colLower is "anio" or "año" or "year" or "model_year")
        {
            if (int.TryParse(valor, out int anio))
                return new DateTime(anio, 1, 1);
        }

        if (DateTime.TryParse(valor, out DateTime d))
            return d;

        return DateTime.Now;
    }

    private static int BuscarColumnaExacta(Dictionary<string, int> mapa, params string[] alias)
    {
        foreach (var a in alias)
            if (mapa.TryGetValue(a, out int idx)) return idx;
        return -1;
    }

    /// <summary>
    /// Busca la primera columna (no ya mapeada) cuyos valores en las primeras filas
    /// sean mayoritariamente numéricos. Evita asignar columnas de texto a "valor".
    /// </summary>
    private static int BuscarPrimeraNumerica(string[] lineas, string[] encabezados,
        char sep, HashSet<int> mapeadas)
    {
        int filasMuestra = Math.Min(10, lineas.Length - 1);
        if (filasMuestra <= 0) return -1;

        for (int col = 0; col < encabezados.Length; col++)
        {
            if (mapeadas.Contains(col)) continue;

            int numericos = 0;
            for (int fila = 1; fila <= filasMuestra; fila++)
            {
                var cols = lineas[fila].Split(sep);
                if (col >= cols.Length) continue;
                string val = cols[col].Trim().Replace("\"", "");
                if (double.TryParse(val, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                    numericos++;
            }

            if (numericos >= filasMuestra * 0.7)
            {
                mapeadas.Add(col);
                return col;
            }
        }
        return -1;
    }
}