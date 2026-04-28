using Microsoft.AspNetCore.Mvc;
using DataFusionArena.Shared.Models;
using DataFusionArena.Web.Models;
using DataFusionArena.Web.Services;
using System.Text.Json;

namespace DataFusionArena.Web.Controllers;

public class ApiExternaController : Controller
{
    private readonly IHttpClientFactory _http;
    private readonly SessionDataStore _sessionStore;
    private const double Lat = 27.0619;
    private const double Lon = -101.5489;
    private const string Ciudad = "San Buenaventura, México";

    public ApiExternaController(IHttpClientFactory http, SessionDataStore sessionStore)
    {
        _http = http;
        _sessionStore = sessionStore;
    }

    private DataStore Store
    {
        get
        {
            HttpContext.Session.SetString("active", "1");
            return _sessionStore.ObtenerSesion(HttpContext.Session.Id);
        }
    }

    public async Task<IActionResult> Index()
    {
        var vm = await ObtenerClimaAsync();
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> IntegrarDatos()
    {
        var vm = await ObtenerClimaAsync();
        if (!vm.ConError)
        {
            var items = vm.Pronostico.Select((d, i) => new DataItem
            {
                Id = 90000 + i,
                Nombre = $"Temp. {d.Fecha}",
                Categoria = "Clima · Temperatura",
                Valor = d.TempMax,
                Fuente = "api-open-meteo",
                Fecha = DateTime.TryParse(d.Fecha, out var dt) ? dt : DateTime.Now
            }).ToList();

            var precip = vm.Pronostico.Select((d, i) => new DataItem
            {
                Id = 91000 + i,
                Nombre = $"Precipitación {d.Fecha}",
                Categoria = "Clima · Precipitación",
                Valor = d.Precipitacion,
                Fuente = "api-open-meteo",
                Fecha = DateTime.TryParse(d.Fecha, out var dt) ? dt : DateTime.Now
            }).ToList();

            items.AddRange(precip);
            Store.Agregar(items);
            TempData["Ok"] = $" {items.Count} registros de clima integrados al DataSet.";
        }
        else
        {
            TempData["Error"] = $"No se pudo obtener datos: {vm.MensajeError}";
        }
        return RedirectToAction(nameof(Index));
    }

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
            using var doc = JsonDocument.Parse(json);
            var daily = doc.RootElement.GetProperty("daily");

            var fechas = daily.GetProperty("time").EnumerateArray().Select(e => e.GetString()!).ToList();
            var tMax = daily.GetProperty("temperature_2m_max").EnumerateArray().Select(e => e.GetDouble()).ToList();
            var tMin = daily.GetProperty("temperature_2m_min").EnumerateArray().Select(e => e.GetDouble()).ToList();
            var precip = daily.GetProperty("precipitation_sum").EnumerateArray()
                              .Select(e => e.ValueKind == JsonValueKind.Null ? 0.0 : e.GetDouble()).ToList();
            var viento = daily.GetProperty("windspeed_10m_max").EnumerateArray()
                              .Select(e => e.ValueKind == JsonValueKind.Null ? 0.0 : e.GetDouble()).ToList();

            for (int i = 0; i < fechas.Count; i++)
            {
                vm.Pronostico.Add(new DiaClima
                {
                    Fecha = fechas[i],
                    TempMax = i < tMax.Count ? tMax[i] : 0,
                    TempMin = i < tMin.Count ? tMin[i] : 0,
                    Precipitacion = i < precip.Count ? precip[i] : 0,
                    Viento = i < viento.Count ? viento[i] : 0,
                    IconoClima = GetIcono(i < tMax.Count ? tMax[i] : 0, i < precip.Count ? precip[i] : 0)
                });
            }

            vm.Fechas = fechas;
            vm.TempMax = tMax;
            vm.TempMin = tMin;
            vm.Precipitacion = precip;
            vm.Viento = viento;
        }
        catch (Exception ex)
        {
            vm.ConError = true;
            vm.MensajeError = ex.Message;
        }
        return vm;
    }

    private static string GetIcono(double tMax, double precip)
    {
        if (precip > 5) return "🌧";
        if (precip > 0) return "🌦";
        if (tMax > 35) return "🌡";
        if (tMax > 28) return "☀";
        if (tMax > 20) return "🌤";
        return "⛅";
    }
}