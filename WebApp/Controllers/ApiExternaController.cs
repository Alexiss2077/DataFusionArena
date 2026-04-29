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

    // Top 50 monedas por capitalización de mercado
    private const string CoinGeckoUrl =
        "https://api.coingecko.com/api/v3/coins/markets" +
        "?vs_currency=usd&order=market_cap_desc&per_page=50&page=1" +
        "&sparkline=false&price_change_percentage=24h";

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
        var vm = await ObtenerCriptomonedasAsync();
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> IntegrarDatos()
    {
        var vm = await ObtenerCriptomonedasAsync();
        if (!vm.ConError)
        {
            var items = vm.Monedas.Select((m, i) => new DataItem
            {
                Id = 92000 + i,
                Nombre = m.Nombre,
                Categoria = m.Categoria,
                Valor = m.PrecioUsd,
                Fuente = "api-coingecko",
                Fecha = DateTime.UtcNow,
                CamposExtra = new Dictionary<string, string>
                {
                    ["simbolo"] = m.Simbolo,
                    ["cap_mercado_usd"] = m.CapMercado.ToString("F0"),
                    ["cambio_24h_pct"] = m.Cambio24h.ToString("F2"),
                    ["volumen_24h_usd"] = m.Volumen24h.ToString("F0"),
                    ["maximo_24h"] = m.Maximo24h.ToString("F4"),
                    ["minimo_24h"] = m.Minimo24h.ToString("F4"),
                    ["ranking_cap_mercado"] = m.Ranking.ToString()
                }
            }).ToList();

            Store.Agregar(items, "api-coingecko");
            TempData["Ok"] = $"{items.Count} criptomonedas integradas al DataSet.";
        }
        else
        {
            TempData["Error"] = $"No se pudo obtener datos: {vm.MensajeError}";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<CoinGeckoViewModel> ObtenerCriptomonedasAsync()
    {
        var vm = new CoinGeckoViewModel();
        try
        {
            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "DataFusionArena/1.0");
            client.Timeout = TimeSpan.FromSeconds(15);

            var json = await client.GetStringAsync(CoinGeckoUrl);
            using var doc = JsonDocument.Parse(json);

            foreach (var coin in doc.RootElement.EnumerateArray())
            {
                vm.Monedas.Add(new MonedaInfo
                {
                    Id = coin.GetProperty("id").GetString() ?? "",
                    Nombre = coin.GetProperty("name").GetString() ?? "",
                    Simbolo = coin.GetProperty("symbol").GetString()?.ToUpper() ?? "",
                    Categoria = ClasificarMoneda(
                        coin.GetProperty("id").GetString() ?? "",
                        coin.GetProperty("market_cap_rank").ValueKind != JsonValueKind.Null
                            ? coin.GetProperty("market_cap_rank").GetInt32() : 99),
                    PrecioUsd = GetDouble(coin, "current_price"),
                    CapMercado = GetDouble(coin, "market_cap"),
                    Cambio24h = GetDouble(coin, "price_change_percentage_24h"),
                    Volumen24h = GetDouble(coin, "total_volume"),
                    Maximo24h = GetDouble(coin, "high_24h"),
                    Minimo24h = GetDouble(coin, "low_24h"),
                    Ranking = coin.GetProperty("market_cap_rank").ValueKind != JsonValueKind.Null
                                    ? coin.GetProperty("market_cap_rank").GetInt32() : 99
                });
            }
        }
        catch (Exception ex)
        {
            vm.ConError = true;
            vm.MensajeError = ex.Message;
        }
        return vm;
    }

    private static double GetDouble(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var p)) return 0;
        if (p.ValueKind == JsonValueKind.Null) return 0;
        return p.TryGetDouble(out double v) ? v : 0;
    }

    private static string ClasificarMoneda(string id, int ranking)
    {
        if (ranking <= 5) return "Large Cap";
        if (ranking <= 20) return "Mid Cap";
        if (id is "tether" or "usd-coin" or "dai" or "binance-usd" or "true-usd" or "frax")
            return "Stablecoin";
        return "Small Cap";
    }
}