using DataFusionArena.Shared.Models;
using DataFusionArena.Shared.Readers;
using DataFusionArena.Shared.Processing;

namespace DataFusionArena.Web.Services;

public class DataStore
{
    private readonly Lock _lock = new();
    public List<DataItem> Datos { get; } = new();

    // Columnas y mapeo del último archivo/fuente cargado
    public List<string> UltimasColumnas { get; private set; } = new();
    public Dictionary<string, string> UltimoMapeo { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public string UltimaFuente { get; private set; } = "";

    public void CargarDatosIniciales(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "SampleData");
        if (!Directory.Exists(dir)) return;

        lock (_lock)
        {
            AgregarDesdeArchivo(dir, "products.json", "json");
            AgregarDesdeArchivo(dir, "sales.csv", "csv");
            AgregarDesdeArchivo(dir, "employees.xml", "xml");
            AgregarDesdeArchivo(dir, "records.txt", "txt");
        }
    }

    private void AgregarDesdeArchivo(string dir, string file, string tipo)
    {
        var ruta = Path.Combine(dir, file);
        if (!File.Exists(ruta)) return;

        List<DataItem> items = tipo switch
        {
            "json" => JsonDataReader.Leer(ruta),
            "csv" => CsvDataReader.Leer(ruta),
            "xml" => FixXml(XmlDataReader.Leer(ruta)),
            "txt" => TxtDataReader.Leer(ruta),
            _ => new()
        };
        DataProcessor.AgregarDatos(Datos, items);
        ActualizarMetadatos(tipo);
    }

    private void ActualizarMetadatos(string tipo)
    {
        UltimaFuente = tipo;
        switch (tipo)
        {
            case "json":
                if (JsonDataReader.UltimasColumnas.Count > 0)
                {
                    UltimasColumnas = new List<string>(JsonDataReader.UltimasColumnas);
                    UltimoMapeo = new Dictionary<string, string>(JsonDataReader.MapeoColumnas, StringComparer.OrdinalIgnoreCase);
                }
                break;
            case "csv":
                if (CsvDataReader.UltimasColumnas.Count > 0)
                {
                    UltimasColumnas = new List<string>(CsvDataReader.UltimasColumnas);
                    UltimoMapeo = new Dictionary<string, string>(CsvDataReader.MapeoColumnas, StringComparer.OrdinalIgnoreCase);
                }
                break;
            case "xml":
                if (XmlDataReader.UltimasColumnas.Count > 0)
                {
                    UltimasColumnas = new List<string>(XmlDataReader.UltimasColumnas);
                    UltimoMapeo = new Dictionary<string, string>(XmlDataReader.MapeoColumnas, StringComparer.OrdinalIgnoreCase);
                }
                break;
            case "txt":
                if (TxtDataReader.UltimasColumnas.Count > 0)
                {
                    UltimasColumnas = new List<string>(TxtDataReader.UltimasColumnas);
                    UltimoMapeo = new Dictionary<string, string>(TxtDataReader.MapeoColumnas, StringComparer.OrdinalIgnoreCase);
                }
                break;
        }
    }

    private static List<DataItem> FixXml(List<DataItem> items)
    {
        foreach (var item in items)
        {
            if (item.CamposExtra.TryGetValue("departamento", out var dep))
            { item.Categoria = dep; item.CamposExtra.Remove("departamento"); }
            if (item.CamposExtra.TryGetValue("salario", out var sal) &&
                double.TryParse(sal, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double sv))
            { item.Valor = sv; item.CamposExtra.Remove("salario"); }
        }
        return items;
    }

    public void Agregar(List<DataItem> nuevos, string tipo)
    {
        lock (_lock)
        {
            DataProcessor.AgregarDatos(Datos, nuevos);
            ActualizarMetadatos(tipo);
        }
    }

    // Mantener compatibilidad con código existente
    public void Agregar(List<DataItem> nuevos)
    {
        lock (_lock) DataProcessor.AgregarDatos(Datos, nuevos);
    }

    public List<DataItem> ObtenerTodos()
    {
        lock (_lock) return new List<DataItem>(Datos);
    }

    /// <summary>
    /// Devuelve la lista de columnas tal cual están en el dataset,
    /// en el orden original, usando los metadatos del último reader.
    /// Si no hay metadatos, construye la lista desde CamposExtra.
    /// </summary>
    public List<string> ObtenerColumnas()
    {
        lock (_lock)
        {
            if (UltimasColumnas.Count > 0)
                return new List<string>(UltimasColumnas);

            // Fallback: construir desde CamposExtra
            var extras = Datos
                .SelectMany(d => d.CamposExtra.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k)
                .ToList();

            if (extras.Count > 0)
                return extras;

            return new List<string> { "id", "nombre", "categoria", "valor", "fecha", "fuente" };
        }
    }

    public Dictionary<string, string> ObtenerMapeo()
    {
        lock (_lock) return new Dictionary<string, string>(UltimoMapeo, StringComparer.OrdinalIgnoreCase);
    }

    public string ObtenerFuente()
    {
        lock (_lock) return UltimaFuente;
    }

    public Dictionary<string, List<DataItem>> PorCategoria()
        => DataProcessor.AgruparPorCategoria(ObtenerTodos());

    public Dictionary<string, EstadisticasCategoria> Estadisticas()
        => DataProcessor.CalcularEstadisticas(ObtenerTodos());

    public List<DataItem> Filtrar(string campo, string valor)
        => DataProcessor.Filtrar(ObtenerTodos(), campo, valor);

    public List<DataItem> Ordenar(string campo, bool asc)
        => DataProcessor.Ordenar(ObtenerTodos(), campo, asc);

    public List<string> Fuentes()
        => ObtenerTodos().Select(d => d.Fuente).Distinct().OrderBy(f => f).ToList();
}