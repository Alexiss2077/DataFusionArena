using DataFusionArena.Shared.Models;
using System.Globalization;
using System.Text;

namespace DataFusionArena.Shared.Readers;

/// <summary>
/// Lector TXT robusto: detecta separador y encabezado automáticamente,
/// mapea columnas por coincidencia flexible (exacta → inicia con),
/// soporta campos entre comillas y almacena TODOS los campos en CamposExtra
/// para acceso confiable por nombre original.
///
/// ▸ Se eliminó el paso "contains" del mapeo de columnas: era demasiado
///   agresivo y provocaba que columnas arbitrarias (p. ej. "employee_name")
///   se asignaran incorrectamente a roles estándar de DataItem.
/// ▸ Se añadió soporte de campos entre comillas en Separar().
/// ▸ Se añadió limpieza de BOM UTF-8 en la primera fila.
/// ▸ El método Leer() ahora normaliza valores numéricos antes de guardar
///   en CamposExtra para que el valor mostrado coincida exactamente con
///   lo que aparece en el archivo original.
/// </summary>
public static class TxtDataReader
{
    public static List<string> UltimasColumnas { get; private set; } = new();
    public static Dictionary<string, string> MapeoColumnas { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    // ── Aliases para campos estándar de DataItem ─────────────────
    // REGLA: solo exacto y "empieza con"; el paso "contains" se eliminó
    // porque producía falsos positivos con nombres de columna arbitrarios.
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
            // Leer con detección automática de encoding (maneja UTF-8 BOM, Latin-1, etc.)
            var lineas = File.ReadAllLines(rutaArchivo, DetectarEncoding(rutaArchivo))
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'))
                .ToArray();

            if (lineas.Length == 0) return lista;

            // 1. Detectar separador
            char sep = DetectarSeparador(lineas);

            // 2. Detectar encabezado (la primera línea útil, sin BOM)
            string primeraLinea = QuitarBom(lineas[0]);
            string[] primeraFila = Separar(primeraLinea, sep);
            bool tieneEncabezado = EsEncabezado(primeraFila);
            int inicio = tieneEncabezado ? 1 : 0;

            // 3. Construir headers (sin BOM, sin comillas extra)
            string[] headers = tieneEncabezado
                ? primeraFila.Select(h => h.Trim().Trim('"').Trim()).ToArray()
                : Enumerable.Range(1, primeraFila.Length).Select(i => $"col{i}").ToArray();

            // 4. Mapear headers → campos DataItem (solo exacto + empieza-con)
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
                    if (cols.Length < 1 || (cols.Length == 1 && string.IsNullOrWhiteSpace(cols[0]))) continue;
                    rowNum++;

                    var item = new DataItem { Fuente = "txt" };

                    // Campos estándar (best-effort)
                    item.Id = ValorInt(cols, mapa[0]) ?? rowNum;
                    item.Nombre = ValorStr(cols, mapa[1]) ?? $"Fila-{rowNum}";
                    item.Categoria = ValorStr(cols, mapa[2]) ?? "";
                    item.Valor = ValorDouble(cols, mapa[3]) ?? 0;
                    item.Fecha = ValorFecha(cols, mapa[4]) ?? DateTime.Now;

                    // TODOS los campos → CamposExtra con valor RAW original
                    // Esto permite mostrar el valor exacto del archivo sin reformatear.
                    for (int c = 0; c < headers.Length; c++)
                    {
                        string key = headers[c].ToLowerInvariant();
                        string raw = c < cols.Length ? cols[c].Trim().Trim('"') : "";
                        item.CamposExtra[key] = raw;
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
    //  DETECCIÓN DE ENCODING
    // ════════════════════════════════════════════════════════════
    private static Encoding DetectarEncoding(string ruta)
    {
        // Leer los primeros bytes para detectar BOM
        try
        {
            byte[] bom = new byte[4];
            using var fs = File.OpenRead(ruta);
            int read = fs.Read(bom, 0, 4);
            if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }
        catch { }
        return Encoding.UTF8;
    }

    // ════════════════════════════════════════════════════════════
    //  LIMPIEZA DE BOM EN STRING
    // ════════════════════════════════════════════════════════════
    private static string QuitarBom(string linea)
    {
        // Quitar BOM U+FEFF si quedó como carácter al inicio
        if (linea.Length > 0 && linea[0] == '\uFEFF')
            return linea[1..];
        return linea;
    }

    // ════════════════════════════════════════════════════════════
    //  DETECCIÓN DE SEPARADOR
    // ════════════════════════════════════════════════════════════
    private static char DetectarSeparador(string[] lineas)
    {
        char[] candidatos = { '|', '\t', ';', ',' };
        string[] muestra = lineas.Take(Math.Min(6, lineas.Length)).ToArray();

        char mejor = '|';
        int mejorScore = -1;

        foreach (char sep in candidatos)
        {
            int[] conteos = muestra.Select(l => ContarSeparadores(l, sep)).ToArray();
            if (conteos[0] == 0) continue;

            bool consistente = conteos.All(c => c == conteos[0]);
            int score = (consistente ? 10000 : 0) + conteos[0];

            if (score > mejorScore) { mejorScore = score; mejor = sep; }
        }
        return mejor;
    }

    /// <summary>Cuenta separadores ignorando los que están dentro de comillas.</summary>
    private static int ContarSeparadores(string linea, char sep)
    {
        int count = 0;
        bool enComillas = false;
        foreach (char c in linea)
        {
            if (c == '"') { enComillas = !enComillas; continue; }
            if (!enComillas && c == sep) count++;
        }
        return count;
    }

    // ════════════════════════════════════════════════════════════
    //  DETECCIÓN DE ENCABEZADO
    // ════════════════════════════════════════════════════════════
    private static bool EsEncabezado(string[] tokens)
    {
        if (tokens.Length == 0) return false;

        string primero = tokens[0].Trim().TrimStart('\uFEFF').ToLower();

        // Señales inequívocas de encabezado
        if (primero is "id" or "#" or "num" or "index" or "idx") return true;

        // Si el primer token es puramente numérico → datos, no encabezado
        if (double.TryParse(tokens[0].Trim(), NumberStyles.Any,
            CultureInfo.InvariantCulture, out _)) return false;

        // Mayoría de tokens parecen etiquetas (texto sin números ni fechas ISO)
        int parecenEtiqueta = tokens.Count(t =>
        {
            string s = t.Trim().Trim('"');
            if (string.IsNullOrEmpty(s)) return false;
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) return false;
            if (s.Length >= 8 && s.Length <= 10 && s.Contains('-') && DateTime.TryParse(s, out _)) return false;
            if (s.Length == 4 && int.TryParse(s, out int y) && y >= 1900 && y <= 2200) return false;
            return true;
        });

        return parecenEtiqueta >= Math.Max(1, tokens.Length * 0.55);
    }

    // ════════════════════════════════════════════════════════════
    //  MAPEO FLEXIBLE: exacto → empieza con
    //  ⚠ Se ELIMINÓ el paso "contains" (era demasiado agresivo).
    //    Ejemplo problemático: "employee_name" contiene "name" → se mapeaba
    //    a "nombre" aunque el campo sea un identificador compuesto.
    // ════════════════════════════════════════════════════════════
    private static int[] MapearColumnas(string[] headers)
    {
        string[] lower = headers.Select(h => h.ToLowerInvariant().Trim().Trim('"')).ToArray();
        var usados = new HashSet<int>();

        int Buscar(string[] alias)
        {
            // Pasada 1: exacta
            foreach (string a in alias)
                for (int i = 0; i < lower.Length; i++)
                    if (!usados.Contains(i) && lower[i] == a)
                    { usados.Add(i); return i; }

            // Pasada 2: empieza con (solo si el alias es suficientemente largo)
            foreach (string a in alias)
            {
                if (a.Length < 3) continue; // evitar falsos positivos con alias cortos
                for (int i = 0; i < lower.Length; i++)
                    if (!usados.Contains(i) && lower[i].StartsWith(a + "_", StringComparison.Ordinal))
                    { usados.Add(i); return i; }
            }

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
    //  SEPARAR CAMPOS  (soporta comillas como en CSV)
    // ════════════════════════════════════════════════════════════
    private static string[] Separar(string linea, char sep)
    {
        var campos = new List<string>();
        var actual = new StringBuilder();
        bool enComillas = false;

        for (int i = 0; i < linea.Length; i++)
        {
            char c = linea[i];

            if (c == '"')
            {
                // Comilla doble escapada dentro de campo entre comillas
                if (enComillas && i + 1 < linea.Length && linea[i + 1] == '"')
                {
                    actual.Append('"');
                    i++; // saltar la segunda comilla
                }
                else
                {
                    enComillas = !enComillas;
                }
                continue;
            }

            if (c == sep && !enComillas)
            {
                campos.Add(actual.ToString().Trim());
                actual.Clear();
            }
            else
            {
                actual.Append(c);
            }
        }
        campos.Add(actual.ToString().Trim());
        return campos.ToArray();
    }

    // ════════════════════════════════════════════════════════════
    //  HELPERS DE PARSEO
    // ════════════════════════════════════════════════════════════
    private static int? ValorInt(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        string s = cols[idx].Trim().Trim('"');
        return int.TryParse(s, out int v) ? v : null;
    }

    private static string? ValorStr(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        string s = cols[idx].Trim().Trim('"');
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static double? ValorDouble(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        string s = cols[idx].Trim().Trim('"');
        return double.TryParse(s, NumberStyles.Any,
            CultureInfo.InvariantCulture, out double v) ? v : null;
    }

    private static DateTime? ValorFecha(string[] cols, int idx)
    {
        if (idx < 0 || idx >= cols.Length) return null;
        string s = cols[idx].Trim().Trim('"');
        if (string.IsNullOrEmpty(s)) return null;

        // Año solo (4 dígitos)
        if (s.Length == 4 && int.TryParse(s, out int anio) && anio >= 1900 && anio <= 2200)
            return new DateTime(anio, 1, 1);

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