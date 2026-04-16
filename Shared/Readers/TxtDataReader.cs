using DataFusionArena.Shared.Models;
using System.Globalization;

namespace DataFusionArena.Shared.Readers;

/// <summary>
/// Lector TXT robusto: detecta separador y encabezado automáticamente,
/// mapea columnas por coincidencia flexible (exacta → inicia con → contiene),
/// y almacena TODOS los campos en CamposExtra para acceso confiable por nombre.
/// </summary>
public static class TxtDataReader
{
    public static List<string> UltimasColumnas { get; private set; } = new();
    public static Dictionary<string, string> MapeoColumnas { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    // ── Aliases para campos estándar de DataItem ─────────────────
    private static readonly string[] _idAlias = {
        "id", "#", "num", "numero", "número", "index", "idx",
        "codigo", "código", "code", "no", "nro", "rank"
    };
    private static readonly string[] _nombreAlias = {
        "nombre", "name", "titulo", "título", "title",
        "jugador", "player", "athlete", "atleta",
        "producto", "item", "descripcion", "descripción", "description",
        "empleado", "employee", "persona", "person", "autor", "author"
    };
    private static readonly string[] _catAlias = {
        "categoria", "categoría", "category", "genero", "género", "genre",
        "tipo", "type", "nivel", "level", "sport", "deporte",
        "grupo", "group", "departamento", "department",
        "clasificacion", "clasificación", "clase", "class",
        "division", "región", "region", "pais", "country", "país"
    };
    private static readonly string[] _valorAlias = {
        "valor", "value", "puntos", "score", "mark", "record",
        "tiempo", "time", "precio", "price", "monto", "amount",
        "ventas", "sales", "total", "distancia", "distance",
        "resultado", "result", "medida", "measure", "metros", "segundos",
        "salario", "salary", "sueldo"
    };
    private static readonly string[] _fechaAlias = {
        "fecha", "date", "año", "anio", "year", "periodo", "period",
        "timestamp", "created_at", "updated_at", "fecha_registro",
        "release", "publicacion", "publicación", "lanzamiento"
    };

    // ════════════════════════════════════════════════════════════
    //  LECTURA PRINCIPAL
    // ════════════════════════════════════════════════════════════
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
            // Filtrar comentarios y vacías
            var lineas = File.ReadAllLines(rutaArchivo)
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'))
                .ToArray();

            if (lineas.Length == 0) return lista;

            // 1. Detectar separador
            char sep = DetectarSeparador(lineas);

            // 2. Detectar encabezado
            string[] primeraFila = Separar(lineas[0], sep);
            bool tieneEncabezado = EsEncabezado(primeraFila);
            int inicio = tieneEncabezado ? 1 : 0;

            // 3. Construir headers
            string[] headers = tieneEncabezado
                ? primeraFila
                : Enumerable.Range(1, primeraFila.Length).Select(i => $"col{i}").ToArray();

            // 4. Mapear headers → campos DataItem
            int[] mapa = MapearColumnas(headers);

            // 5. Guardar metadatos para consola y WinForms
            UltimasColumnas = headers.ToList();
            MapeoColumnas.Clear();
            if (mapa[0] >= 0 && mapa[0] < headers.Length) MapeoColumnas[headers[mapa[0]]] = "id";
            if (mapa[1] >= 0 && mapa[1] < headers.Length) MapeoColumnas[headers[mapa[1]]] = "nombre";
            if (mapa[2] >= 0 && mapa[2] < headers.Length) MapeoColumnas[headers[mapa[2]]] = "categoria";
            if (mapa[3] >= 0 && mapa[3] < headers.Length) MapeoColumnas[headers[mapa[3]]] = "valor";
            if (mapa[4] >= 0 && mapa[4] < headers.Length) MapeoColumnas[headers[mapa[4]]] = "fecha";

            Console.WriteLine($"[TXT] Sep='{EscapeSep(sep)}' | Encabezado={tieneEncabezado}" +
                $" | Columnas={headers.Length}" +
                $" | id@{mapa[0]} nombre@{mapa[1]} cat@{mapa[2]} val@{mapa[3]} fecha@{mapa[4]}");

            // 6. Leer filas
            int rowNum = 0;
            for (int i = inicio; i < lineas.Length; i++)
            {
                try
                {
                    string[] cols = Separar(lineas[i], sep);
                    if (cols.Length < 1) continue;
                    rowNum++;

                    var item = new DataItem { Fuente = "txt" };

                    // Campos estándar
                    item.Id = ValorInt(cols, mapa[0]) ?? rowNum;
                    item.Nombre = ValorStr(cols, mapa[1]) ?? $"Fila-{rowNum}";
                    item.Categoria = ValorStr(cols, mapa[2]) ?? "";
                    item.Valor = ValorDouble(cols, mapa[3]) ?? 0;
                    item.Fecha = ValorFecha(cols, mapa[4]) ?? DateTime.Now;

                    // TODOS los campos → CamposExtra (clave = nombre columna en minúscula)
                    // Esto garantiza acceso confiable por nombre original de columna
                    for (int c = 0; c < headers.Length; c++)
                    {
                        string key = headers[c].ToLowerInvariant();
                        item.CamposExtra[key] = c < cols.Length ? cols[c] : "";
                    }

                    lista.Add(item);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TXT] ⚠  Error línea {i + 1}: {ex.Message}");
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

    // ════════════════════════════════════════════════════════════
    //  DETECCIÓN DE SEPARADOR
    // ════════════════════════════════════════════════════════════
    private static char DetectarSeparador(string[] lineas)
    {
        char[] candidatos = { '|', '\t', ';', ',' };
        // Muestra: primeras 6 líneas (o todas si hay menos)
        string[] muestra = lineas.Take(Math.Min(6, lineas.Length)).ToArray();

        char mejor = '|';
        int mejorScore = -1;

        foreach (char sep in candidatos)
        {
            int[] conteos = muestra.Select(l => l.Split(sep).Length - 1).ToArray();
            if (conteos[0] == 0) continue;

            // Score: consistencia entre líneas + cantidad de separadores
            bool consistente = conteos.All(c => c == conteos[0]);
            int score = (consistente ? 10000 : 0) + conteos[0];

            if (score > mejorScore) { mejorScore = score; mejor = sep; }
        }
        return mejor;
    }

    // ════════════════════════════════════════════════════════════
    //  DETECCIÓN DE ENCABEZADO
    // ════════════════════════════════════════════════════════════
    private static bool EsEncabezado(string[] tokens)
    {
        if (tokens.Length == 0) return false;

        string primero = tokens[0].Trim().ToLower();

        // Señales claras de encabezado
        if (primero is "id" or "#" or "num" or "index" or "idx") return true;

        // Primer token puramente numérico → es fila de datos
        if (double.TryParse(tokens[0].Trim(), NumberStyles.Any,
            CultureInfo.InvariantCulture, out _)) return false;

        // Mayoría de tokens parecen etiquetas (texto, no números ni fechas)
        int parecenEtiqueta = tokens.Count(t =>
        {
            string s = t.Trim();
            if (string.IsNullOrEmpty(s)) return false;
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) return false;
            // Fecha ISO (YYYY-MM-DD) → dato, no etiqueta
            if (s.Length == 10 && s[4] == '-' && s[7] == '-') return false;
            return true;
        });

        return parecenEtiqueta >= Math.Max(1, tokens.Length * 0.6);
    }

    // ════════════════════════════════════════════════════════════
    //  MAPEO FLEXIBLE: exacto → empieza con → contiene
    // ════════════════════════════════════════════════════════════
    private static int[] MapearColumnas(string[] headers)
    {
        string[] lower = headers.Select(h => h.ToLowerInvariant().Trim()).ToArray();
        var usados = new HashSet<int>();

        int Buscar(string[] alias)
        {
            // Pasada 1: exacta
            foreach (string a in alias)
                for (int i = 0; i < lower.Length; i++)
                    if (!usados.Contains(i) && lower[i] == a)
                    { usados.Add(i); return i; }

            // Pasada 2: empieza con
            foreach (string a in alias)
                for (int i = 0; i < lower.Length; i++)
                    if (!usados.Contains(i) && lower[i].StartsWith(a, StringComparison.Ordinal))
                    { usados.Add(i); return i; }

            // Pasada 3: contiene
            foreach (string a in alias)
                for (int i = 0; i < lower.Length; i++)
                    if (!usados.Contains(i) && lower[i].Contains(a, StringComparison.Ordinal))
                    { usados.Add(i); return i; }

            return -1;
        }

        return new[]
        {
            Buscar(_idAlias),
            Buscar(_nombreAlias),
            Buscar(_catAlias),
            Buscar(_valorAlias),
            Buscar(_fechaAlias)
        };
    }

    // ════════════════════════════════════════════════════════════
    //  HELPERS DE PARSEO
    // ════════════════════════════════════════════════════════════
    private static string[] Separar(string linea, char sep)
        => linea.Split(sep).Select(c => c.Trim()).ToArray();

    private static int? ValorInt(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        return int.TryParse(cols[idx].Trim(), out int v) ? v : null;
    }

    private static string? ValorStr(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        string s = cols[idx].Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static double? ValorDouble(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        return double.TryParse(cols[idx].Trim(), NumberStyles.Any,
            CultureInfo.InvariantCulture, out double v) ? v : null;
    }

    private static DateTime? ValorFecha(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        string s = cols[idx].Trim();
        if (string.IsNullOrEmpty(s)) return null;

        // Año solo (4 dígitos)
        if (s.Length == 4 && int.TryParse(s, out int anio) && anio >= 1900 && anio <= 2200)
            return new DateTime(anio, 1, 1);

        // Formatos comunes
        string[] fmts = {
            "yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yyyy", "dd/MM/yyyy",
            "MM-dd-yyyy", "MM/dd/yyyy", "yyyy-MM-ddTHH:mm:ss",
            "dd-MMM-yyyy", "MMM dd yyyy", "yyyy.MM.dd"
        };
        if (DateTime.TryParseExact(s, fmts, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime dt)) return dt;

        return DateTime.TryParse(s, out DateTime dt2) ? dt2 : null;
    }

    private static string EscapeSep(char sep) => sep switch
    {
        '\t' => "TAB",
        ' ' => "SPACE",
        _ => sep.ToString()
    };
}