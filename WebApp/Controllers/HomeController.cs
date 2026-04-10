using Microsoft.AspNetCore.Mvc;
using DataFusionArena.Shared.Models;
using DataFusionArena.Shared.Readers;
using DataFusionArena.Web.Models;
using DataFusionArena.Web.Services;
using System.Text.Json;

namespace DataFusionArena.Web.Controllers;

public class HomeController : Controller
{
    private readonly DataStore _store;
    private const int PageSize = 20;

    public HomeController(DataStore store) => _store = store;

    // ── Tabla de datos ──────────────────────────────────────────
    public IActionResult Index(
        string busqueda = "", string campo = "nombre",
        string fuente = "", int pagina = 1)
    {
        var todos = _store.ObtenerTodos();

        // Filtrar por fuente
        if (!string.IsNullOrEmpty(fuente))
            todos = todos.Where(d => d.Fuente == fuente).ToList();

        // Filtrar por texto
        if (!string.IsNullOrEmpty(busqueda))
            todos = DataFusionArena.Shared.Processing.DataProcessor
                        .Filtrar(todos, campo, busqueda);

        int total = todos.Count;
        int totalPags = (int)Math.Ceiling(total / (double)PageSize);
        pagina = Math.Clamp(pagina, 1, Math.Max(1, totalPags));

        var vm = new HomeViewModel
        {
            Datos          = todos.Skip((pagina - 1) * PageSize).Take(PageSize).ToList(),
            TotalRegistros = total,
            Pagina         = pagina,
            TotalPaginas   = totalPags,
            Busqueda       = busqueda,
            Campo          = campo,
            Fuentes        = _store.Fuentes(),
            FuenteActiva   = fuente
        };
        return View(vm);
    }

    // ── Gráficas ────────────────────────────────────────────────
    public IActionResult Graficas()
    {
        var stats    = _store.Estadisticas();
        var todos    = _store.ObtenerTodos();

        // Por categoría
        var cats   = stats.Values.OrderByDescending(s => s.SumaValores).Take(12).ToList();

        // Por fecha (agrupar por mes)
        var porFecha = todos
            .GroupBy(d => d.Fecha.ToString("yyyy-MM"))
            .OrderBy(g => g.Key)
            .Select(g => (Fecha: g.Key, Total: g.Sum(x => x.Valor)))
            .ToList();

        // Por fuente
        var porFuente = todos
            .GroupBy(d => d.Fuente)
            .Select(g => (Fuente: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var vm = new GraficasViewModel
        {
            Categorias    = cats.Select(s => s.Categoria).ToList(),
            Totales       = cats.Select(s => Math.Round(s.SumaValores, 2)).ToList(),
            Fuentes       = porFuente.Select(x => x.Fuente).ToList(),
            ConteoFuente  = porFuente.Select(x => x.Count).ToList(),
            Fechas        = porFecha.Select(x => x.Fecha).ToList(),
            ValoresFecha  = porFecha.Select(x => Math.Round(x.Total, 2)).ToList(),
            TotalItems    = todos.Count
        };
        return View(vm);
    }

    // ── Cargar archivo desde web ─────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CargarArchivo(IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
        {
            TempData["Error"] = "No se seleccionó ningún archivo.";
            return RedirectToAction(nameof(Index));
        }

        var ext  = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        var tipo = ext.TrimStart('.');
        if (!new[] { ".json", ".csv", ".xml", ".txt" }.Contains(ext))
        {
            TempData["Error"] = $"Formato '{ext}' no soportado. Usa: json, csv, xml, txt";
            return RedirectToAction(nameof(Index));
        }

        var tmp = Path.Combine(Path.GetTempPath(), $"dfa_{Guid.NewGuid()}{ext}");
        try
        {
            await using var fs = System.IO.File.Create(tmp);
            await archivo.CopyToAsync(fs);
        }
        catch
        {
            TempData["Error"] = "Error al guardar el archivo temporal.";
            return RedirectToAction(nameof(Index));
        }

        List<DataItem> nuevos = tipo switch
        {
            "json" => JsonDataReader.Leer(tmp),
            "csv"  => CsvDataReader.Leer(tmp),
            "xml"  => XmlDataReader.Leer(tmp),
            "txt"  => TxtDataReader.Leer(tmp),
            _      => new()
        };

        System.IO.File.Delete(tmp);
        _store.Agregar(nuevos);
        TempData["Ok"] = $"✅ {nuevos.Count} registros cargados desde {archivo.FileName}";
        return RedirectToAction(nameof(Index));
    }

    // ── API endpoint para Chart.js (JSON) ───────────────────────
    [HttpGet("/api/chartdata")]
    public IActionResult ChartData()
    {
        var stats = _store.Estadisticas();
        var data  = stats.Values
            .OrderByDescending(s => s.SumaValores)
            .Take(12)
            .Select(s => new { label = s.Categoria, value = Math.Round(s.SumaValores, 2) });
        return Json(data);
    }

    public IActionResult Error() => View();
}
