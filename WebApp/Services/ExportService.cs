using System.Text;
using DataFusionArena.Shared.Models;
using DataFusionArena.Shared.Processing;

namespace DataFusionArena.Web.Services;

/// <summary>
/// Servicio para exportar datos a CSV y Excel.
/// </summary>
public static class ExportService
{
    /// <summary>Exporta a CSV (texto separado por comas).</summary>
    public static byte[] ExportarCsv(List<DataItem> datos)
    {
        var sb = new StringBuilder();

        if (datos.Count == 0)
        {
            sb.AppendLine("No hay datos para exportar");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        // Detectar todas las columnas extra
        var extraKeys = datos
            .SelectMany(d => d.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k)
            .ToList();

        // Encabezados
        var headers = new List<string>
        {
            "ID", "Nombre", "Categoría", "Valor", "Fecha", "Fuente"
        };
        headers.AddRange(extraKeys);

        // Escribir encabezados
        sb.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

        // Escribir filas
        foreach (var item in datos)
        {
            var fields = new List<string>
            {
                EscapeCsv(item.Id.ToString()),
                EscapeCsv(item.Nombre),
                EscapeCsv(item.Categoria),
                EscapeCsv(item.Valor.ToString("F2")),
                EscapeCsv(item.Fecha.ToString("yyyy-MM-dd")),
                EscapeCsv(item.Fuente)
            };

            foreach (var key in extraKeys)
            {
                var value = item.CamposExtra.TryGetValue(key, out var v) ? v : "";
                fields.Add(EscapeCsv(value));
            }

            sb.AppendLine(string.Join(",", fields));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Exporta a Excel (.xlsx).</summary>
    public static byte[] ExportarExcel(List<DataItem> datos)
    {
        // Por ahora, devolvemos CSV (para Excel completo se necesaría EPPlus)
        // Si instalas EPPlus, puedes implementar Excel real
        return ExportarCsv(datos);
    }

    /// <summary>Exporta estadísticas a CSV.</summary>
    public static byte[] ExportarEstadisticasCsv(Dictionary<string, EstadisticasCategoria> stats)
    {
        var sb = new StringBuilder();

        // Encabezados
        sb.AppendLine("\"Categoría\",\"Cantidad\",\"Promedio\",\"Máximo\",\"Mínimo\",\"Total\"");

        // Ordenar por total descendente
        var ordenado = stats.Values.OrderByDescending(x => x.SumaValores);

        // Filas
        foreach (var s in ordenado)
        {
            sb.AppendLine(
                $"\"{EscapeCsvValue(s.Categoria)}\"," +
                $"{s.Cantidad}," +
                $"{s.Promedio:F2}," +
                $"{s.ValorMaximo:F2}," +
                $"{s.ValorMinimo:F2}," +
                $"{s.SumaValores:F2}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        // Si contiene comillas o saltos de línea, escapar
        string escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value.Replace("\"", "\"\"");
    }
}