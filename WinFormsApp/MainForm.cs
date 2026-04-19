using DataFusionArena.Shared.Database;
using DataFusionArena.Shared.Models;
using DataFusionArena.Shared.Processing;
using DataFusionArena.Shared.Readers;
using DataFusionArena.Shared.Services;
using System.Data;
using System.Globalization;
using System.Reflection;

namespace DataFusionArena.WinForms;

public partial class MainForm : Form
{
    private readonly List<DataItem> _datos = new();
    private List<DataItem> _datosBase = new();
    private List<DataItem> _datosVista = new();

    private Dictionary<string, List<DataItem>> _porCategoria = new();
    private Dictionary<int, DataItem> _porId = new();

    private PostgreSqlConnector? _lastPgConnector;
    private MariaDbConnector? _lastMdConnector;

    private const int DISPLAY_LIMIT = 75_000;

    private readonly string _dirDatos = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "SampleData");

    private List<(string Display, string Clave)> _infoColumnas = new()
    {
        ("ID","id"),("Nombre","nombre"),("Categoría","categoria"),
        ("Valor","valor"),("Fecha","fecha"),("Fuente","fuente")
    };

    private string _ultimoTipoCargado = "";

    private static readonly List<(string Display, string Clave)> _colsDefault = new()
    {
        ("ID","id"),("Nombre","nombre"),("Categoría","categoria"),
        ("Valor","valor"),("Fecha","fecha"),("Fuente","fuente")
    };

    private readonly HashSet<string> _numericDisplays = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _currencyDisplays = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] _kwMoneda = {
        "price","precio","monto","costo","cost","revenue",
        "salary","salario","ventas","sales","importe","amount",
        "fee","wage","income","ingreso","earning","pago","payment","ganancia"
    };

    private static bool EsMonedaDisplay(string display) =>
        _kwMoneda.Any(k => display.ToLower().Contains(k));

    // ══════════════════════════════════════════════════════════════
    //  CONSTRUCTOR
    // ══════════════════════════════════════════════════════════════
    public MainForm()
    {
        InitializeComponent();
        ConfigurarDataGridViews();
        AplicarDoubleBuffer(dgvTodos);
        AplicarDoubleBuffer(dgvCategoria);
        AplicarDoubleBuffer(dgvProcesamiento);
        AplicarDoubleBuffer(dgvEstadisticas);

        ActualizarEstadoBarra("Listo. Cargue datos usando el menú o los botones.");
        Text = "Data Fusion Arena – Administración y Organización de Datos";

        Load += (s, e) =>
        {
            splitMain.SplitterDistance = 170;
            splitCategoria.SplitterDistance = 220;
        };

        tabControl1.SelectedIndexChanged += (s, e) =>
        {
            if (tabControl1.SelectedTab == tabGraficas)
                ActualizarChart();
        };
    }

    // ══════════════════════════════════════════════════════════════
    //  COLUMNAS / COMBOBOXES
    // ══════════════════════════════════════════════════════════════

    private string TraducirClave(string display)
    {
        foreach (var (d, c) in _infoColumnas)
            if (string.Equals(d, display, StringComparison.OrdinalIgnoreCase))
                return c;
        return display.ToLower();
    }

    private void ReconstruirInfoColumnas()
    {
        _infoColumnas.Clear();

        bool usarPg = _ultimoTipoCargado == "postgresql" && (_lastPgConnector?.UltimasColumnas.Count ?? 0) > 0;
        bool usarMd = _ultimoTipoCargado == "mariadb" && (_lastMdConnector?.UltimasColumnas.Count ?? 0) > 0;
        bool usarCsv = _ultimoTipoCargado == "csv" && CsvDataReader.UltimasColumnas.Count > 0;
        bool usarJson = _ultimoTipoCargado == "json" && JsonDataReader.UltimasColumnas.Count > 0;
        bool usarXml = _ultimoTipoCargado == "xml" && XmlDataReader.UltimasColumnas.Count > 0;
        bool usarTxt = _ultimoTipoCargado == "txt" && TxtDataReader.UltimasColumnas.Count > 0;

        if (usarPg) BuildFromConnector(_lastPgConnector!.UltimasColumnas, _lastPgConnector.MapeoColumnas);
        else if (usarMd) BuildFromConnector(_lastMdConnector!.UltimasColumnas, _lastMdConnector.MapeoColumnas);
        else if (usarCsv) BuildFromReader(CsvDataReader.UltimasColumnas, CsvDataReader.MapeoColumnas);
        else if (usarJson) BuildFromReader(JsonDataReader.UltimasColumnas, JsonDataReader.MapeoColumnas);
        else if (usarXml) BuildFromReader(XmlDataReader.UltimasColumnas, XmlDataReader.MapeoColumnas);
        else if (usarTxt) BuildFromReader(TxtDataReader.UltimasColumnas, TxtDataReader.MapeoColumnas);
        else
        {
            foreach (var col in _colsDefault) _infoColumnas.Add(col);
            var ya = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "id","nombre","categoria","valor","fecha","fuente" };
            foreach (var k in _datos
                .SelectMany(d => d.CamposExtra.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(k => !ya.Contains(k.ToLower()))
                .OrderBy(k => k))
                _infoColumnas.Add((k, k.ToLower()));
        }
    }

    private void BuildFromReader(List<string> columnas, Dictionary<string, string> mapeo)
    {
        var ya = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in columnas)
        {
            string clave = mapeo.TryGetValue(col, out var p) ? p.ToLower() : col.ToLowerInvariant();
            _infoColumnas.Add((col, clave));
            ya.Add(col.ToLowerInvariant());
        }
        if (!ya.Contains("fuente")) _infoColumnas.Add(("Fuente", "fuente"));
    }

    private void BuildFromConnector(List<string> columnas, Dictionary<string, string> mapeo)
    {
        var ya = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in columnas)
        {
            string clave = mapeo.TryGetValue(col, out var p) ? p.ToLower() : col.ToLowerInvariant();
            _infoColumnas.Add((col, clave));
            ya.Add(col.ToLowerInvariant());
        }
        if (!ya.Contains("fuente")) _infoColumnas.Add(("Fuente", "fuente"));
    }

    private void RefrescarComboboxes()
    {
        var items = _infoColumnas.Select(c => c.Display).Distinct().ToArray<object>();

        string pf = cmbCampoBusqueda.Text;
        cmbCampoBusqueda.Items.Clear(); cmbCampoBusqueda.Items.AddRange(items);
        int f = cmbCampoBusqueda.FindStringExact(pf);
        cmbCampoBusqueda.SelectedIndex = f >= 0 ? f : 0;

        string po = cmbCampoOrden.Text;
        cmbCampoOrden.Items.Clear(); cmbCampoOrden.Items.AddRange(items);
        int o = cmbCampoOrden.FindStringExact(po);
        if (o < 0) o = _infoColumnas.FindIndex(c => c.Clave == "valor");
        cmbCampoOrden.SelectedIndex = Math.Max(0, o);

        if (cmbLinqCampo != null)
        {
            string pl = cmbLinqCampo.Text;
            cmbLinqCampo.Items.Clear(); cmbLinqCampo.Items.AddRange(items);
            int l = cmbLinqCampo.FindStringExact(pl);
            cmbLinqCampo.SelectedIndex = Math.Max(0, l >= 0 ? l : 0);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  DETECCIÓN NUMÉRICA Y DE MONEDA
    // ══════════════════════════════════════════════════════════════

    private void DetectarNumericosYMoneda()
    {
        _numericDisplays.Clear();
        _currencyDisplays.Clear();

        var sample = (_datosBase.Count > 0 ? _datosBase : _datos).Take(40).ToList();

        foreach (var (display, clave) in _infoColumnas)
        {
            if (clave is "id" or "nombre" or "categoria" or "fecha" or "fuente") continue;

            if (clave == "valor")
            {
                if (EsMonedaDisplay(display)) _currencyDisplays.Add(display);
                continue;
            }

            int num = 0, total = 0;
            foreach (var item in sample)
            {
                string v = BuscarExtra(item, clave);
                if (string.IsNullOrEmpty(v)) continue;
                total++;
                if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) num++;
            }
            if (total > 0 && num >= total * 0.75)
            {
                _numericDisplays.Add(display);
                if (EsMonedaDisplay(display)) _currencyDisplays.Add(display);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  GRÁFICA INTELIGENTE
    // ══════════════════════════════════════════════════════════════

    private void RefrescarCombosGrafica()
    {
        if (cmbGrupoGrafica == null || cmbMetricaGrafica == null) return;

        string prevGrupo = cmbGrupoGrafica.Text;
        string prevMetrica = cmbMetricaGrafica.Text;

        cmbGrupoGrafica.Items.Clear();
        cmbMetricaGrafica.Items.Clear();
        cmbMetricaGrafica.Items.Add("Contar registros");

        foreach (var (display, clave) in _infoColumnas)
        {
            bool esNumerico = clave is "id" or "valor" || _numericDisplays.Contains(display);
            if (!esNumerico && clave != "fecha") cmbGrupoGrafica.Items.Add(display);
            if (esNumerico && clave != "id") cmbMetricaGrafica.Items.Add(display);
        }

        int gi = cmbGrupoGrafica.FindStringExact(prevGrupo);
        if (gi < 0) { gi = cmbGrupoGrafica.FindStringExact("Categoría"); if (gi < 0 && cmbGrupoGrafica.Items.Count > 0) gi = 0; }
        if (gi >= 0 && gi < cmbGrupoGrafica.Items.Count) cmbGrupoGrafica.SelectedIndex = gi;

        int mi = cmbMetricaGrafica.FindStringExact(prevMetrica);
        if (mi < 0) mi = cmbMetricaGrafica.Items.Count > 1 ? 1 : 0;
        if (mi >= 0 && mi < cmbMetricaGrafica.Items.Count) cmbMetricaGrafica.SelectedIndex = mi;
    }

    private void ActualizarChart()
    {
        try
        {
            var fuente = _datosBase.Count > 0 ? _datosBase : _datos.Count > 0 ? _datos : null;
            if (fuente == null || fuente.Count == 0) { chartMain.Limpiar(); return; }

            string grupoDisplay = cmbGrupoGrafica?.Text ?? "";
            string metricaDisplay = cmbMetricaGrafica?.Text ?? "";
            bool contar = metricaDisplay == "Contar registros" || string.IsNullOrEmpty(metricaDisplay);

            string grupoClv = string.IsNullOrEmpty(grupoDisplay)
                ? "categoria"
                : _infoColumnas.FirstOrDefault(c => c.Display == grupoDisplay).Clave ?? "categoria";

            string metricaClv = contar ? ""
                : _infoColumnas.FirstOrDefault(c => c.Display == metricaDisplay).Clave ?? "valor";

            string GetGrupo(DataItem item) => grupoClv switch
            {
                "nombre" => item.Nombre,
                "categoria" => string.IsNullOrWhiteSpace(item.Categoria) ? "(sin categoría)" : item.Categoria,
                "fuente" => item.Fuente,
                "fecha" => item.Fecha.ToString("yyyy-MM"),
                _ => BuscarExtra(item, grupoClv) is { Length: > 0 } ev ? ev : "(vacío)"
            };

            double GetValor(DataItem item)
            {
                if (contar) return 1;
                return metricaClv switch
                {
                    "valor" => item.Valor,
                    "id" => item.Id,
                    _ => double.TryParse(BuscarExtra(item, metricaClv),
                                    NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0
                };
            }

            var agrupado = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in fuente)
            {
                string grupo = GetGrupo(item);
                if (!agrupado.ContainsKey(grupo)) agrupado[grupo] = 0;
                agrupado[grupo] += GetValor(item);
            }

            var data = agrupado
                .OrderByDescending(kv => kv.Value)
                .Take(12)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();

            if (data.Count == 0) { chartMain.Limpiar(); return; }

            string metricaLabel = contar ? "Conteo" : metricaDisplay;
            string grupoLabel = string.IsNullOrEmpty(grupoDisplay) ? "Categoría" : grupoDisplay;

            var tipo = cmbTipoGrafica.Text switch
            {
                "Barras" => TipoGrafica.Barras,
                "Pastel" => TipoGrafica.Pastel,
                _ => TipoGrafica.Columnas
            };

            chartMain.SetData(data, tipo, $"{metricaLabel}  por  {grupoLabel}");
        }
        catch (Exception ex)
        { chartMain.Limpiar(); ActualizarEstadoBarra($"⚠ Error gráfica: {ex.Message}"); }
    }

    private void BtnActualizarGrafica_Click(object sender, EventArgs e) => ActualizarChart();
    private void CmbTipoGrafica_SelectedIndexChanged(object sender, EventArgs e) => ActualizarChart();
    private void CmbGrupoGrafica_SelectedIndexChanged(object sender, EventArgs e) => ActualizarChart();
    private void CmbMetricaGrafica_SelectedIndexChanged(object sender, EventArgs e) => ActualizarChart();

    // ══════════════════════════════════════════════════════════════
    //  CARGA ARCHIVOS
    // ══════════════════════════════════════════════════════════════

    private async void BtnCargarJson_Click(object? sender, EventArgs e) =>
        await CargarArchivoAsync(Path.Combine(_dirDatos, "products.json"), "json");
    private async void BtnCargarCsv_Click(object? sender, EventArgs e) =>
        await CargarArchivoAsync(Path.Combine(_dirDatos, "sales.csv"), "csv");
    private async void BtnCargarXml_Click(object? sender, EventArgs e) =>
        await CargarArchivoAsync(Path.Combine(_dirDatos, "employees.xml"), "xml");
    private async void BtnCargarTxt_Click(object? sender, EventArgs e) =>
        await CargarArchivoAsync(Path.Combine(_dirDatos, "records.txt"), "txt");

    private async void BtnCargarTodo_Click(object? sender, EventArgs e)
    {
        await CargarArchivoAsync(Path.Combine(_dirDatos, "products.json"), "json", true);
        await CargarArchivoAsync(Path.Combine(_dirDatos, "sales.csv"), "csv", true);
        await CargarArchivoAsync(Path.Combine(_dirDatos, "employees.xml"), "xml", true);
        await CargarArchivoAsync(Path.Combine(_dirDatos, "records.txt"), "txt", true);
        _ultimoTipoCargado = "";
        await ActualizarTodoAsync();
        ActualizarEstadoBarra($"✅ Todos los archivos cargados. Total: {_datos.Count} registros.");
        MessageBox.Show($"Archivos cargados.\nTotal: {_datos.Count} registros.",
            "Data Fusion Arena", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void MenuCargarPersonalizado_Click(object sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Selecciona un archivo de datos",
            Filter = "Archivos soportados|*.json;*.csv;*.xml;*.txt|JSON|*.json|CSV|*.csv|XML|*.xml|TXT|*.txt"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            await CargarArchivoAsync(dlg.FileName,
                Path.GetExtension(dlg.FileName).TrimStart('.').ToLower());
    }

    private async Task CargarArchivoAsync(string ruta, string tipo, bool silencioso = false)
    {
        if (!File.Exists(ruta))
        {
            if (!silencioso)
                MessageBox.Show($"Archivo no encontrado:\n{ruta}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        ActualizarEstadoBarra($"⏳ Leyendo {Path.GetFileName(ruta)}...");
        var nuevos = await Task.Run(() =>
        {
            switch (tipo)
            {
                case "json": return JsonDataReader.Leer(ruta);
                case "csv": return CsvDataReader.Leer(ruta);
                case "xml":
                    var items = XmlDataReader.Leer(ruta);
                    foreach (var it in items)
                    {
                        if (it.CamposExtra.TryGetValue("departamento", out var dep))
                        { it.Categoria = dep; it.CamposExtra.Remove("departamento"); }
                        if (it.CamposExtra.TryGetValue("salario", out var sal) &&
                            double.TryParse(sal, NumberStyles.Any, CultureInfo.InvariantCulture, out double sv))
                        { it.Valor = sv; it.CamposExtra.Remove("salario"); }
                    }
                    return items;
                case "txt": return TxtDataReader.Leer(ruta);
                default: return new List<DataItem>();
            }
        });
        DataProcessor.AgregarDatos(_datos, nuevos);
        _ultimoTipoCargado = tipo;
        await ActualizarTodoAsync();
        if (!silencioso)
            ActualizarEstadoBarra(
                $"✅ {nuevos.Count} registros cargados desde {Path.GetFileName(ruta)}. Total: {_datos.Count}");
    }

    // ══════════════════════════════════════════════════════════════
    //  EXPORTAR A BASE DE DATOS ⭐ NUEVO
    // ══════════════════════════════════════════════════════════════

    private async void BtnExportarBD_Click(object? sender, EventArgs e)
    {
        var datos = _datosBase.Count > 0 ? _datosBase : _datos;
        if (datos.Count == 0)
        {
            MessageBox.Show(
                "No hay datos en memoria para enviar.\nCarga un archivo primero (JSON, CSV, XML, TXT).",
                "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Snapshot de _infoColumnas en el momento del click
        // Es la misma fuente que usa el DataGridView y los exportadores de archivo,
        // así que la tabla en BD tendrá exactamente las mismas columnas que se ven en pantalla.
        var infoColumnas = new List<(string Display, string Clave)>(_infoColumnas);

        using var dlg = new FormExportarBD(datos.Count);
        if (dlg.ShowDialog() != DialogResult.OK) return;

        ActualizarEstadoBarra($"⏳ Enviando {datos.Count} registros a {dlg.Motor}...");
        tsBtnExportarBD.Enabled = false;

        var progreso = new Progress<int>(pct =>
            ActualizarEstadoBarra($"⏳ Enviando a BD... {pct}%"));

        try
        {
            WriteResult result;
            var snapshot = new List<DataItem>(datos);

            if (dlg.Motor == "PostgreSQL")
            {
                result = await DatabaseWriter.EscribirEnPostgreSQLAsync(
                    dlg.CadenaConexion, dlg.NombreTabla, snapshot, infoColumnas, progreso);
            }
            else
            {
                result = await DatabaseWriter.EscribirEnMariaDBAsync(
                    dlg.CadenaConexion, dlg.NombreTabla, snapshot, infoColumnas, progreso);
            }

            ActualizarEstadoBarra(result.Mensaje);
            MessageBox.Show(
                $"{result.Mensaje}\n\n" +
                $"Motor:      {dlg.Motor}\n" +
                $"Tabla:      {dlg.NombreTabla}\n" +
                $"Columnas:   {infoColumnas.Count}\n" +
                $"Insertados: {result.Insertados:N0}\n" +
                $"Errores:    {result.Errores}",
                "Exportar a Base de Datos",
                MessageBoxButtons.OK,
                result.Exito ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            ActualizarEstadoBarra($"❌ Error al exportar a BD: {ex.Message}");
            MessageBox.Show($"Error inesperado:\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            tsBtnExportarBD.Enabled = _datosBase.Count > 0 || _datos.Count > 0;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  EXPORTAR ARCHIVOS
    // ══════════════════════════════════════════════════════════════

    private void BtnExportarCsv_Click(object? sender, EventArgs e) => _ = ExportarAsync("csv");
    private void BtnExportarJson_Click(object? sender, EventArgs e) => _ = ExportarAsync("json");
    private void BtnExportarXml_Click(object? sender, EventArgs e) => _ = ExportarAsync("xml");
    private void BtnExportarTxt_Click(object? sender, EventArgs e) => _ = ExportarAsync("txt");

    private async Task ExportarAsync(string formato)
    {
        var datos = _datosVista.Count > 0 ? _datosVista : _datosBase;
        if (datos.Count == 0)
        {
            MessageBox.Show("No hay datos para exportar.\nCarga archivos o conecta una base de datos primero.",
                "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string ext = formato;
        string filter = formato switch
        {
            "csv" => "CSV (*.csv)|*.csv",
            "json" => "JSON (*.json)|*.json",
            "xml" => "XML (*.xml)|*.xml",
            "txt" => "TXT pipe-separated (*.txt)|*.txt",
            _ => "Todos|*.*"
        };

        using var dlg = new SaveFileDialog
        {
            Title = $"Exportar datos a {formato.ToUpper()}",
            Filter = filter,
            FileName = $"DataFusionArena_Export_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        if (dlg.ShowDialog() != DialogResult.OK) return;

        var columnas = _infoColumnas.Select(c => c.Display).ToList();
        var mapeo = _infoColumnas.ToDictionary(
            c => c.Display, c => c.Clave, StringComparer.OrdinalIgnoreCase);

        ActualizarEstadoBarra($"💾 Exportando {datos.Count} registros a {formato.ToUpper()}...");

        tsBtnExportCsv.Enabled = false;
        tsBtnExportJson.Enabled = false;
        tsBtnExportXml.Enabled = false;
        tsBtnExportTxt.Enabled = false;

        try
        {
            var snapshot = new List<DataItem>(datos);
            await Task.Run(() =>
            {
                switch (formato)
                {
                    case "csv": FileExportService.ExportarCsv(dlg.FileName, snapshot, columnas, mapeo); break;
                    case "json": FileExportService.ExportarJson(dlg.FileName, snapshot, columnas, mapeo); break;
                    case "xml": FileExportService.ExportarXml(dlg.FileName, snapshot, columnas, mapeo); break;
                    case "txt": FileExportService.ExportarTxt(dlg.FileName, snapshot, columnas, mapeo); break;
                }
            });

            long bytes = new FileInfo(dlg.FileName).Length;
            string size = bytes >= 1_048_576
                ? $"{bytes / 1_048_576.0:F1} MB"
                : $"{bytes / 1024.0:F0} KB";

            ActualizarEstadoBarra($"✅ Exportado: {Path.GetFileName(dlg.FileName)} ({size})");
            MessageBox.Show(
                $"✅ Exportado exitosamente\n\n" +
                $"Formato:    {formato.ToUpper()}\n" +
                $"Registros:  {snapshot.Count:N0}\n" +
                $"Tamaño:     {size}\n\n" +
                $"📁 {dlg.FileName}",
                "Exportar", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ActualizarEstadoBarra($"❌ Error al exportar: {ex.Message}");
            MessageBox.Show($"Error al exportar:\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            tsBtnExportCsv.Enabled = true;
            tsBtnExportJson.Enabled = true;
            tsBtnExportXml.Enabled = true;
            tsBtnExportTxt.Enabled = true;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  CONEXIÓN BD (LECTURA)
    // ══════════════════════════════════════════════════════════════

    private async void BtnConectarPostgres_Click(object sender, EventArgs e)
    {
        using var dlg = new FormConexionBD("PostgreSQL");
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var pg = new PostgreSqlConnector(dlg.CadenaConexion, dlg.NombreTabla);
        ActualizarEstadoBarra("🔌 Conectando a PostgreSQL...");

        bool ok = await Task.Run(() => pg.ProbarConexion(out _));
        if (!ok)
        {
            pg.ProbarConexion(out string err);
            MessageBox.Show($"Error:\n{err}", "PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ActualizarEstadoBarra("❌ Error PostgreSQL."); return;
        }

        var cols = await Task.Run(() => pg.ObtenerNombresColumnas());
        using var dlgCols = new FormSeleccionColumnas(cols, pg.MapeoColumnas);
        if (dlgCols.ShowDialog() != DialogResult.OK) return;

        pg.SobreescribirMapeo(dlgCols.ColCategoria, dlgCols.ColValor,
                              dlgCols.ColNombre, dlgCols.ColFecha);

        ActualizarEstadoBarra("⏳ Cargando datos PostgreSQL...");
        var datos = await Task.Run(() => pg.LeerDatos());

        _lastPgConnector = pg;
        _ultimoTipoCargado = "postgresql";
        _datos.RemoveAll(d => d.Fuente == "postgresql");
        DataProcessor.AgregarDatos(_datos, datos);
        await ActualizarTodoAsync();
        ActualizarEstadoBarra($"✅ PostgreSQL: {datos.Count} registros cargados.");
    }

    private async void BtnConectarMariaDB_Click(object sender, EventArgs e)
    {
        using var dlg = new FormConexionBD("MariaDB");
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var md = new MariaDbConnector(dlg.CadenaConexion, dlg.NombreTabla);
        ActualizarEstadoBarra("🔌 Conectando a MariaDB...");

        bool ok = await Task.Run(() => md.ProbarConexion(out _));
        if (!ok)
        {
            md.ProbarConexion(out string err);
            MessageBox.Show($"Error:\n{err}", "MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ActualizarEstadoBarra("❌ Error MariaDB."); return;
        }

        var cols = await Task.Run(() => md.ObtenerNombresColumnas());
        using var dlgCols = new FormSeleccionColumnas(cols, md.MapeoColumnas);
        if (dlgCols.ShowDialog() != DialogResult.OK) return;

        md.SobreescribirMapeo(dlgCols.ColCategoria, dlgCols.ColValor,
                              dlgCols.ColNombre, dlgCols.ColFecha);

        ActualizarEstadoBarra("⏳ Cargando datos MariaDB...");
        var datos = await Task.Run(() => md.LeerDatos());

        _lastMdConnector = md;
        _ultimoTipoCargado = "mariadb";
        _datos.RemoveAll(d => d.Fuente == "mariadb");
        DataProcessor.AgregarDatos(_datos, datos);
        await ActualizarTodoAsync();
        ActualizarEstadoBarra($"✅ MariaDB: {datos.Count} registros cargados.");
    }

    private async void BtnRefresh_Click(object sender, EventArgs e)
    {
        if (_lastPgConnector == null && _lastMdConnector == null)
        {
            MessageBox.Show("No hay bases de datos conectadas.",
                "Sin conexión", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _datos.RemoveAll(d => d.Fuente is "postgresql" or "mariadb");
        if (_lastPgConnector != null)
        {
            ActualizarEstadoBarra("♻ Actualizando PostgreSQL...");
            DataProcessor.AgregarDatos(_datos, await Task.Run(() => _lastPgConnector.LeerDatos()));
        }
        if (_lastMdConnector != null)
        {
            ActualizarEstadoBarra("♻ Actualizando MariaDB...");
            DataProcessor.AgregarDatos(_datos, await Task.Run(() => _lastMdConnector.LeerDatos()));
        }
        _ultimoTipoCargado = (_lastPgConnector != null && _lastMdConnector == null) ? "postgresql"
                           : (_lastMdConnector != null && _lastPgConnector == null) ? "mariadb" : "";
        await ActualizarTodoAsync();
        ActualizarEstadoBarra($"✅ Datos actualizados. Total: {_datos.Count}");
    }

    // ══════════════════════════════════════════════════════════════
    //  FILTRAR / ORDENAR
    // ══════════════════════════════════════════════════════════════

    private async void BtnFiltrar_Click(object? sender, EventArgs e)
    {
        string display = cmbCampoBusqueda.Text, clave = TraducirClave(display),
               valor = txtBusqueda.Text.Trim();
        ActualizarEstadoBarra("🔍 Filtrando...");
        _datosVista = string.IsNullOrEmpty(valor)
            ? new List<DataItem>(_datosBase)
            : await Task.Run(() => DataProcessor.Filtrar(_datosBase, clave, valor));
        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        ActualizarEstadoBarra($"Filtro '{display}'='{valor}' → {_datosVista.Count} resultados.");
    }

    private async void BtnLimpiarFiltro_Click(object? sender, EventArgs e)
    {
        txtBusqueda.Text = "";
        _datosVista = new List<DataItem>(_datosBase);
        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        ActualizarEstadoBarra($"Filtro limpiado. {_datosVista.Count} registros.");
    }

    private async void BtnOrdenar_Click(object? sender, EventArgs e)
    {
        string display = cmbCampoOrden.Text, clave = TraducirClave(display);
        bool asc = rbAscendente.Checked;
        ActualizarEstadoBarra("⏳ Ordenando...");
        _datosVista = await Task.Run(() => DataProcessor.Ordenar(_datosVista, clave, asc));
        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        ActualizarEstadoBarra($"Ordenado por '{display}' {(asc ? "↑" : "↓")}. {_datosVista.Count} registros.");
    }

    // ══════════════════════════════════════════════════════════════
    //  CATEGORÍAS
    // ══════════════════════════════════════════════════════════════

    private async void LstCategorias_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (lstCategorias.SelectedItem is not string cat) return;
        if (!_porCategoria.TryGetValue(cat, out var lista)) return;
        await BindGridAsync(dgvCategoria, lista, null);
        lblCatInfo.Text =
            $"  {cat}   |   {lista.Count} registros   |   " +
            $"Promedio: {(lista.Count > 0 ? lista.Average(x => x.Valor) : 0):F2}   |   " +
            $"Total: {lista.Sum(x => x.Valor):N2}";
    }

    // ══════════════════════════════════════════════════════════════
    //  ESTADÍSTICAS
    // ══════════════════════════════════════════════════════════════

    private async Task ActualizarTabEstadisticasAsync()
    {
        if (dgvEstadisticas.Columns.Count == 0)
        {
            foreach (var (h, w, a) in new (string, int, DataGridViewAutoSizeColumnMode)[]
            {
                ("Categoría", 0,   DataGridViewAutoSizeColumnMode.Fill),
                ("Cant.",      65,  DataGridViewAutoSizeColumnMode.None),
                ("Promedio",   100, DataGridViewAutoSizeColumnMode.None),
                ("Máximo",     100, DataGridViewAutoSizeColumnMode.None),
                ("Mínimo",     100, DataGridViewAutoSizeColumnMode.None),
                ("Total",      120, DataGridViewAutoSizeColumnMode.None),
            })
            {
                var col = new DataGridViewTextBoxColumn
                { HeaderText = h, ReadOnly = true, AutoSizeMode = a, MinimumWidth = 55 };
                if (a == DataGridViewAutoSizeColumnMode.None) col.Width = w;
                dgvEstadisticas.Columns.Add(col);
            }
            AplicarEstiloGrid(dgvEstadisticas);
        }
        if (_datosBase.Count == 0) { dgvEstadisticas.Rows.Clear(); return; }
        var stats = await Task.Run(() => DataProcessor.CalcularEstadisticas(_datosBase));
        dgvEstadisticas.Rows.Clear();
        foreach (var s in stats.Values.OrderByDescending(x => x.SumaValores))
            dgvEstadisticas.Rows.Add(s.Categoria, s.Cantidad,
                s.Promedio.ToString("F2"), s.ValorMaximo.ToString("F2"),
                s.ValorMinimo.ToString("F2"), s.SumaValores.ToString("N2"));
        lblTotalRegistros.Text = $"Total registros: {_datos.Count}";
        lblTotalCategorias.Text = $"Categorías: {stats.Count}";
        lblTotalFuentes.Text = $"Fuentes activas: {_datosBase.Select(d => d.Fuente).Distinct().Count()}";
    }

    // ══════════════════════════════════════════════════════════════
    //  PROCESAMIENTO / LINQ
    // ══════════════════════════════════════════════════════════════

    private async void BtnDetectarDuplicados_Click(object? sender, EventArgs e)
    {
        ActualizarEstadoBarra("🔍 Detectando duplicados...");
        var dupes = await Task.Run(() => DataProcessor.DetectarDuplicados(_datos));
        await BindGridAsync(dgvProcesamiento, dupes, null);
        lblProcInfo.Text = dupes.Count == 0
            ? "✅ No se encontraron duplicados."
            : $"⚠ {dupes.Count} duplicados encontrados.";
        if (dupes.Count > 0) btnEliminarDuplicados.Enabled = true;
        ActualizarEstadoBarra($"Duplicados: {dupes.Count}");
    }

    private async void BtnEliminarDuplicados_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("¿Eliminar duplicados? No se puede deshacer.", "Confirmar",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        int antes = _datos.Count;
        var limpia = await Task.Run(() => DataProcessor.EliminarDuplicados(_datos));
        _datos.Clear(); _datos.AddRange(limpia);
        await ActualizarTodoAsync();
        lblProcInfo.Text = $"✅ Eliminados {antes - _datos.Count}. Quedan {_datos.Count}.";
        btnEliminarDuplicados.Enabled = false;
    }

    private static string Normalizar(string t)
    {
        if (string.IsNullOrEmpty(t)) return "";
        var fd = t.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(fd.Length);
        foreach (char c in fd)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    private static bool ContieneNorm(string t, string b) =>
        Normalizar(t).Contains(Normalizar(b));

    private async void BtnLinqWhere_Click(object? sender, EventArgs e)
    {
        string busqueda = txtLinqFiltro.Text.Trim(),
               display = cmbLinqCampo?.Text ?? "",
               clave = TraducirClave(display);
        if (string.IsNullOrEmpty(busqueda))
        { lblProcInfo.Text = "⚠ Escribe un término antes de usar .Where()"; return; }
        var res = await Task.Run(() =>
            _datosBase.Where(d => clave switch
            {
                "nombre" => ContieneNorm(d.Nombre, busqueda),
                "fuente" => ContieneNorm(d.Fuente, busqueda),
                "id" => int.TryParse(busqueda, out int n) ? d.Id == n : d.Id.ToString().Contains(busqueda),
                "categoria" => ContieneNorm(d.Categoria, busqueda),
                "valor" => d.Valor.ToString("F2").Contains(busqueda),
                "fecha" => d.Fecha.ToString("yyyy-MM-dd").Contains(busqueda),
                _ => d.CamposExtra.TryGetValue(clave, out var ev) && ContieneNorm(ev, busqueda)
            }).ToList());
        await BindGridAsync(dgvProcesamiento, res, null);
        lblProcInfo.Text = res.Count > 0
            ? $"✅ LINQ .Where() → {res.Count} registro(s)"
            : "⚠  LINQ .Where() → Sin resultados";
        ActualizarEstadoBarra($"LINQ .Where(): {res.Count} resultados.");
    }

    private async void BtnLinqGroupBy_Click(object? sender, EventArgs e)
    {
        var grupos = await Task.Run(() =>
            DataProcessor.AgruparLinq(_datosBase).OrderByDescending(g => g.Count())
                .Select(g => new DataItem
                {
                    Id = g.Count(),
                    Nombre = g.Key,
                    Categoria = $"{g.Count()} registros",
                    Valor = Math.Round(g.Average(x => x.Valor), 2),
                    Fuente = "LINQ GroupBy",
                    Fecha = DateTime.Now
                }).ToList());
        await BindGridAsync(dgvProcesamiento, grupos, null, usarColsDefault: true);
        lblProcInfo.Text = $"✅ LINQ .GroupBy() → {grupos.Count} grupo(s)";
        ActualizarEstadoBarra($"LINQ .GroupBy(): {grupos.Count} grupos.");
    }

    private async void BtnLinqOrderBy_Click(object? sender, EventArgs e)
    {
        var ord = await Task.Run(() => DataProcessor.OrdenarLinq(_datosBase).ToList());
        await BindGridAsync(dgvProcesamiento, ord, null);
        lblProcInfo.Text = $"✅ LINQ .OrderByDescending(Valor) → {ord.Count} registros";
        ActualizarEstadoBarra($"LINQ .OrderBy(): {ord.Count} registros.");
    }

    private void BtnLinqLimpiar_Click(object? sender, EventArgs e)
    {
        txtLinqFiltro.Text = "";
        dgvProcesamiento.DataSource = null;
        dgvProcesamiento.Columns.Clear();
        lblProcInfo.Text = "Resultados limpiados.";
        btnEliminarDuplicados.Enabled = false;
    }

    // ══════════════════════════════════════════════════════════════
    //  ACTUALIZAR TODO
    // ══════════════════════════════════════════════════════════════

    private async Task ActualizarTodoAsync()
    {
        _porCategoria = DataProcessor.AgruparPorCategoria(_datos);
        _porId = DataProcessor.IndexarPorId(_datos);

        ActualizarFuentesCheckedList();
        _datosBase = GetDatosBase();
        _datosVista = new List<DataItem>(_datosBase);

        ReconstruirInfoColumnas();
        DetectarNumericosYMoneda();
        RefrescarCombosGrafica();
        RefrescarComboboxes();

        _porCategoria = DataProcessor.AgruparPorCategoria(_datosBase);
        lstCategorias.Items.Clear();
        foreach (var cat in _porCategoria.Keys.OrderBy(k => k))
            lstCategorias.Items.Add(cat);

        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        await ActualizarTabEstadisticasAsync();
        ActualizarChart();

        bool hayDatos = _datosBase.Count > 0 || _datos.Count > 0;
        tsBtnExportCsv.Enabled = hayDatos;
        tsBtnExportJson.Enabled = hayDatos;
        tsBtnExportXml.Enabled = hayDatos;
        tsBtnExportTxt.Enabled = hayDatos;
        tsBtnExportarBD.Enabled = hayDatos;   // ⭐ nuevo
        menuExportar.Enabled = hayDatos;
        menuExportarBD.Enabled = hayDatos;     // ⭐ nuevo

        lblTotalRegistros.Text = $"Total registros: {_datos.Count}";
        lblTotalCategorias.Text = $"Categorías: {_porCategoria.Count}";
        lblTotalFuentes.Text = $"Fuentes: {_datos.Select(d => d.Fuente).Distinct().Count()}";
    }

    private List<DataItem> GetDatosBase()
    {
        if (clbFuentes.Items.Count == 0) return new List<DataItem>(_datos);
        var sel = clbFuentes.CheckedItems.Cast<string>()
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return sel.Count == 0 ? new List<DataItem>(_datos)
                              : _datos.Where(d => sel.Contains(d.Fuente)).ToList();
    }

    private void ActualizarFuentesCheckedList()
    {
        var prevSel = clbFuentes.CheckedItems.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var prevExist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < clbFuentes.Items.Count; i++)
            prevExist.Add(clbFuentes.Items[i]?.ToString() ?? "");

        clbFuentes.ItemCheck -= ClbFuentes_ItemCheck!;
        clbFuentes.Items.Clear();
        foreach (var f in _datos.Select(d => d.Fuente).Distinct().OrderBy(f => f))
        {
            bool esNueva = !prevExist.Contains(f);
            clbFuentes.Items.Add(f, esNueva || prevSel.Count == 0 || prevSel.Contains(f));
        }
        clbFuentes.ItemCheck += ClbFuentes_ItemCheck!;
    }

    private async void ClbFuentes_ItemCheck(object sender, ItemCheckEventArgs e)
    {
        BeginInvoke(async () =>
        {
            _datosBase = GetDatosBase();
            _datosVista = new List<DataItem>(_datosBase);
            _porCategoria = DataProcessor.AgruparPorCategoria(_datosBase);
            lstCategorias.Items.Clear();
            foreach (var cat in _porCategoria.Keys.OrderBy(k => k))
                lstCategorias.Items.Add(cat);
            await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
            await ActualizarTabEstadisticasAsync();
            ActualizarChart();

            bool hayDatos = _datosBase.Count > 0;
            tsBtnExportCsv.Enabled = hayDatos;
            tsBtnExportJson.Enabled = hayDatos;
            tsBtnExportXml.Enabled = hayDatos;
            tsBtnExportTxt.Enabled = hayDatos;
            tsBtnExportarBD.Enabled = hayDatos;
            menuExportar.Enabled = hayDatos;
            menuExportarBD.Enabled = hayDatos;

            ActualizarEstadoBarra($"Mostrando {_datosVista.Count} registros de: " +
                string.Join(", ", clbFuentes.CheckedItems.Cast<string>()));
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  BIND GRID
    // ══════════════════════════════════════════════════════════════

    private async Task BindGridAsync(DataGridView dgv, List<DataItem> items,
        Label? contadorLabel, bool usarColsDefault = false)
    {
        contadorLabel?.Invoke(() => contadorLabel.Text = "⏳ Cargando...");
        bool limitado = items.Count > DISPLAY_LIMIT;
        var itemsDisplay = limitado ? items.Take(DISPLAY_LIMIT).ToList() : items;
        var colInfos = usarColsDefault
            ? new List<(string, string)>(_colsDefault)
            : new List<(string, string)>(_infoColumnas);

        var numSnap = new HashSet<string>(_numericDisplays, StringComparer.OrdinalIgnoreCase);
        var curSnap = new HashSet<string>(_currencyDisplays, StringComparer.OrdinalIgnoreCase);

        var dt = await Task.Run(() => BuildDataTable(itemsDisplay, colInfos, numSnap, curSnap));

        if (dgv.InvokeRequired)
            dgv.Invoke(() => AplicarDataTable(dgv, dt, items.Count, limitado, contadorLabel, colInfos));
        else
            AplicarDataTable(dgv, dt, items.Count, limitado, contadorLabel, colInfos);
    }

    private void AplicarDataTable(DataGridView dgv, DataTable dt, int totalReal, bool limitado,
        Label? contadorLabel, List<(string Display, string Clave)> colInfos)
    {
        dgv.DataSource = null; dgv.Columns.Clear(); dgv.AutoGenerateColumns = false;
        var cm = colInfos.ToDictionary(c => c.Display, c => c.Clave, StringComparer.OrdinalIgnoreCase);
        string? nombreDisplay = colInfos.FirstOrDefault(c => c.Clave == "nombre").Display;

        foreach (DataColumn col in dt.Columns)
        {
            string clave = cm.TryGetValue(col.ColumnName, out var cv) ? cv : col.ColumnName.ToLower();
            var dgvCol = new DataGridViewTextBoxColumn
            {
                Name = col.ColumnName,
                HeaderText = col.ColumnName,
                DataPropertyName = col.ColumnName,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.Automatic,
                MinimumWidth = 60,
            };
            dgvCol.Width = clave switch
            { "id" => 60, "nombre" => 200, "categoria" => 130, "valor" => 100, "fecha" => 105, "fuente" => 90, _ => 120 };

            if (!string.IsNullOrEmpty(nombreDisplay) && col.ColumnName == nombreDisplay)
            { dgvCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; dgvCol.MinimumWidth = 150; }

            bool esNumerico = clave is "id" or "valor" || _numericDisplays.Contains(col.ColumnName);
            if (esNumerico)
            {
                dgvCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                dgvCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            }

            dgv.Columns.Add(dgvCol);
        }

        AplicarEstiloGrid(dgv);
        dgv.DataSource = dt;
        dgv.CellFormatting -= DgvCellFormatting!;
        dgv.CellFormatting += DgvCellFormatting!;

        if (contadorLabel != null)
            contadorLabel.Text = limitado
                ? $"⚠ Mostrando {DISPLAY_LIMIT:N0} de {totalReal:N0}"
                : $"{totalReal:N0} registros";
    }

    private static DataTable BuildDataTable(
        List<DataItem> items,
        List<(string Display, string Clave)> colInfos,
        HashSet<string> numericDisplays,
        HashSet<string> currencyDisplays)
    {
        var dt = new DataTable();
        foreach (var (display, clave) in colInfos)
        {
            var tipo = clave switch { "id" => typeof(int), "valor" => typeof(double), _ => typeof(string) };
            if (!dt.Columns.Contains(display)) dt.Columns.Add(display, tipo);
        }
        dt.BeginLoadData();
        foreach (var item in items)
        {
            var row = dt.NewRow();
            foreach (var (display, clave) in colInfos)
            {
                if (!dt.Columns.Contains(display)) continue;
                switch (clave)
                {
                    case "id": row[display] = (object)item.Id; break;
                    case "nombre": row[display] = item.Nombre; break;
                    case "categoria": row[display] = item.Categoria; break;
                    case "valor": row[display] = (object)item.Valor; break;
                    case "fecha": row[display] = item.Fecha.ToString("yyyy-MM-dd"); break;
                    case "fuente": row[display] = item.Fuente; break;
                    default:
                        string raw = BuscarExtra(item, clave);
                        if (!string.IsNullOrEmpty(raw) && currencyDisplays.Contains(display)
                            && double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double cv))
                            row[display] = "$" + cv.ToString("N2");
                        else
                            row[display] = raw;
                        break;
                }
            }
            dt.Rows.Add(row);
        }
        dt.EndLoadData();
        return dt;
    }

    private void DgvCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var dgv = (DataGridView)sender;

        if (dgv.Columns.Contains("Fuente"))
        {
            try
            {
                var bgColor = dgv.Rows[e.RowIndex].Cells["Fuente"].Value?.ToString() switch
                {
                    "json" => Color.FromArgb(18, 50, 18),
                    "csv" => Color.FromArgb(50, 44, 8),
                    "xml" => Color.FromArgb(8, 34, 60),
                    "txt" => Color.FromArgb(44, 16, 52),
                    "postgresql" => Color.FromArgb(8, 24, 70),
                    "mariadb" => Color.FromArgb(54, 24, 8),
                    "LINQ GroupBy" => Color.FromArgb(20, 40, 20),
                    _ => Color.FromArgb(32, 32, 48)
                };
                e.CellStyle.BackColor = bgColor;
                e.CellStyle.ForeColor = Color.FromArgb(230, 230, 240);
                e.CellStyle.SelectionBackColor = Color.FromArgb(0, 100, 180);
                e.CellStyle.SelectionForeColor = Color.White;
            }
            catch { }
        }

        if (e.ColumnIndex >= 0 && e.ColumnIndex < dgv.Columns.Count && e.Value is double dv)
        {
            string colHeader = dgv.Columns[e.ColumnIndex].HeaderText;
            if (_currencyDisplays.Contains(colHeader))
            {
                e.Value = "$" + dv.ToString("N2");
                e.FormattingApplied = true;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  ESTILOS
    // ══════════════════════════════════════════════════════════════

    private void ConfigurarDataGridViews()
    {
        foreach (var dgv in new[] { dgvTodos, dgvCategoria, dgvProcesamiento })
        { dgv.AutoGenerateColumns = false; AplicarEstiloGrid(dgv); }

        dgvEstadisticas.Columns.Clear();
        foreach (var (h, w, a) in new (string, int, DataGridViewAutoSizeColumnMode)[]
        {
            ("Categoría", 0,   DataGridViewAutoSizeColumnMode.Fill),
            ("Cant.",      65,  DataGridViewAutoSizeColumnMode.None),
            ("Promedio",   100, DataGridViewAutoSizeColumnMode.None),
            ("Máximo",     100, DataGridViewAutoSizeColumnMode.None),
            ("Mínimo",     100, DataGridViewAutoSizeColumnMode.None),
            ("Total",      120, DataGridViewAutoSizeColumnMode.None),
        })
        {
            var col = new DataGridViewTextBoxColumn
            { HeaderText = h, ReadOnly = true, AutoSizeMode = a, MinimumWidth = 55 };
            if (a == DataGridViewAutoSizeColumnMode.None) col.Width = w;
            dgvEstadisticas.Columns.Add(col);
        }
        AplicarEstiloGrid(dgvEstadisticas);

        tsBtnExportCsv.Enabled = false;
        tsBtnExportJson.Enabled = false;
        tsBtnExportXml.Enabled = false;
        tsBtnExportTxt.Enabled = false;
        tsBtnExportarBD.Enabled = false;   // ⭐ nuevo
        menuExportar.Enabled = false;
        menuExportarBD.Enabled = false;    // ⭐ nuevo
    }

    private static void AplicarEstiloGrid(DataGridView dgv)
    {
        dgv.BackgroundColor = Color.FromArgb(18, 18, 28);
        dgv.GridColor = Color.FromArgb(55, 55, 75);
        dgv.BorderStyle = BorderStyle.None;
        dgv.DefaultCellStyle.BackColor = Color.FromArgb(28, 28, 42);
        dgv.DefaultCellStyle.ForeColor = Color.FromArgb(230, 230, 240);
        dgv.DefaultCellStyle.Font = new Font("Consolas", 9f);
        dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 100, 180);
        dgv.DefaultCellStyle.SelectionForeColor = Color.White;
        dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(33, 33, 50);
        dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(42, 42, 60);
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(0, 200, 220);
        dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        dgv.ColumnHeadersHeight = 32;
        dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        dgv.EnableHeadersVisualStyles = false;
        dgv.AllowUserToAddRows = false;
        dgv.AllowUserToResizeRows = false;
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.RowHeadersVisible = false;
        dgv.RowTemplate.Height = 24;
        dgv.ScrollBars = ScrollBars.Both;
        dgv.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
    }

    private static void AplicarDoubleBuffer(DataGridView dgv) =>
        typeof(DataGridView)
            .GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(dgv, true);

    private void ActualizarEstadoBarra(string mensaje)
    {
        if (lblStatus.GetCurrentParent()?.InvokeRequired == true)
            lblStatus.GetCurrentParent().Invoke(() => lblStatus.Text = mensaje);
        else
            lblStatus.Text = mensaje;
        Application.DoEvents();
    }

    // ══════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════

    private static string BuscarExtra(DataItem item, string clave)
    {
        if (item.CamposExtra.TryGetValue(clave, out var v)) return v;
        foreach (var kv in item.CamposExtra)
            if (string.Equals(kv.Key, clave, StringComparison.OrdinalIgnoreCase)) return kv.Value;
        return "";
    }

    private void MenuLimpiarDatos_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show("¿Limpiar todos los datos en memoria?", "Confirmar",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        _datos.Clear(); _porCategoria.Clear(); _porId.Clear();
        _datosBase.Clear(); _datosVista.Clear();
        _lastPgConnector = null; _lastMdConnector = null; _ultimoTipoCargado = "";
        _numericDisplays.Clear(); _currencyDisplays.Clear();

        foreach (var dgv in new[] { dgvTodos, dgvCategoria, dgvProcesamiento })
        { dgv.DataSource = null; dgv.Columns.Clear(); }

        dgvEstadisticas.Rows.Clear();
        lstCategorias.Items.Clear();
        clbFuentes.Items.Clear();
        cmbGrupoGrafica?.Items.Clear();
        cmbMetricaGrafica?.Items.Clear();
        txtLinqFiltro.Text = "";
        lblProcInfo.Text = "Selecciona una operación.";
        btnEliminarDuplicados.Enabled = false;
        chartMain.Limpiar();
        lblContadorTodos.Text = "0 registros";

        tsBtnExportCsv.Enabled = false;
        tsBtnExportJson.Enabled = false;
        tsBtnExportXml.Enabled = false;
        tsBtnExportTxt.Enabled = false;
        tsBtnExportarBD.Enabled = false;
        menuExportar.Enabled = false;
        menuExportarBD.Enabled = false;

        _infoColumnas.Clear();
        foreach (var col in _colsDefault) _infoColumnas.Add(col);
        RefrescarComboboxes();
        ActualizarEstadoBarra("Datos limpiados.");
    }

    private void MenuAcercaDe_Click(object sender, EventArgs e) =>
        MessageBox.Show(
            "Data Fusion Arena\nAdministración y Organización de Datos\n\n" +
            "Ingeniería · 4.º Semestre · C# .NET 10 · WinForms\n\n" +
            "Fuentes: JSON · CSV · XML · TXT · PostgreSQL · MariaDB\n" +
            "Exportar: CSV · JSON · XML · TXT · PostgreSQL · MariaDB\n" +
            "Estructuras: List<T> · Dictionary<TKey,TValue>\n" +
            "Gráficas: GDI+ propio (sin dependencias externas)",
            "Acerca de", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void MenuSalir_Click(object sender, EventArgs e) => Close();
}

// ══════════════════════════════════════════════════════════════
//  DIÁLOGO – Conexión BD con detección automática de tablas ⭐
// ══════════════════════════════════════════════════════════════
public class FormConexionBD : Form
{
    public string CadenaConexion { get; private set; } = "";
    public string NombreTabla { get; private set; } = "";

    private readonly TextBox txtHost, txtPuerto, txtBD, txtUsuario, txtContrasena;
    private readonly ComboBox cmbTabla;
    private readonly Button btnDetectarTablas;
    private readonly Label lblEstadoTablas;
    private readonly bool _esPg;
    private readonly string _pd, _ud;

    public FormConexionBD(string motor)
    {
        _esPg = motor == "PostgreSQL";
        _pd = _esPg ? "5432" : "3306";
        _ud = _esPg ? "postgres" : "root";

        Text = $"Conexión a {motor}";
        Size = new Size(490, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(30, 30, 45);
        ForeColor = Color.White;

        int y = 18;
        const int lx = 15, cx = 135, lw = 115, cw = 325;

        Label Lbl(string t) => new()
        {
            Text = t,
            AutoSize = true,
            ForeColor = Color.FromArgb(0, 200, 220)
        };
        TextBox Txt(string d, bool p = false) => new()
        {
            Width = cw,
            Text = d,
            BackColor = Color.FromArgb(45, 45, 65),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = p
        };

        var l1 = Lbl("Host:"); l1.Location = new Point(lx, y + 3);
        txtHost = Txt("localhost"); txtHost.Location = new Point(cx, y); y += 35;

        var l2 = Lbl("Puerto:"); l2.Location = new Point(lx, y + 3);
        txtPuerto = Txt(_pd); txtPuerto.Location = new Point(cx, y); y += 35;

        var l3 = Lbl("Base de datos:"); l3.Location = new Point(lx, y + 3);
        txtBD = Txt(""); txtBD.Location = new Point(cx, y); y += 35;

        var l4 = Lbl("Usuario:"); l4.Location = new Point(lx, y + 3);
        txtUsuario = Txt(_ud); txtUsuario.Location = new Point(cx, y); y += 35;

        var l5 = Lbl("Contraseña:"); l5.Location = new Point(lx, y + 3);
        txtContrasena = Txt("", true); txtContrasena.Location = new Point(cx, y); y += 35;

        // ── Separador ──────────────────────────────────────────
        var sep = new Label
        {
            Text = "",
            Location = new Point(lx, y),
            Size = new Size(445, 2),
            BackColor = Color.FromArgb(0, 160, 180)
        };
        y += 10;

        // ── Detectar tablas ────────────────────────────────────
        var lTabla = Lbl("Tabla:"); lTabla.Location = new Point(lx, y + 6);

        cmbTabla = new ComboBox
        {
            Location = new Point(cx, y + 2),
            Width = 210,
            DropDownStyle = ComboBoxStyle.DropDown,
            BackColor = Color.FromArgb(45, 45, 65),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        btnDetectarTablas = new Button
        {
            Text = "🔍 Detectar tablas",
            Location = new Point(cx + 218, y),
            Width = 107,
            Height = 28,
            BackColor = Color.FromArgb(0, 90, 150),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f)
        };
        btnDetectarTablas.FlatAppearance.BorderSize = 0;
        btnDetectarTablas.Click += BtnDetectarTablas_Click!;
        y += 38;

        lblEstadoTablas = new Label
        {
            Text = "Ingresa los datos de conexión y pulsa 'Detectar tablas'.",
            Location = new Point(cx, y),
            Size = new Size(cw, 18),
            ForeColor = Color.FromArgb(120, 180, 120),
            Font = new Font("Segoe UI", 7.8f)
        };
        y += 30;

        // ── Botones ───────────────────────────────────────────
        var ok = new Button
        {
            Text = "✔ Conectar",
            Location = new Point(270, y),
            Width = 100,
            Height = 28,
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        ok.FlatAppearance.BorderSize = 0;

        var can = new Button
        {
            Text = "Cancelar",
            Location = new Point(378, y),
            Width = 87,
            Height = 28,
            DialogResult = DialogResult.Cancel,
            BackColor = Color.FromArgb(80, 30, 30),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        can.FlatAppearance.BorderSize = 0;

        ok.Click += (_, _) =>
        {
            string h = string.IsNullOrWhiteSpace(txtHost.Text) ? "localhost" : txtHost.Text.Trim();
            string p = string.IsNullOrWhiteSpace(txtPuerto.Text) ? _pd : txtPuerto.Text.Trim();
            string u = string.IsNullOrWhiteSpace(txtUsuario.Text) ? _ud : txtUsuario.Text.Trim();
            CadenaConexion = _esPg
                ? $"Host={h};Port={p};Database={txtBD.Text.Trim()};Username={u};Password={txtContrasena.Text};"
                : $"Server={h};Port={p};Database={txtBD.Text.Trim()};User={u};Password={txtContrasena.Text};";
            NombreTabla = cmbTabla.Text.Trim();
            if (string.IsNullOrWhiteSpace(NombreTabla))
            {
                MessageBox.Show("Debes especificar el nombre de la tabla.",
                    "Tabla requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        ClientSize = new Size(475, y + 55);
        Controls.AddRange(new Control[]
        {
            l1, txtHost, l2, txtPuerto, l3, txtBD,
            l4, txtUsuario, l5, txtContrasena,
            sep, lTabla, cmbTabla, btnDetectarTablas, lblEstadoTablas,
            ok, can
        });
        AcceptButton = ok;
        CancelButton = can;
    }

    private async void BtnDetectarTablas_Click(object sender, EventArgs e)
    {
        string h = string.IsNullOrWhiteSpace(txtHost.Text) ? "localhost" : txtHost.Text.Trim();
        string p = string.IsNullOrWhiteSpace(txtPuerto.Text) ? _pd : txtPuerto.Text.Trim();
        string u = string.IsNullOrWhiteSpace(txtUsuario.Text) ? _ud : txtUsuario.Text.Trim();
        string db = txtBD.Text.Trim();

        if (string.IsNullOrWhiteSpace(db))
        {
            MessageBox.Show("Escribe el nombre de la base de datos primero.",
                "Base de datos requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string cadena = _esPg
            ? $"Host={h};Port={p};Database={db};Username={u};Password={txtContrasena.Text};"
            : $"Server={h};Port={p};Database={db};User={u};Password={txtContrasena.Text};";

        btnDetectarTablas.Enabled = false;
        lblEstadoTablas.Text = "⏳ Conectando y detectando tablas...";
        lblEstadoTablas.ForeColor = Color.FromArgb(255, 200, 50);

        try
        {
            List<string> tablas = await Task.Run(() =>
                _esPg
                    ? DatabaseWriter.ObtenerTablasPostgreSQL(cadena)
                    : DatabaseWriter.ObtenerTablasMariaDB(cadena));

            string tablaActual = cmbTabla.Text;
            cmbTabla.Items.Clear();
            foreach (var t in tablas) cmbTabla.Items.Add(t);

            if (tablas.Count > 0)
            {
                // Restaurar selección previa o seleccionar la primera
                int idx = cmbTabla.FindStringExact(tablaActual);
                cmbTabla.SelectedIndex = idx >= 0 ? idx : 0;
                lblEstadoTablas.Text = $"✅ {tablas.Count} tabla(s) encontrada(s).";
                lblEstadoTablas.ForeColor = Color.FromArgb(0, 220, 100);
            }
            else
            {
                lblEstadoTablas.Text = "⚠ No se encontraron tablas. Escribe el nombre manualmente.";
                lblEstadoTablas.ForeColor = Color.FromArgb(255, 160, 50);
            }
        }
        catch (Exception ex)
        {
            lblEstadoTablas.Text = $"❌ Error: {ex.Message}";
            lblEstadoTablas.ForeColor = Color.FromArgb(255, 80, 80);
        }
        finally
        {
            btnDetectarTablas.Enabled = true;
        }
    }
}

// ══════════════════════════════════════════════════════════════
//  DIÁLOGO – Exportar datos a BD ⭐ NUEVO
// ══════════════════════════════════════════════════════════════
public class FormExportarBD : Form
{
    public string CadenaConexion { get; private set; } = "";
    public string NombreTabla { get; private set; } = "";
    public string Motor { get; private set; } = "PostgreSQL";

    private readonly RadioButton rbPostgres, rbMariaDB;
    private readonly TextBox txtHost, txtPuerto, txtBD, txtUsuario, txtContrasena;
    private readonly ComboBox cmbTabla;
    private readonly Button btnDetectarTablas;
    private readonly Label lblEstadoTablas, lblInfo;
    private readonly int _totalRegistros;

    public FormExportarBD(int totalRegistros)
    {
        _totalRegistros = totalRegistros;
        Text = "Exportar datos a Base de Datos";
        Size = new Size(510, 490);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(25, 25, 40);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        int y = 12;
        const int lx = 15, cx = 140, cw = 340;

        // ── Info ──────────────────────────────────────────────
        lblInfo = new Label
        {
            Text = $"Se enviarán {totalRegistros:N0} registros a la base de datos seleccionada.\n" +
                   "Si la tabla no existe, se creará automáticamente.",
            Location = new Point(lx, y),
            Size = new Size(460, 36),
            ForeColor = Color.FromArgb(0, 200, 220),
            Font = new Font("Segoe UI", 9f)
        };
        y += 48;

        // ── Separador ─────────────────────────────────────────
        var sep0 = new Label { Location = new Point(lx, y), Size = new Size(455, 2), BackColor = Color.FromArgb(0, 160, 180) };
        y += 10;

        // ── Motor ─────────────────────────────────────────────
        var lblMotor = new Label
        {
            Text = "Motor:",
            Location = new Point(lx, y + 3),
            AutoSize = true,
            ForeColor = Color.FromArgb(0, 200, 220)
        };
        rbPostgres = new RadioButton
        {
            Text = "🐘 PostgreSQL",
            Location = new Point(cx, y),
            AutoSize = true,
            Checked = true,
            ForeColor = Color.FromArgb(100, 180, 255),
            BackColor = Color.Transparent
        };
        rbMariaDB = new RadioButton
        {
            Text = "🐬 MariaDB",
            Location = new Point(cx + 145, y),
            AutoSize = true,
            ForeColor = Color.FromArgb(255, 180, 100),
            BackColor = Color.Transparent
        };
        rbPostgres.CheckedChanged += Motor_Changed!;
        rbMariaDB.CheckedChanged += Motor_Changed!;
        y += 30;

        var sep1 = new Label { Location = new Point(lx, y), Size = new Size(455, 1), BackColor = Color.FromArgb(55, 55, 75) };
        y += 10;

        // ── Campos conexión ───────────────────────────────────
        Label Lbl(string t) => new()
        { Text = t, AutoSize = true, ForeColor = Color.FromArgb(180, 180, 200) };

        TextBox Txt(string d, bool p = false) => new()
        {
            Width = cw,
            Text = d,
            BackColor = Color.FromArgb(40, 40, 60),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = p
        };

        var l1 = Lbl("Host:"); l1.Location = new Point(lx, y + 3);
        txtHost = Txt("localhost"); txtHost.Location = new Point(cx, y); y += 32;

        var l2 = Lbl("Puerto:"); l2.Location = new Point(lx, y + 3);
        txtPuerto = Txt("5432"); txtPuerto.Location = new Point(cx, y); y += 32;

        var l3 = Lbl("Base de datos:"); l3.Location = new Point(lx, y + 3);
        txtBD = Txt(""); txtBD.Location = new Point(cx, y); y += 32;

        var l4 = Lbl("Usuario:"); l4.Location = new Point(lx, y + 3);
        txtUsuario = Txt("postgres"); txtUsuario.Location = new Point(cx, y); y += 32;

        var l5 = Lbl("Contraseña:"); l5.Location = new Point(lx, y + 3);
        txtContrasena = Txt("", true); txtContrasena.Location = new Point(cx, y); y += 32;

        // ── Tabla con detección automática ────────────────────
        var sep2 = new Label { Location = new Point(lx, y), Size = new Size(455, 2), BackColor = Color.FromArgb(0, 160, 180) };
        y += 10;

        var lTabla = Lbl("Tabla destino:"); lTabla.Location = new Point(lx, y + 5);

        cmbTabla = new ComboBox
        {
            Location = new Point(cx, y + 1),
            Width = 215,
            DropDownStyle = ComboBoxStyle.DropDown,
            BackColor = Color.FromArgb(40, 40, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        // Nombre por defecto sugerido
        cmbTabla.Text = "datos_exportados";

        btnDetectarTablas = new Button
        {
            Text = "🔍 Ver tablas",
            Location = new Point(cx + 223, y),
            Width = 117,
            Height = 28,
            BackColor = Color.FromArgb(0, 90, 150),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f)
        };
        btnDetectarTablas.FlatAppearance.BorderSize = 0;
        btnDetectarTablas.Click += BtnDetectarTablas_Click!;
        y += 36;

        lblEstadoTablas = new Label
        {
            Text = "Ingresa conexión y pulsa 'Ver tablas' para elegir una existente, o escribe un nombre nuevo.",
            Location = new Point(cx, y),
            Size = new Size(cw, 30),
            ForeColor = Color.FromArgb(120, 180, 120),
            Font = new Font("Segoe UI", 7.8f)
        };
        y += 36;

        // ── Botones finales ───────────────────────────────────
        var ok = new Button
        {
            Text = $"📤 Enviar {totalRegistros:N0} registros",
            Location = new Point(cx, y),
            Width = 200,
            Height = 30,
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(0, 120, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        ok.FlatAppearance.BorderSize = 0;

        var can = new Button
        {
            Text = "Cancelar",
            Location = new Point(cx + 210, y),
            Width = 90,
            Height = 30,
            DialogResult = DialogResult.Cancel,
            BackColor = Color.FromArgb(80, 30, 30),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        can.FlatAppearance.BorderSize = 0;

        ok.Click += (_, _) =>
        {
            string h = string.IsNullOrWhiteSpace(txtHost.Text) ? "localhost" : txtHost.Text.Trim();
            string p = string.IsNullOrWhiteSpace(txtPuerto.Text) ? (rbPostgres.Checked ? "5432" : "3306") : txtPuerto.Text.Trim();
            string u = string.IsNullOrWhiteSpace(txtUsuario.Text) ? (rbPostgres.Checked ? "postgres" : "root") : txtUsuario.Text.Trim();
            string db = txtBD.Text.Trim();

            if (string.IsNullOrWhiteSpace(db))
            {
                MessageBox.Show("Escribe el nombre de la base de datos.",
                    "Datos incompletos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }

            NombreTabla = cmbTabla.Text.Trim();
            if (string.IsNullOrWhiteSpace(NombreTabla))
            {
                MessageBox.Show("Escribe o selecciona el nombre de la tabla destino.",
                    "Tabla requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }

            Motor = rbPostgres.Checked ? "PostgreSQL" : "MariaDB";
            CadenaConexion = rbPostgres.Checked
                ? $"Host={h};Port={p};Database={db};Username={u};Password={txtContrasena.Text};"
                : $"Server={h};Port={p};Database={db};User={u};Password={txtContrasena.Text};";
        };

        ClientSize = new Size(478, y + 55);
        Controls.AddRange(new Control[]
        {
            lblInfo, sep0,
            lblMotor, rbPostgres, rbMariaDB, sep1,
            l1, txtHost, l2, txtPuerto, l3, txtBD,
            l4, txtUsuario, l5, txtContrasena,
            sep2, lTabla, cmbTabla, btnDetectarTablas, lblEstadoTablas,
            ok, can
        });
        AcceptButton = ok;
        CancelButton = can;
    }

    private void Motor_Changed(object sender, EventArgs e)
    {
        if (rbPostgres.Checked)
        {
            txtPuerto.Text = "5432";
            txtUsuario.Text = "postgres";
        }
        else
        {
            txtPuerto.Text = "3306";
            txtUsuario.Text = "root";
        }
        // Limpiar lista de tablas al cambiar motor
        cmbTabla.Items.Clear();
        lblEstadoTablas.Text = "Motor cambiado. Vuelve a detectar tablas si lo necesitas.";
        lblEstadoTablas.ForeColor = Color.FromArgb(180, 180, 180);
    }

    private async void BtnDetectarTablas_Click(object sender, EventArgs e)
    {
        string h = string.IsNullOrWhiteSpace(txtHost.Text) ? "localhost" : txtHost.Text.Trim();
        string p = string.IsNullOrWhiteSpace(txtPuerto.Text) ? (rbPostgres.Checked ? "5432" : "3306") : txtPuerto.Text.Trim();
        string u = string.IsNullOrWhiteSpace(txtUsuario.Text) ? (rbPostgres.Checked ? "postgres" : "root") : txtUsuario.Text.Trim();
        string db = txtBD.Text.Trim();

        if (string.IsNullOrWhiteSpace(db))
        {
            MessageBox.Show("Escribe el nombre de la base de datos primero.",
                "Base de datos requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string cadena = rbPostgres.Checked
            ? $"Host={h};Port={p};Database={db};Username={u};Password={txtContrasena.Text};"
            : $"Server={h};Port={p};Database={db};User={u};Password={txtContrasena.Text};";

        btnDetectarTablas.Enabled = false;
        lblEstadoTablas.Text = "⏳ Detectando tablas...";
        lblEstadoTablas.ForeColor = Color.FromArgb(255, 200, 50);

        try
        {
            string tablaActual = cmbTabla.Text;
            List<string> tablas = await Task.Run(() =>
                rbPostgres.Checked
                    ? DatabaseWriter.ObtenerTablasPostgreSQL(cadena)
                    : DatabaseWriter.ObtenerTablasMariaDB(cadena));

            cmbTabla.Items.Clear();
            foreach (var t in tablas) cmbTabla.Items.Add(t);

            if (tablas.Count > 0)
            {
                int idx = cmbTabla.FindStringExact(tablaActual);
                if (idx >= 0) cmbTabla.SelectedIndex = idx;
                else if (!string.IsNullOrWhiteSpace(tablaActual)) cmbTabla.Text = tablaActual;
                lblEstadoTablas.Text = $"✅ {tablas.Count} tabla(s) encontrada(s). Puedes elegir una o escribir un nombre nuevo.";
                lblEstadoTablas.ForeColor = Color.FromArgb(0, 220, 100);
            }
            else
            {
                lblEstadoTablas.Text = "⚠ Sin tablas existentes. Se creará la tabla que escribas.";
                lblEstadoTablas.ForeColor = Color.FromArgb(255, 160, 50);
            }
        }
        catch (Exception ex)
        {
            lblEstadoTablas.Text = $"❌ {ex.Message}";
            lblEstadoTablas.ForeColor = Color.FromArgb(255, 80, 80);
        }
        finally
        {
            btnDetectarTablas.Enabled = true;
        }
    }
}

// ══════════════════════════════════════════════════════════════
//  DIÁLOGO – Selección de columnas (sin cambios)
// ══════════════════════════════════════════════════════════════
public class FormSeleccionColumnas : Form
{
    public string ColCategoria { get; private set; } = "";
    public string ColValor { get; private set; } = "";
    public string ColNombre { get; private set; } = "";
    public string ColFecha { get; private set; } = "";

    public FormSeleccionColumnas(List<string> columnas, Dictionary<string, string> mapeoActual)
    {
        Text = "Mapeo de columnas para análisis"; Size = new Size(480, 295);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        BackColor = Color.FromArgb(25, 25, 40); ForeColor = Color.White;

        string sC = mapeoActual.FirstOrDefault(kv => kv.Value == "categoria").Key ?? "";
        string sV = mapeoActual.FirstOrDefault(kv => kv.Value == "valor").Key ?? "";
        string sN = mapeoActual.FirstOrDefault(kv => kv.Value == "nombre").Key ?? "";
        string sF = mapeoActual.FirstOrDefault(kv => kv.Value == "fecha").Key ?? "";

        var todas = new List<string> { "(ninguna)" }; todas.AddRange(columnas);
        var arr = todas.ToArray<object>();
        int y = 15, lx = 15, cx = 215, lw = 195, cw = 230;

        var intro = new Label { Text = "Elige qué columna de tu tabla representa cada concepto:", Location = new Point(lx, y), Size = new Size(440, 20), ForeColor = Color.FromArgb(160, 200, 220), Font = new Font("Segoe UI", 8.5f) };
        y += 30;

        Label Lbl(string t, string s) => new() { Text = s.Length > 0 ? $"{t}  (auto: {s})" : t, Location = new Point(lx, y), Size = new Size(lw, 20), ForeColor = Color.FromArgb(0, 200, 220), Font = new Font("Segoe UI", 8.5f) };
        ComboBox Cmb(string s)
        {
            var c = new ComboBox { Location = new Point(cx, y - 2), Width = cw, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(40, 40, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            c.Items.AddRange(arr);
            int idx = s.Length > 0 ? todas.IndexOf(s) : 0;
            c.SelectedIndex = Math.Max(0, idx);
            return c;
        }

        var lC = Lbl("📊 Categoría (eje X / agrupación):", sC); var cC = Cmb(sC); y += 30;
        var lV = Lbl("🔢 Valor numérico (eje Y / suma):", sV); var cV = Cmb(sV); y += 30;
        var lN = Lbl("🏷  Nombre / etiqueta:", sN); var cN = Cmb(sN); y += 30;
        var lF = Lbl("📅 Fecha:", sF); var cF = Cmb(sF); y += 38;

        var nota = new Label { Text = "ℹ  Deja en (ninguna) si la columna no aplica.", Location = new Point(lx, y), Size = new Size(440, 18), ForeColor = Color.FromArgb(120, 120, 150), Font = new Font("Segoe UI", 8f) };
        y += 28;

        var btnOk = new Button { Text = "✔ Confirmar", Location = new Point(248, y), Width = 105, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(0, 120, 212), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var btnCan = new Button { Text = "Cancelar", Location = new Point(362, y), Width = 85, DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(80, 30, 30), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };

        btnOk.Click += (_, _) => {
            ColCategoria = cC.Text == "(ninguna)" ? "" : cC.Text;
            ColValor = cV.Text == "(ninguna)" ? "" : cV.Text;
            ColNombre = cN.Text == "(ninguna)" ? "" : cN.Text;
            ColFecha = cF.Text == "(ninguna)" ? "" : cF.Text;
            var usados = new[] { ColCategoria, ColValor, ColNombre, ColFecha }
                .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (usados.Count != usados.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                MessageBox.Show("No uses la misma columna para más de un campo.\nElige columnas distintas.",
                    "Mapeo inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        ClientSize = new Size(460, y + 50);
        Controls.AddRange(new Control[] { intro, lC, cC, lV, cV, lN, cN, lF, cF, nota, btnOk, btnCan });
        AcceptButton = btnOk; CancelButton = btnCan;
    }
}