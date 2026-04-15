using System.Xml.Linq;
using DataFusionArena.Shared.Models;

namespace DataFusionArena.Shared.Readers;

public static class XmlDataReader
{
    public static List<string> UltimasColumnas { get; private set; } = new();
    public static Dictionary<string, string> MapeoColumnas { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] _idAliases = { "id", "Id", "ID" };
    private static readonly string[] _nombreAliases = { "nombre", "name", "titulo", "title" };
    private static readonly string[] _categoriaAliases = { "categoria", "category", "departamento", "department" };
    private static readonly string[] _valorAliases = { "valor", "value", "salario", "salary" };
    private static readonly string[] _fechaAliases = { "fecha", "date", "fechaContratacion", "hireDate" };

    public static List<DataItem> Leer(string rutaArchivo)
    {
        var lista = new List<DataItem>();
        UltimasColumnas = new List<string>();
        MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(rutaArchivo))
        {
            Console.WriteLine($"[XML] ⚠  Archivo no encontrado: {rutaArchivo}");
            return lista;
        }

        try
        {
            var doc = XDocument.Load(rutaArchivo);
            var elementos = doc.Root?.Elements().ToList() ?? new List<XElement>();

            if (elementos.Count > 0 && !TieneAtributosOTexto(elementos[0]))
                elementos = elementos.SelectMany(e => e.Elements()).ToList();

            if (elementos.Count > 0)
                DetectarMetadatos(elementos[0]);

            int contador = 1;
            foreach (var el in elementos)
            {
                try
                {
                    var item = new DataItem { Fuente = "xml" };

                    item.Id = LeerEntero(el, _idAliases) ?? contador;
                    item.Nombre = LeerCadena(el, _nombreAliases) ?? $"Item-{contador}";
                    item.Categoria = LeerCadena(el, _categoriaAliases) ?? "Sin categoría";
                    item.Valor = LeerDouble(el, _valorAliases) ?? 0;
                    item.Fecha = LeerFecha(el, _fechaAliases) ?? DateTime.Now;

                    var mapeadas = new HashSet<string>(
                        _idAliases.Concat(_nombreAliases).Concat(_categoriaAliases)
                                  .Concat(_valorAliases).Concat(_fechaAliases),
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var attr in el.Attributes())
                    {
                        if (mapeadas.Contains(attr.Name.LocalName)) continue;
                        item.CamposExtra[attr.Name.LocalName] = attr.Value;
                    }
                    foreach (var hijo in el.Elements())
                    {
                        if (mapeadas.Contains(hijo.Name.LocalName)) continue;
                        item.CamposExtra[hijo.Name.LocalName] = hijo.Value.Trim();
                    }

                    lista.Add(item);
                    contador++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[XML] ⚠  Error en elemento #{contador}: {ex.Message}");
                    contador++;
                }
            }

            Console.WriteLine($"[XML] ✓  {lista.Count} registros leídos desde {Path.GetFileName(rutaArchivo)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[XML] ✗  Error leyendo XML: {ex.Message}");
        }

        return lista;
    }

    private static void DetectarMetadatos(XElement primerElemento)
    {
        var nombresHijos = new List<string>();
        foreach (var hijo in primerElemento.Elements())
            nombresHijos.Add(hijo.Name.LocalName);
        foreach (var attr in primerElemento.Attributes())
            if (!nombresHijos.Contains(attr.Name.LocalName))
                nombresHijos.Add(attr.Name.LocalName);

        MapeoColumnas.Clear();
        string? c;
        c = BuscarEnLista(nombresHijos, _idAliases); if (c != null) MapeoColumnas[c] = "id";
        c = BuscarEnLista(nombresHijos, _nombreAliases); if (c != null) MapeoColumnas[c] = "nombre";
        c = BuscarEnLista(nombresHijos, _categoriaAliases); if (c != null) MapeoColumnas[c] = "categoria";
        c = BuscarEnLista(nombresHijos, _valorAliases); if (c != null) MapeoColumnas[c] = "valor";
        c = BuscarEnLista(nombresHijos, _fechaAliases); if (c != null) MapeoColumnas[c] = "fecha";

        UltimasColumnas = new List<string>(nombresHijos);
    }

    private static string? BuscarEnLista(List<string> lista, string[] aliases)
    {
        foreach (var alias in aliases)
            foreach (var item in lista)
                if (string.Equals(item, alias, StringComparison.OrdinalIgnoreCase))
                    return item;
        return null;
    }

    private static bool TieneAtributosOTexto(XElement el)
        => el.Attributes().Any() || el.Elements().Any() || !string.IsNullOrWhiteSpace(el.Value);

    private static string? LeerCadena(XElement el, params string[] claves)
    {
        foreach (var c in claves)
        {
            var hijo = el.Element(c) ?? el.Element(c.ToLower()) ?? el.Element(Capitalizar(c));
            if (hijo != null) return hijo.Value.Trim();
            var attr = el.Attribute(c);
            if (attr != null) return attr.Value.Trim();
        }
        return null;
    }

    private static int? LeerEntero(XElement el, params string[] claves)
    {
        var s = LeerCadena(el, claves);
        return s != null && int.TryParse(s, out int v) ? v : (int?)null;
    }

    private static double? LeerDouble(XElement el, params string[] claves)
    {
        var s = LeerCadena(el, claves);
        return s != null && double.TryParse(s, System.Globalization.NumberStyles.Any,
               System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : (double?)null;
    }

    private static DateTime? LeerFecha(XElement el, params string[] claves)
    {
        var s = LeerCadena(el, claves);
        return s != null && DateTime.TryParse(s, out DateTime d) ? d : (DateTime?)null;
    }

    private static string Capitalizar(string s)
        => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
}