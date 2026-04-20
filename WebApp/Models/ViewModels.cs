using DataFusionArena.Shared.Models;
using DataFusionArena.Shared.Processing;

namespace DataFusionArena.Web.Models;

public class HomeViewModel
{
    public List<DataItem> Datos { get; set; } = new();
    public int TotalRegistros { get; set; }
    public int Pagina { get; set; } = 1;
    public int TotalPaginas { get; set; }
    public string Busqueda { get; set; } = "";
    public string Campo { get; set; } = "nombre";
    public List<string> Fuentes { get; set; } = new();
    public string FuenteActiva { get; set; } = "";

    // Columnas dinámicas del dataset
    public List<string> Columnas { get; set; } = new();
    public Dictionary<string, string> Mapeo { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string FuenteDatos { get; set; } = "";
}

public class GraficasViewModel
{
    public List<string> Categorias { get; set; } = new();
    public List<double> Totales { get; set; } = new();
    public List<string> Fuentes { get; set; } = new();
    public List<int> ConteoFuente { get; set; } = new();
    public List<string> Fechas { get; set; } = new();
    public List<double> ValoresFecha { get; set; } = new();
    public int TotalItems { get; set; }
}

public class ClimaViewModel
{
    public string Ciudad { get; set; } = "San Buenaventura, México";
    public List<DiaClima> Pronostico { get; set; } = new();
    public bool ConError { get; set; }
    public string MensajeError { get; set; } = "";
    public List<string> Fechas { get; set; } = new();
    public List<double> TempMax { get; set; } = new();
    public List<double> TempMin { get; set; } = new();
    public List<double> Precipitacion { get; set; } = new();
    public List<double> Viento { get; set; } = new();
}

public class DiaClima
{
    public string Fecha { get; set; } = "";
    public double TempMax { get; set; }
    public double TempMin { get; set; }
    public double Precipitacion { get; set; }
    public double Viento { get; set; }
    public string IconoClima { get; set; } = "🌤";
}