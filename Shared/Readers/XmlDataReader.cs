using System.Xml.Linq;
using DataFusionArena.Shared.Models;

namespace DataFusionArena.Shared.Readers;

/// <summary>
/// Lee un archivo XML y lo convierte a List&lt;DataItem&gt;.
/// Acepta XML jerárquico o plano; busca el primer nivel de elementos hijo.
/// </summary>
public static class XmlDataReader
{
    public static List<DataItem> Leer(string rutaArchivo)
    {
        var lista = new List<DataItem>();

        if (!File.Exists(rutaArchivo))
        {
            Console.WriteLine($"[XML] ⚠  Archivo no encontrado: {rutaArchivo}");
            return lista;
        }

        try
        {
            var doc = XDocument.Load(rutaArchivo);
            // Toma los hijos directos del elemento raíz
            var elementos = doc.Root?.Elements().ToList() ?? new List<XElement>();

            // Si no hay hijos directos con datos, baja un nivel más
            if (elementos.Count > 0 && !TieneAtributosOTexto(elementos[0]))
                elementos = elementos.SelectMany(e => e.Elements()).ToList();

            int contador = 1;
            foreach (var el in elementos)
            {
                try
                {
                    var item = new DataItem { Fuente = "xml" };

                    item.Id       = LeerEntero(el, "id", "Id", "ID") ?? contador;
                    item.Nombre   = LeerCadena(el, "nombre", "name", "titulo", "title") ?? $"Item-{contador}";
                    item.Categoria = LeerCadena(el, "categoria", "category", "departamento", "department") ?? "Sin categoría";
                    item.Valor    = LeerDouble(el, "valor", "value", "salario", "salary") ?? 0;
                    item.Fecha    = LeerFecha(el, "fecha", "date", "fechaContratacion", "hireDate") ?? DateTime.Now;

                    // Atributos y elementos extra → CamposExtra
                    foreach (var attr in el.Attributes())
                    {
                        string clave = attr.Name.LocalName.ToLower();
                        if (clave is "id" or "nombre" or "name" or "titulo" or "title"
                                  or "categoria" or "category" or "departamento" or "department"
                                  or "valor" or "value" or "salario" or "salary"
                                  or "fecha" or "date") continue;
                        item.CamposExtra[attr.Name.LocalName] = attr.Value;
                    }
                    foreach (var hijo in el.Elements())
                    {
                        string clave = hijo.Name.LocalName.ToLower();
                        if (clave is "id" or "nombre" or "name" or "titulo" or "title"
                                  or "categoria" or "category" or "departamento" or "department"
                                  or "valor" or "value" or "salario" or "salary"
                                  or "fecha" or "date") continue;
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

    // ──────────────────────────────────────────────────────────────
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
