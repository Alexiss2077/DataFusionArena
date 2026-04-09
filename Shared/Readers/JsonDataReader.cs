using System.Text.Json;
using DataFusionArena.Shared.Models;

namespace DataFusionArena.Shared.Readers;

/// <summary>
/// Lee un archivo JSON con un arreglo de objetos y lo convierte a List&lt;DataItem&gt;.
/// Maneja JSON mal formado (evento sorpresa) con try-catch granular.
/// </summary>
public static class JsonDataReader
{
    public static List<DataItem> Leer(string rutaArchivo)
    {
        var lista = new List<DataItem>();

        if (!File.Exists(rutaArchivo))
        {
            Console.WriteLine($"[JSON] ⚠  Archivo no encontrado: {rutaArchivo}");
            return lista;
        }

        string contenido = File.ReadAllText(rutaArchivo);

        try
        {
            // Intenta parsear como arreglo JSON
            using var documento = JsonDocument.Parse(contenido);
            var raiz = documento.RootElement;

            var elementos = raiz.ValueKind == JsonValueKind.Array
                ? raiz.EnumerateArray().ToList()
                : new List<JsonElement> { raiz };    // acepta objeto único también

            int contadorId = 1;
            foreach (var el in elementos)
            {
                try
                {
                    var item = new DataItem
                    {
                        Fuente = "json"
                    };

                    // ─── Mapeo flexible de propiedades ───
                    item.Id       = LeerEntero(el, "id", "Id", "ID") ?? contadorId;
                    item.Nombre   = LeerCadena(el, "nombre", "name", "titulo", "title") ?? $"Item-{contadorId}";
                    item.Categoria = LeerCadena(el, "categoria", "category", "genero", "genre") ?? "Sin categoría";
                    item.Valor    = LeerDouble(el, "valor", "value", "precio", "price") ?? 0.0;
                    item.Fecha    = LeerFecha(el, "fecha", "date", "releaseDate", "fecha_lanzamiento") ?? DateTime.Now;

                    // Resto de campos → CamposExtra
                    foreach (var prop in el.EnumerateObject())
                    {
                        string clave = prop.Name.ToLower();
                        if (clave is "id" or "nombre" or "name" or "titulo" or "title"
                                  or "categoria" or "category" or "genero" or "genre"
                                  or "valor" or "value" or "precio" or "price"
                                  or "fecha" or "date" or "releasedate" or "fecha_lanzamiento")
                            continue;
                        item.CamposExtra[prop.Name] = prop.Value.ToString();
                    }

                    lista.Add(item);
                    contadorId++;
                }
                catch (Exception exItem)
                {
                    Console.WriteLine($"[JSON] ⚠  Error en elemento #{contadorId}: {exItem.Message}");
                    contadorId++;
                }
            }

            Console.WriteLine($"[JSON] ✓  {lista.Count} registros leídos desde {Path.GetFileName(rutaArchivo)}");
        }
        catch (JsonException ex)
        {
            // ── Evento sorpresa: JSON mal formado ──
            Console.WriteLine($"[JSON] ✗  JSON mal formado en {Path.GetFileName(rutaArchivo)}: {ex.Message}");
            Console.WriteLine("[JSON]    Intentando recuperar datos válidos...");
            lista.AddRange(RecuperarJsonParcial(contenido));
        }

        return lista;
    }

    // ──────────────────────────────────────────────────────────────
    // Recuperación de emergencia para JSON con errores
    // ──────────────────────────────────────────────────────────────
    private static List<DataItem> RecuperarJsonParcial(string contenido)
    {
        var lista = new List<DataItem>();
        // Intenta encontrar objetos individuales {} aunque el arreglo esté roto
        int inicio = contenido.IndexOf('{');
        int id = 1;
        while (inicio >= 0)
        {
            int fin = EncontrarCierreLlave(contenido, inicio);
            if (fin < 0) break;

            string fragmento = contenido[inicio..(fin + 1)];
            try
            {
                using var doc = JsonDocument.Parse(fragmento);
                var el = doc.RootElement;
                lista.Add(new DataItem
                {
                    Id        = LeerEntero(el, "id") ?? id,
                    Nombre    = LeerCadena(el, "nombre", "name") ?? $"Recuperado-{id}",
                    Categoria = LeerCadena(el, "categoria", "category") ?? "Recuperado",
                    Valor     = LeerDouble(el, "valor", "value") ?? 0,
                    Fuente    = "json(recuperado)",
                    Fecha     = DateTime.Now
                });
                id++;
            }
            catch { /* ignorar fragmentos inválidos */ }

            inicio = contenido.IndexOf('{', fin + 1);
        }
        Console.WriteLine($"[JSON]    Recuperados {lista.Count} objetos parciales.");
        return lista;
    }

    private static int EncontrarCierreLlave(string s, int inicio)
    {
        int depth = 0;
        bool inString = false;
        for (int i = inicio; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '"' && (i == 0 || s[i - 1] != '\\')) inString = !inString;
            if (inString) continue;
            if (c == '{') depth++;
            else if (c == '}') { depth--; if (depth == 0) return i; }
        }
        return -1;
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers de lectura con múltiples nombres de propiedad posibles
    // ──────────────────────────────────────────────────────────────
    private static string? LeerCadena(JsonElement el, params string[] claves)
    {
        foreach (var clave in claves)
            if (el.TryGetProperty(clave, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        return null;
    }

    private static int? LeerEntero(JsonElement el, params string[] claves)
    {
        foreach (var clave in claves)
            if (el.TryGetProperty(clave, out var p))
                if (p.TryGetInt32(out int v)) return v;
                else if (int.TryParse(p.ToString(), out int v2)) return v2;
        return null;
    }

    private static double? LeerDouble(JsonElement el, params string[] claves)
    {
        foreach (var clave in claves)
            if (el.TryGetProperty(clave, out var p))
                if (p.TryGetDouble(out double v)) return v;
                else if (double.TryParse(p.ToString(), System.Globalization.NumberStyles.Any,
                         System.Globalization.CultureInfo.InvariantCulture, out double v2)) return v2;
        return null;
    }

    private static DateTime? LeerFecha(JsonElement el, params string[] claves)
    {
        foreach (var clave in claves)
            if (el.TryGetProperty(clave, out var p) && p.ValueKind == JsonValueKind.String)
                if (DateTime.TryParse(p.GetString(), out DateTime d)) return d;
        return null;
    }
}
