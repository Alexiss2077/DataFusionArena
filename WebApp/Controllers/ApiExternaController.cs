using Microsoft.AspNetCore.Mvc;
using DataFusionArena.Shared.Models;
using DataFusionArena.Web.Models;
using DataFusionArena.Web.Services;
using System.Text.Json;

namespace DataFusionArena.Web.Controllers;

/// <summary>
/// Nivel Bonus – Nube/API:
/// Consume la API REST de Open-Meteo (https://open-meteo.com)
/// para obtener pronóstico del clima de 14 días.
/// Gratis, sin API key, sin registro.
/// </summary>
public class ApiExternaController : Controller
{
    private readonly IHttpClientFactory _http;
    private readonly DataStore          _store;

    // Coordenadas – san buena
    private const double Lat  = 27.0619;
    private const double Lon  = -101.5489;
    private const string Ciudad = "San Buenaventura, México";

    public ApiExternaController(IHttpClientFactory http, DataStore store)
    {
        _http  = http;
        _store = store;
    }

    // ── Vista principal del clima ────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var vm = await ObtenerClimaAsync();
        return View(vm);
    }

    // ── Integrar datos de clima al DataStore ─────────────────────
    [HttpPost]
    public async Task<IActionResult> IntegrarDatos()
    {
        var vm = await ObtenerClimaAsync();
        if (!vm.ConError)
        {
            // Convertir pronóstico a DataItems
            var items = vm.Pronostico.Select((d, i) => new DataItem
            {
                Id        = 90000 + i,
                Nombre    = $"Temp. {d.Fecha}",
                Categoria = "Clima · Temperatura",
                Valor     = d.TempMax,
                Fuente    = "api-open-meteo",
                Fecha     = DateTime.TryParse(d.Fecha, out var dt) ? dt : DateTime.Now
            }).ToList();

            // También agregar precipitación
            var precip = vm.Pronostico.Select((d, i) => new DataItem
            {
                Id        = 91000 + i,
                Nombre    = $"Precipitación {d.Fecha}",
                Categoria = "Clima · Precipitación",
                Valor     = d.Precipitacion,
                Fuente    = "api-open-meteo",
                Fecha     = DateTime.TryParse(d.Fecha, out var dt) ? dt : DateTime.Now
            }).ToList();

            items.AddRange(precip);
            _store.Agregar(items);
            TempData["Ok"] = $"✅ {items.Count} registros de clima integrados al DataSet.";
        }
        else
        {
            TempData["Error"] = $"No se pudo obtener datos: {vm.MensajeError}";
        }
        return RedirectToAction(nameof(Index));
    }

    // ── Helper: llama a Open-Meteo ───────────────────────────────
    private async Task<ClimaViewModel> ObtenerClimaAsync()
    {
        var vm = new ClimaViewModel { Ciudad = Ciudad };
        var url = $"https://api.open-meteo.com/v1/forecast?" +
                  $"latitude={Lat}&longitude={Lon}" +
                  $"&daily=temperature_2m_max,temperature_2m_min," +
                  $"precipitation_sum,windspeed_10m_max" +
                  $"&timezone=America%2FMonterrey&forecast_days=14";
        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var json = await client.GetStringAsync(url);
            using var doc  = JsonDocument.Parse(json);
            var daily = doc.RootElement.GetProperty("daily");

            var fechas  = daily.GetProperty("time").EnumerateArray().Select(e => e.GetString()!).ToList();
            var tMax    = daily.GetProperty("temperature_2m_max").EnumerateArray().Select(e => e.GetDouble()).ToList();
            var tMin    = daily.GetProperty("temperature_2m_min").EnumerateArray().Select(e => e.GetDouble()).ToList();
            var precip  = daily.GetProperty("precipitation_sum").EnumerateArray()
                               .Select(e => e.ValueKind == JsonValueKind.Null ? 0.0 : e.GetDouble()).ToList();
            var viento  = daily.GetProperty("windspeed_10m_max").EnumerateArray()
                               .Select(e => e.ValueKind == JsonValueKind.Null ? 0.0 : e.GetDouble()).ToList();

            for (int i = 0; i < fechas.Count; i++)
            {
                double max = i < tMax.Count ? tMax[i] : 0;
                double min = i < tMin.Count ? tMin[i] : 0;
                double pr  = i < precip.Count ? precip[i] : 0;
                double vt  = i < viento.Count ? viento[i] : 0;

                vm.Pronostico.Add(new DiaClima
                {
                    Fecha         = fechas[i],
                    TempMax       = max,
                    TempMin       = min,
                    Precipitacion = pr,
                    Viento        = vt,
                    IconoClima    = GetIcono(max, pr)
                });
            }

            vm.Fechas       = fechas;
            vm.TempMax      = tMax;
            vm.TempMin      = tMin;
            vm.Precipitacion= precip;
            vm.Viento       = viento;
        }
        catch (Exception ex)
        {
            vm.ConError     = true;
            vm.MensajeError = ex.Message;
        }
        return vm;
    }

    private static string GetIcono(double tMax, double precip)
    {
        if (precip > 5)  return "🌧";
        if (precip > 0)  return "🌦";
        if (tMax > 35)   return "🌡";
        if (tMax > 28)   return "☀";
        if (tMax > 20)   return "🌤";
        return "⛅";
    }
}
