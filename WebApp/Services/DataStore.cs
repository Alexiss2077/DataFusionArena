using DataFusionArena.Shared.Models;
using DataFusionArena.Shared.Readers;
using DataFusionArena.Shared.Processing;

namespace DataFusionArena.Web.Services;

/// <summary>
/// Servicio singleton que mantiene todos los datos en memoria
/// durante la vida del servidor web.
/// </summary>
public class DataStore
{
    private readonly Lock _lock = new();
    public List<DataItem> Datos { get; } = new();

    // ── Carga inicial ───────────────────────────────────────────
    public void CargarDatosIniciales(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "SampleData");
        if (!Directory.Exists(dir)) return;

        lock (_lock)
        {
            AgregarDesdeArchivo(dir, "products.json",  "json");
            AgregarDesdeArchivo(dir, "sales.csv",      "csv");
            AgregarDesdeArchivo(dir, "employees.xml",  "xml");
            AgregarDesdeArchivo(dir, "records.txt",    "txt");
        }
    }

    private void AgregarDesdeArchivo(string dir, string file, string tipo)
    {
        var ruta = Path.Combine(dir, file);
        if (!File.Exists(ruta)) return;

        List<DataItem> items = tipo switch
        {
            "json" => JsonDataReader.Leer(ruta),
            "csv"  => CsvDataReader.Leer(ruta),
            "xml"  => FixXml(XmlDataReader.Leer(ruta)),
            "txt"  => TxtDataReader.Leer(ruta),
            _      => new()
        };
        DataProcessor.AgregarDatos(Datos, items);
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

    // ── Operaciones ─────────────────────────────────────────────
    public void Agregar(List<DataItem> nuevos)
    {
        lock (_lock) DataProcessor.AgregarDatos(Datos, nuevos);
    }

    public List<DataItem> ObtenerTodos()
    {
        lock (_lock) return new List<DataItem>(Datos);
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
