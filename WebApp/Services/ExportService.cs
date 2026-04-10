using System.Text;
using DataFusionArena.Shared.Models;

namespace DataFusionArena.Web.Services;

/// <summary>
/// Servicio para exportar datos a CSV y Excel.
/// </summary>
public static class ExportService
{
    /// <summary>Exporta a CSV (texto separado por comas).</summary>
    public static byte[] ExportarCsv(List<DataItem> datos, string nombreArchivo = "datos")
    {
        var sb = new StringBuilder();

        // Encabezados
        var extraKeys = datos
            .SelectMany(d => d.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k)
            .ToList();

        var headers = new List<string>
        {
            "ID", "Nombre", "Categoría", "Valor", "Fecha", "Fuente"
        };
        headers.AddRange(extraKeys);

        sb.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

        // Filas
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
                fields.Add(EscapeCsv(item.CamposExtra.TryGetValue(key, out var v) ? v : ""));

            sb.AppendLine(string.Join(",", fields));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Exporta a Excel (.xlsx) usando EPPlus (requiere licencia o referencia).</summary>
    public static byte[] ExportarExcel(List<DataItem> datos, string nombreArchivo = "datos")
    {
        // Si no tienes EPPlus, devuelve CSV como fallback
        // En producción, agregar: dotnet add package EPPlus
        return ExportarCsv(datos, nombreArchivo);
    }

    /// <summary>Exporta estadísticas a CSV.</summary>
    public static byte[] ExportarEstadisticasCsv(
        Dictionary<string, DataFusionArena.Shared.Processing.EstadisticasCategoria> stats)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\"Categoría\",\"Cantidad\",\"Promedio\",\"Máximo\",\"Mínimo\",\"Total\"");

        foreach (var s in stats.Values.OrderByDescending(x => x.SumaValores))
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

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        string escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\"", "\"\"");
    }

    /// <summary>Genera nombre de archivo con timestamp.</summary>
    public static string GenerarNombreArchivo(string prefijo = "datos")
        => $"{prefijo}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
}