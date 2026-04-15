using System.Text.Json;
using DataFusionArena.Shared.Models;

namespace DataFusionArena.Shared.Readers;

public static class JsonDataReader
{
    private static readonly string[] _nombreKeys = { "nombre", "name", "titulo", "title", "producto", "juego", "descripcion", "description", "player", "jugador" };
    private static readonly string[] _categoriaKeys = { "categoria", "category", "genero", "genre", "tipo", "type", "grupo", "group", "departamento", "department", "nivel", "level" };
    private static readonly string[] _valorKeys = { "valor", "value", "precio", "price", "monto", "amount", "score", "puntos", "points", "salario", "salary", "total", "suma" };
    private static readonly string[] _fechaKeys = { "fecha", "date", "releasedate", "fecha_lanzamiento", "fecha_registro", "created_at", "updated_at", "timestamp" };
    private static readonly string[] _idKeys = { "id", "Id", "ID", "codigo", "code", "sku" };

    public static List<string> UltimasColumnas { get; private set; } = new();
    public static Dictionary<string, string> MapeoColumnas { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public static List<DataItem> Leer(string rutaArchivo)
    {
        var lista = new List<DataItem>();
        UltimasColumnas = new List<string>();
        MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(rutaArchivo))
        {
            Console.WriteLine($"[JSON] ⚠  Archivo no encontrado: {rutaArchivo}");
            return lista;
        }

        string contenido = File.ReadAllText(rutaArchivo);

        try
        {
            using var documento = JsonDocument.Parse(contenido);
            var raiz = documento.RootElement;

            var elementos = raiz.ValueKind == JsonValueKind.Array
                ? raiz.EnumerateArray().ToList()
                : new List<JsonElement> { raiz };

            if (elementos.Count > 0)
                DetectarMetadatos(elementos[0]);

            int contadorId = 1;
            foreach (var el in elementos)
            {
                try
                {
                    var item = new DataItem { Fuente = "json" };

                    item.Id = LeerEntero(el, _idKeys) ?? contadorId;
                    item.Nombre = LeerCadena(el, _nombreKeys) ?? FallbackPrimeraString(el, _idKeys) ?? $"Item-{contadorId}";
                    item.Categoria = LeerCadena(el, _categoriaKeys) ?? FallbackPrimeraString(el, _idKeys, _nombreKeys) ?? "Sin categoría";
                    item.Valor = LeerDouble(el, _valorKeys) ?? FallbackPrimerNumero(el, _idKeys) ?? 0.0;
                    item.Fecha = LeerFecha(el, _fechaKeys) ?? DateTime.Now;

                    var usadas = new HashSet<string>(_idKeys.Concat(_nombreKeys).Concat(_categoriaKeys)
                                                           .Concat(_valorKeys).Concat(_fechaKeys),
                                                    StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in el.EnumerateObject())
                    {
                        if (usadas.Contains(prop.Name)) continue;
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
            Console.WriteLine($"[JSON] ✗  JSON mal formado en {Path.GetFileName(rutaArchivo)}: {ex.Message}");
            Console.WriteLine("[JSON]    Intentando recuperar datos válidos...");
            lista.AddRange(RecuperarJsonParcial(contenido));
        }

        return lista;
    }

    private static void DetectarMetadatos(JsonElement primerElemento)
    {
        var todasProps = new List<string>();
        foreach (var prop in primerElemento.EnumerateObject())
            todasProps.Add(prop.Name);

        string? idCol = BuscarProp(todasProps, _idKeys);
        string? nombreCol = BuscarProp(todasProps, _nombreKeys);
        string? catCol = BuscarProp(todasProps, _categoriaKeys);
        string? valorCol = BuscarProp(todasProps, _valorKeys);
        string? fechaCol = BuscarProp(todasProps, _fechaKeys);

        MapeoColumnas.Clear();
        if (idCol != null) MapeoColumnas[idCol] = "id";
        if (nombreCol != null) MapeoColumnas[nombreCol] = "nombre";
        if (catCol != null) MapeoColumnas[catCol] = "categoria";
        if (valorCol != null) MapeoColumnas[valorCol] = "valor";
        if (fechaCol != null) MapeoColumnas[fechaCol] = "fecha";

        UltimasColumnas.Clear();
        foreach (var prop in todasProps)
            UltimasColumnas.Add(prop);
    }

    private static string? BuscarProp(List<string> props, string[] aliases)
    {
        foreach (var alias in aliases)
            foreach (var p in props)
                if (string.Equals(p, alias, StringComparison.OrdinalIgnoreCase))
                    return p;
        return null;
    }

    private static List<DataItem> RecuperarJsonParcial(string contenido)
    {
        var lista = new List<DataItem>();
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
                    Id = LeerEntero(el, _idKeys) ?? id,
                    Nombre = LeerCadena(el, _nombreKeys) ?? FallbackPrimeraString(el, _idKeys) ?? $"Recuperado-{id}",
                    Categoria = LeerCadena(el, _categoriaKeys) ?? "Recuperado",
                    Valor = LeerDouble(el, _valorKeys) ?? 0,
                    Fuente = "json(recuperado)",
                    Fecha = DateTime.Now
                });
                id++;
            }
            catch { }

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

    private static string? LeerCadena(JsonElement el, string[] claves)
    {
        foreach (var clave in claves)
            if (el.TryGetProperty(clave, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        return null;
    }

    private static int? LeerEntero(JsonElement el, string[] claves)
    {
        foreach (var clave in claves)
            if (el.TryGetProperty(clave, out var p))
                if (p.TryGetInt32(out int v)) return v;
                else if (int.TryParse(p.ToString(), out int v2)) return v2;
        return null;
    }

    private static double? LeerDouble(JsonElement el, string[] claves)
    {
        foreach (var clave in claves)
            if (el.TryGetProperty(clave, out var p))
                if (p.TryGetDouble(out double v)) return v;
                else if (double.TryParse(p.ToString(), System.Globalization.NumberStyles.Any,
                         System.Globalization.CultureInfo.InvariantCulture, out double v2)) return v2;
        return null;
    }

    private static DateTime? LeerFecha(JsonElement el, string[] claves)
    {
        foreach (var clave in claves)
            if (el.TryGetProperty(clave, out var p) && p.ValueKind == JsonValueKind.String)
                if (DateTime.TryParse(p.GetString(), out DateTime d)) return d;
        return null;
    }

    private static string? FallbackPrimeraString(JsonElement el, params string[][] excluidos)
    {
        var exc = new HashSet<string>(excluidos.SelectMany(a => a), StringComparer.OrdinalIgnoreCase);
        foreach (var prop in el.EnumerateObject())
        {
            if (exc.Contains(prop.Name)) continue;
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var val = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
        }
        return null;
    }

    private static double? FallbackPrimerNumero(JsonElement el, params string[][] excluidos)
    {
        var exc = new HashSet<string>(excluidos.SelectMany(a => a), StringComparer.OrdinalIgnoreCase);
        foreach (var prop in el.EnumerateObject())
        {
            if (exc.Contains(prop.Name)) continue;
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out double v))
                return v;
        }
        return null;
    }
}