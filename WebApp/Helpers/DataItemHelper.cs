using DataFusionArena.Shared.Models;

namespace DataFusionArena.Web.Helpers;

public static class DataItemHelper
{
    /// <summary>
    /// Obtiene el valor de un DataItem para una columna dada (por nombre original del dataset).
    /// Busca primero en CamposExtra con el nombre original, luego por mapeo a propiedad estándar.
    /// </summary>
    public static string ObtenerValor(DataItem item, string columna, Dictionary<string, string> mapeo)
    {
        // 1. Buscar en CamposExtra por nombre exacto de columna (caso TXT, CSV, JSON, XML)
        if (item.CamposExtra.TryGetValue(columna, out var v) && v != null)
            return v;

        // 2. Buscar en CamposExtra por nombre en minúsculas
        if (item.CamposExtra.TryGetValue(columna.ToLowerInvariant(), out v) && v != null)
            return v;

        // 3. Resolver via mapeo → propiedad estándar de DataItem
        string campo = mapeo.TryGetValue(columna, out var m) ? m.ToLower() : columna.ToLower();
        return campo switch
        {
            "id" => item.Id.ToString(),
            "nombre" => item.Nombre ?? "",
            "categoria" => item.Categoria ?? "",
            "valor" => item.Valor.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "fecha" => item.Fecha.ToString("yyyy-MM-dd"),
            "fuente" => item.Fuente ?? "",
            _ => ""
        };
    }

    /// <summary>
    /// Detecta si la columna contiene principalmente valores numéricos,
    /// tomando una muestra de items.
    /// </summary>
    public static bool EsColumnaNumerica(
        IEnumerable<DataItem> muestra,
        string columna,
        Dictionary<string, string> mapeo)
    {
        string campo = mapeo.TryGetValue(columna, out var m) ? m.ToLower() : columna.ToLower();
        if (campo is "id" or "valor") return true;
        if (campo is "nombre" or "categoria" or "fuente" or "fecha") return false;

        int total = 0, numericos = 0;
        foreach (var item in muestra.Take(30))
        {
            string val = ObtenerValor(item, columna, mapeo);
            if (string.IsNullOrEmpty(val)) continue;
            total++;
            if (double.TryParse(val, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out _))
                numericos++;
        }
        return total > 0 && numericos >= total * 0.75;
    }
}