using DataFusionArena.Shared.Models;
using DataFusionArena.Shared.Processing;

namespace DataFusionArena.Web.Models;

// ── Home / Datos locales ────────────────────────────────────────
public class HomeViewModel
{
    public List<DataItem>  Datos          { get; set; } = new();
    public int             TotalRegistros { get; set; }
    public int             Pagina         { get; set; } = 1;
    public int             TotalPaginas   { get; set; }
    public string          Busqueda       { get; set; } = "";
    public string          Campo          { get; set; } = "nombre";
    public List<string>    Fuentes        { get; set; } = new();
    public string          FuenteActiva   { get; set; } = "";
}

public class GraficasViewModel
{
    // Datos para Chart.js (serializados a JSON en la vista)
    public List<string> Categorias   { get; set; } = new();
    public List<double> Totales      { get; set; } = new();
    public List<string> Fuentes      { get; set; } = new();
    public List<int>    ConteoFuente { get; set; } = new();
    // Línea de tiempo
    public List<string> Fechas       { get; set; } = new();
    public List<double> ValoresFecha { get; set; } = new();
    public int          TotalItems   { get; set; }
}

// ── API Externa – Open-Meteo (clima) ───────────────────────────
public class ClimaViewModel
{
    public string       Ciudad      { get; set; } = "Monterrey, México";
    public List<DiaClima> Pronostico { get; set; } = new();
    public bool         ConError    { get; set; }
    public string       MensajeError{ get; set; } = "";
    // Datos para Chart.js
    public List<string> Fechas      { get; set; } = new();
    public List<double> TempMax     { get; set; } = new();
    public List<double> TempMin     { get; set; } = new();
    public List<double> Precipitacion{ get; set; } = new();
    public List<double> Viento      { get; set; } = new();
}

public class DiaClima
{
    public string Fecha         { get; set; } = "";
    public double TempMax       { get; set; }
    public double TempMin       { get; set; }
    public double Precipitacion { get; set; }
    public double Viento        { get; set; }
    public string IconoClima    { get; set; } = "🌤";
}
