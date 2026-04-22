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

    // Límite de tamaño: 200 MB
    private const long MaxFileSizeBytes = 209_715_200L;

    public HomeController(DataStore store) => _store = store;

    public IActionResult Index(
        string busqueda = "", string campo = "",
        string fuente = "", int pagina = 1)
    {
        var todos = _store.ObtenerTodos();
        var columnas = _store.ObtenerColumnas();
        var mapeo = _store.ObtenerMapeo();
        var fuenteDatos = _store.ObtenerFuente();

        if (string.IsNullOrEmpty(campo))
        {
            var colNombre = mapeo.FirstOrDefault(kv =>
                string.Equals(kv.Value, "nombre", StringComparison.OrdinalIgnoreCase)).Key;
            campo = colNombre ?? (columnas.Count > 0 ? columnas[0] : "nombre");
        }

        if (!string.IsNullOrEmpty(fuente))
            todos = todos.Where(d => d.Fuente == fuente).ToList();

        if (!string.IsNullOrEmpty(busqueda))
        {
            string claveInternal = mapeo.TryGetValue(campo, out var mapped)
                ? mapped.ToLower()
                : campo.ToLower();
            todos = DataFusionArena.Shared.Processing.DataProcessor
                        .Filtrar(todos, claveInternal, busqueda);
        }

        int total = todos.Count;
        int totalPags = (int)Math.Ceiling(total / (double)PageSize);
        pagina = Math.Clamp(pagina, 1, Math.Max(1, totalPags));

        var vm = new HomeViewModel
        {
            Datos = todos.Skip((pagina - 1) * PageSize).Take(PageSize).ToList(),
            TotalRegistros = total,
            Pagina = pagina,
            TotalPaginas = totalPags,
            Busqueda = busqueda,
            Campo = campo,
            Fuentes = _store.Fuentes(),
            FuenteActiva = fuente,
            Columnas = columnas,
            Mapeo = mapeo,
            FuenteDatos = fuenteDatos
        };
        return View(vm);
    }

    public IActionResult Graficas()
    {
        var stats = _store.Estadisticas();
        var todos = _store.ObtenerTodos();

        var cats = stats.Values.OrderByDescending(s => s.SumaValores).Take(12).ToList();

        var porFecha = todos
            .GroupBy(d => d.Fecha.ToString("yyyy-MM"))
            .OrderBy(g => g.Key)
            .Select(g => (Fecha: g.Key, Total: g.Sum(x => x.Valor)))
            .ToList();

        var porFuente = todos
            .GroupBy(d => d.Fuente)
            .Select(g => (Fuente: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var vm = new GraficasViewModel
        {
            Categorias = cats.Select(s => s.Categoria).ToList(),
            Totales = cats.Select(s => Math.Round(s.SumaValores, 2)).ToList(),
            Fuentes = porFuente.Select(x => x.Fuente).ToList(),
            ConteoFuente = porFuente.Select(x => x.Count).ToList(),
            Fechas = porFecha.Select(x => x.Fecha).ToList(),
            ValoresFecha = porFecha.Select(x => Math.Round(x.Total, 2)).ToList(),
            TotalItems = todos.Count
        };
        return View(vm);
    }

    [HttpPost]
    [RequestSizeLimit(209_715_200)]
    [RequestFormLimits(MultipartBodyLengthLimit = 209_715_200)]
    public async Task<IActionResult> CargarArchivo(IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
        {
            TempData["Error"] = "No se seleccionó ningún archivo.";
            return RedirectToAction(nameof(Index));
        }

        if (archivo.Length > MaxFileSizeBytes)
        {
            TempData["Error"] = $"El archivo supera el límite de 200 MB ({archivo.Length / 1024 / 1024} MB).";
            return RedirectToAction(nameof(Index));
        }

        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
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
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al guardar el archivo temporal: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }

        List<DataItem> nuevos;
        try
        {
            nuevos = tipo switch
            {
                "json" => JsonDataReader.Leer(tmp),
                "csv" => CsvDataReader.Leer(tmp),
                "xml" => XmlDataReader.Leer(tmp),
                "txt" => TxtDataReader.Leer(tmp),
                _ => new()
            };
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al procesar el archivo '{archivo.FileName}': {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
        finally
        {
            if (System.IO.File.Exists(tmp))
                System.IO.File.Delete(tmp);
        }

        if (nuevos.Count == 0)
        {
            TempData["Error"] = $"No se pudieron leer registros desde '{archivo.FileName}'. Verifica el formato.";
            return RedirectToAction(nameof(Index));
        }

        _store.Agregar(nuevos, tipo);
        TempData["Ok"] = $"✅ {nuevos.Count} registros cargados desde {archivo.FileName}";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("descargar-datos")]
    public IActionResult DescargarDatos(string formato = "csv")
    {
        try
        {
            var datos = _store.ObtenerTodos();
            if (datos.Count == 0)
            {
                TempData["Error"] = "No hay datos para descargar.";
                return RedirectToAction(nameof(Index));
            }

            byte[] contenido;
            string contentType;
            string nombreArchivo;

            if (formato.ToLower() == "excel")
            {
                contenido = ExportService.ExportarExcel(datos);
                contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                nombreArchivo = $"dataset_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.xlsx";
            }
            else
            {
                contenido = ExportService.ExportarCsv(datos);
                contentType = "text/csv; charset=utf-8";
                nombreArchivo = $"dataset_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
            }

            return File(contenido, contentType, nombreArchivo);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al descargar: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet("descargar-estadisticas")]
    public IActionResult DescargarEstadisticas()
    {
        try
        {
            var stats = _store.Estadisticas();
            byte[] contenido = ExportService.ExportarEstadisticasCsv(stats);
            string nombre = $"estadisticas_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
            return File(contenido, "text/csv; charset=utf-8", nombre);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al descargar estadísticas: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet("/api/chartdata")]
    public IActionResult ChartData()
    {
        var stats = _store.Estadisticas();
        var data = stats.Values
            .OrderByDescending(s => s.SumaValores)
            .Take(12)
            .Select(s => new { label = s.Categoria, value = Math.Round(s.SumaValores, 2) });
        return Json(data);
    }

    public IActionResult Error() => View();
}