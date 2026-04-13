using DataFusionArena.Shared.Models;
using DataFusionArena.Shared.Readers;
using DataFusionArena.Shared.Database;
using DataFusionArena.Shared.Processing;
using System.Data;
using System.Reflection;
using System.Windows.Forms.DataVisualization.Charting;

namespace DataFusionArena.WinForms;

public partial class MainForm : Form
{
    // ── Estado global ────────────────────────────────────────────
    private readonly List<DataItem> _datos = new();
    private List<DataItem> _datosBase  = new();   // filtrado por checkboxes de fuente
    private List<DataItem> _datosVista = new();   // filtrado por campo/valor

    private Dictionary<string, List<DataItem>> _porCategoria = new();
    private Dictionary<int, DataItem>           _porId        = new();

    // Conectores BD para refresh
    private PostgreSqlConnector? _lastPgConnector;
    private MariaDbConnector?    _lastMdConnector;

    // Límite de filas para mostrar en grilla (procesa todo, solo limita display)
    private const int DISPLAY_LIMIT = 75_000;

    private readonly string _dirDatos = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "SampleData");

    // ════════════════════════════════════════════════════════════
    //  CONSTRUCTOR
    // ════════════════════════════════════════════════════════════
    public MainForm()
    {
        InitializeComponent();
        ConfigurarDataGridViews();
        AplicarDoubleBuffer(dgvTodos);
        AplicarDoubleBuffer(dgvCategoria);
        AplicarDoubleBuffer(dgvProcesamiento);
        AplicarDoubleBuffer(dgvEstadisticas);

        // ── Configuración inicial del Chart ──────────────────────
        InicializarChart();

        ActualizarEstadoBarra("Listo. Cargue datos usando el menú o los botones.");
        Text = "Data Fusion Arena – Administración y Organización de Datos";

        Load += (s, e) =>
        {
            splitMain.SplitterDistance     = 170;
            splitCategoria.SplitterDistance = 220;
        };

        // Actualizar Chart al cambiar a la pestaña de gráficas
        tabControl1.SelectedIndexChanged += (s, e) =>
        {
            if (tabControl1.SelectedTab == tabGraficas)
                ActualizarChart();
        };
    }

    // ════════════════════════════════════════════════════════════
    //  CONFIGURACIÓN INICIAL DEL CHART
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Prepara el control Chart con tema oscuro, área de gráfica y leyenda.
    /// Se llama una sola vez en el constructor.
    /// </summary>
    private void InicializarChart()
    {
        chartMain.Series.Clear();
        chartMain.ChartAreas.Clear();
        chartMain.Legends.Clear();
        chartMain.Titles.Clear();

        // ── Fondo del control ────────────────────────────────────
        chartMain.BackColor        = Color.FromArgb(18, 18, 28);
        chartMain.BorderlineColor  = Color.FromArgb(42, 42, 60);

        // ── Área de gráfica ──────────────────────────────────────
        var area = new ChartArea("AreaPrincipal");

        // Fondo del área
        area.BackColor         = Color.FromArgb(28, 28, 42);
        area.BackSecondaryColor= Color.FromArgb(22, 22, 36);
        area.BackGradientStyle = GradientStyle.TopBottom;
        area.BorderColor       = Color.FromArgb(42, 42, 60);
        area.BorderWidth       = 1;

        // Eje X
        area.AxisX.LabelStyle.ForeColor   = Color.FromArgb(180, 180, 200);
        area.AxisX.LabelStyle.Font        = new Font("Segoe UI", 8f);
        area.AxisX.LabelStyle.Angle       = -35;
        area.AxisX.MajorGrid.LineColor    = Color.FromArgb(45, 45, 65);
        area.AxisX.LineColor              = Color.FromArgb(60, 60, 80);
        area.AxisX.TitleFont              = new Font("Segoe UI", 9f, FontStyle.Bold);
        area.AxisX.TitleForeColor         = Color.FromArgb(0, 200, 220);

        // Eje Y
        area.AxisY.LabelStyle.ForeColor   = Color.FromArgb(180, 180, 200);
        area.AxisY.LabelStyle.Font        = new Font("Segoe UI", 8f);
        area.AxisY.MajorGrid.LineColor    = Color.FromArgb(45, 45, 65);
        area.AxisY.LineColor              = Color.FromArgb(60, 60, 80);
        area.AxisY.TitleFont              = new Font("Segoe UI", 9f, FontStyle.Bold);
        area.AxisY.TitleForeColor         = Color.FromArgb(0, 200, 220);

        chartMain.ChartAreas.Add(area);

        // ── Leyenda ──────────────────────────────────────────────
        var leyenda = new Legend("Leyenda")
        {
            BackColor  = Color.FromArgb(28, 28, 42),
            ForeColor  = Color.FromArgb(200, 200, 220),
            Font       = new Font("Segoe UI", 8.5f),
            BorderColor= Color.FromArgb(42, 42, 60),
            IsDockedInsideChartArea = false,
            Docking    = Docking.Right
        };
        chartMain.Legends.Add(leyenda);

        // ── Título por defecto ───────────────────────────────────
        var titulo = new Title("Carga datos para ver la gráfica")
        {
            ForeColor = Color.FromArgb(120, 120, 150),
            Font      = new Font("Segoe UI", 13f, FontStyle.Italic),
            Docking   = Docking.Top
        };
        chartMain.Titles.Add(titulo);
    }

    // ════════════════════════════════════════════════════════════
    //  ACTUALIZAR CHART (llamado desde eventos)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Reconstruye las series del Chart según el tipo seleccionado y los datos actuales.
    /// </summary>
    private void ActualizarChart()
    {
        var fuente = _datosBase.Count > 0 ? _datosBase : _datos;

        chartMain.Series.Clear();
        chartMain.Titles.Clear();
        chartMain.ChartAreas[0].AxisX.IsLogarithmic = false;

        if (fuente.Count == 0)
        {
            chartMain.Titles.Add(new Title("Sin datos – carga archivos primero")
            {
                ForeColor = Color.FromArgb(120, 120, 150),
                Font      = new Font("Segoe UI", 13f, FontStyle.Italic),
                Docking   = Docking.Top
            });
            return;
        }

        // Calcular estadísticas agrupadas por categoría
        var stats = DataProcessor
            .CalcularEstadisticas(fuente)
            .Values
            .OrderByDescending(s => s.SumaValores)
            .Take(12)
            .ToList();

        string tipo = cmbTipoGrafica.Text;

        switch (tipo)
        {
            case "Columnas": ConfigurarChartColumnas(stats); break;
            case "Barras":   ConfigurarChartBarras(stats);   break;
            case "Pastel":   ConfigurarChartPastel(stats);   break;
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Gráfica de Columnas (vertical)
    // ────────────────────────────────────────────────────────────
    private void ConfigurarChartColumnas(List<EstadisticasCategoria> stats)
    {
        // Título
        chartMain.Titles.Add(new Title("Valor Total por Categoría – Gráfica de Columnas")
        {
            ForeColor = Color.FromArgb(0, 200, 220),
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
            Docking   = Docking.Top
        });

        // Configurar ejes para columnas verticales
        var area = chartMain.ChartAreas[0];
        area.AxisX.Title = "Categoría";
        area.AxisY.Title = "Valor Total";
        area.AxisX.LabelStyle.Angle = -38;
        area.AxisY.LabelStyle.Format = "N0";
        chartMain.Legends[0].Enabled = false;

        // Paleta de colores
        var colores = PaletaColores();

        // Una serie por categoría para poder colorear cada barra diferente
        for (int i = 0; i < stats.Count; i++)
        {
            var s   = stats[i];
            var ser = new Series(s.Categoria)
            {
                ChartType  = SeriesChartType.Column,
                ChartArea  = "AreaPrincipal",
                Color      = colores[i % colores.Length],
                BorderColor= Color.FromArgb(10, 10, 20),
                BorderWidth= 1,
                // Etiqueta encima de cada barra
                IsValueShownAsLabel = true,
                LabelForeColor      = Color.FromArgb(230, 230, 240),
                LabelFormat         = "N0",
                Font                = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                LegendText          = s.Categoria
            };

            // Esquinas redondeadas simuladas con CustomProperties
            ser["DrawingStyle"] = "Cylinder";
            ser.Points.AddXY(s.Categoria, s.SumaValores);
            ser.Points[0].ToolTip =
                $"{s.Categoria}\nTotal: {s.SumaValores:N2}\n" +
                $"Cant.: {s.Cantidad}\nProm.: {s.Promedio:N2}";

            chartMain.Series.Add(ser);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Gráfica de Barras (horizontal)
    // ────────────────────────────────────────────────────────────
    private void ConfigurarChartBarras(List<EstadisticasCategoria> stats)
    {
        chartMain.Titles.Add(new Title("Valor Total por Categoría – Gráfica de Barras")
        {
            ForeColor = Color.FromArgb(255, 200, 50),
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
            Docking   = Docking.Top
        });

        var area = chartMain.ChartAreas[0];
        area.AxisX.Title = "Categoría";
        area.AxisY.Title = "Valor Total";
        area.AxisX.LabelStyle.Angle = 0;   // horizontal → etiquetas rectas
        area.AxisY.LabelStyle.Format = "N0";
        chartMain.Legends[0].Enabled = false;

        var colores = PaletaColores();

        for (int i = 0; i < stats.Count; i++)
        {
            var s   = stats[i];
            var ser = new Series(s.Categoria)
            {
                ChartType           = SeriesChartType.Bar,   // ← Bar = horizontal
                ChartArea           = "AreaPrincipal",
                Color               = colores[i % colores.Length],
                BorderColor         = Color.FromArgb(10, 10, 20),
                BorderWidth         = 1,
                IsValueShownAsLabel = true,
                LabelForeColor      = Color.FromArgb(230, 230, 240),
                LabelFormat         = "N0",
                Font                = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            };
            ser["DrawingStyle"] = "Cylinder";
            ser.Points.AddXY(s.Categoria, s.SumaValores);
            ser.Points[0].ToolTip =
                $"{s.Categoria}\nTotal: {s.SumaValores:N2}\nCant.: {s.Cantidad}";
            chartMain.Series.Add(ser);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Gráfica de Pastel (Pie)
    // ────────────────────────────────────────────────────────────
    private void ConfigurarChartPastel(List<EstadisticasCategoria> stats)
    {
        chartMain.Titles.Add(new Title("Distribución por Categoría – Gráfica de Pastel")
        {
            ForeColor = Color.FromArgb(0, 224, 128),
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
            Docking   = Docking.Top
        });

        // El Pie no usa ejes
        chartMain.ChartAreas[0].AxisX.Title = "";
        chartMain.ChartAreas[0].AxisY.Title = "";
        chartMain.Legends[0].Enabled        = true;

        var ser = new Series("Distribución")
        {
            ChartType = SeriesChartType.Doughnut,   // Doughnut = pastel con hueco central
            ChartArea = "AreaPrincipal",
            Legend    = "Leyenda",
            // Mostrar porcentaje dentro de cada sector
            IsValueShownAsLabel = true,
            LabelFormat         = "P1",
            LabelForeColor      = Color.White,
            Font                = new Font("Segoe UI", 8f, FontStyle.Bold),
        };
        ser["DoughnutRadius"]       = "35";   // tamaño del hueco central (%)
        ser["PieLabelStyle"]        = "Outside";
        ser["PieLineColor"]         = "DimGray";
        ser["CollectedLabel"]       = "Otros";
        ser["CollectedColor"]       = "Gray";
        ser["CollectedThreshold"]   = "2";    // sectores < 2% se agrupan en "Otros"

        var colores = PaletaColores();
        double total = stats.Sum(s => s.SumaValores);

        for (int i = 0; i < stats.Count; i++)
        {
            var s = stats[i];
            int idx = ser.Points.AddXY(s.Categoria, s.SumaValores);
            ser.Points[idx].Color      = colores[i % colores.Length];
            ser.Points[idx].LegendText = $"{s.Categoria} ({s.SumaValores / total:P1})";
            ser.Points[idx].ToolTip    =
                $"{s.Categoria}\nTotal: {s.SumaValores:N2}\n" +
                $"Porcentaje: {s.SumaValores / total:P2}";
            // Explotar ligeramente el sector mayor
            if (i == 0) ser.Points[idx]["Exploded"] = "true";
        }

        chartMain.Series.Add(ser);
    }

    // ════════════════════════════════════════════════════════════
    //  EVENTOS DEL CHART
    // ════════════════════════════════════════════════════════════
    private void BtnActualizarGrafica_Click(object sender, EventArgs e) => ActualizarChart();
    private void CmbTipoGrafica_SelectedIndexChanged(object sender, EventArgs e) => ActualizarChart();

    // ════════════════════════════════════════════════════════════
    //  CARGA DE ARCHIVOS (async)
    // ════════════════════════════════════════════════════════════
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
        await CargarArchivoAsync(Path.Combine(_dirDatos, "products.json"),   "json", silencioso: true);
        await CargarArchivoAsync(Path.Combine(_dirDatos, "sales.csv"),       "csv",  silencioso: true);
        await CargarArchivoAsync(Path.Combine(_dirDatos, "employees.xml"),   "xml",  silencioso: true);
        await CargarArchivoAsync(Path.Combine(_dirDatos, "records.txt"),     "txt",  silencioso: true);
        ActualizarEstadoBarra($"✅ Todos los archivos cargados. Total: {_datos.Count} registros.");
        MessageBox.Show($"Archivos cargados.\nTotal: {_datos.Count} registros.",
            "Data Fusion Arena", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void MenuCargarPersonalizado_Click(object sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Selecciona un archivo de datos",
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
                    "Archivo no encontrado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ActualizarEstadoBarra($"⏳ Leyendo {Path.GetFileName(ruta)}...");

        List<DataItem> nuevos = await Task.Run(() =>
        {
            List<DataItem> items;
            switch (tipo)
            {
                case "json":
                    items = JsonDataReader.Leer(ruta);
                    break;
                case "csv":
                    items = CsvDataReader.Leer(ruta);
                    break;
                case "xml":
                    items = XmlDataReader.Leer(ruta);
                    // El XML del Iris no tiene departamento/salario,
                    // pero se mantiene la lógica por compatibilidad con employees.xml anterior
                    foreach (var item in items)
                    {
                        if (item.CamposExtra.TryGetValue("departamento", out var dep))
                        { item.Categoria = dep; item.CamposExtra.Remove("departamento"); }
                        if (item.CamposExtra.TryGetValue("salario", out var sal) &&
                            double.TryParse(sal,
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out double sv))
                        { item.Valor = sv; item.CamposExtra.Remove("salario"); }
                    }
                    break;
                case "txt":
                    items = TxtDataReader.Leer(ruta);
                    break;
                default:
                    return new List<DataItem>();
            }
            return items;
        });

        DataProcessor.AgregarDatos(_datos, nuevos);
        await ActualizarTodoAsync();

        if (!silencioso)
            ActualizarEstadoBarra(
                $"✅ {nuevos.Count} registros cargados desde {Path.GetFileName(ruta)}. Total: {_datos.Count}");
    }

    // ════════════════════════════════════════════════════════════
    //  BASES DE DATOS (async)
    // ════════════════════════════════════════════════════════════
    private async void BtnConectarPostgres_Click(object sender, EventArgs e)
    {
        using var dlg = new FormConexionBD("PostgreSQL");
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var pg = new PostgreSqlConnector(dlg.CadenaConexion, dlg.NombreTabla);
        ActualizarEstadoBarra("🔌 Conectando a PostgreSQL...");

        bool ok = await Task.Run(() => pg.ProbarConexion(out _));
        if (!ok)
        {
            pg.ProbarConexion(out string msgErr);
            MessageBox.Show($"Error:\n{msgErr}", "PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ActualizarEstadoBarra("❌ Error de conexión PostgreSQL.");
            return;
        }

        pg.ProbarConexion(out string msg);
        ActualizarEstadoBarra("⏳ Cargando datos PostgreSQL...");
        var datos = await Task.Run(() => pg.LeerDatos());
        _lastPgConnector = pg;
        DataProcessor.AgregarDatos(_datos, datos);
        await ActualizarTodoAsync();
        ActualizarEstadoBarra($"✅ PostgreSQL: {datos.Count} registros. {msg}");
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
            md.ProbarConexion(out string msgErr);
            MessageBox.Show($"Error:\n{msgErr}", "MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ActualizarEstadoBarra("❌ Error de conexión MariaDB.");
            return;
        }

        md.ProbarConexion(out string msg);
        ActualizarEstadoBarra("⏳ Cargando datos MariaDB...");
        var datos = await Task.Run(() => md.LeerDatos());
        _lastMdConnector = md;
        DataProcessor.AgregarDatos(_datos, datos);
        await ActualizarTodoAsync();
        ActualizarEstadoBarra($"✅ MariaDB: {datos.Count} registros. {msg}");
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
            var datos = await Task.Run(() => _lastPgConnector.LeerDatos());
            DataProcessor.AgregarDatos(_datos, datos);
        }

        if (_lastMdConnector != null)
        {
            ActualizarEstadoBarra("♻ Actualizando MariaDB...");
            var datos = await Task.Run(() => _lastMdConnector.LeerDatos());
            DataProcessor.AgregarDatos(_datos, datos);
        }

        await ActualizarTodoAsync();
        ActualizarEstadoBarra($"✅ Datos actualizados. Total: {_datos.Count} registros.");
    }

    // ════════════════════════════════════════════════════════════
    //  TAB 1 – TODOS LOS DATOS
    // ════════════════════════════════════════════════════════════
    private async void BtnFiltrar_Click(object? sender, EventArgs e)
    {
        string campo = cmbCampoBusqueda.Text.ToLower();
        string valor = txtBusqueda.Text.Trim();

        ActualizarEstadoBarra("🔍 Filtrando...");
        _datosVista = string.IsNullOrEmpty(valor)
            ? new List<DataItem>(_datosBase)
            : await Task.Run(() => DataProcessor.Filtrar(_datosBase, campo, valor));

        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        ActualizarEstadoBarra($"Filtro '{campo}'='{valor}' → {_datosVista.Count} resultados.");
    }

    private async void BtnLimpiarFiltro_Click(object? sender, EventArgs e)
    {
        txtBusqueda.Text = "";
        _datosVista = new List<DataItem>(_datosBase);
        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        ActualizarEstadoBarra($"Filtro limpiado. {_datosVista.Count} registros visibles.");
    }

    private async void BtnOrdenar_Click(object? sender, EventArgs e)
    {
        string campo = cmbCampoOrden.Text.ToLower();
        bool   asc   = rbAscendente.Checked;

        ActualizarEstadoBarra("⏳ Ordenando...");
        var ordenado = await Task.Run(() => DataProcessor.Ordenar(_datosVista, campo, asc));
        _datosVista  = ordenado;
        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        ActualizarEstadoBarra($"Ordenado por '{campo}' {(asc ? "↑" : "↓")}. {ordenado.Count} registros.");
    }

    // ════════════════════════════════════════════════════════════
    //  TAB 2 – POR CATEGORÍA
    // ════════════════════════════════════════════════════════════
    private async void LstCategorias_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (lstCategorias.SelectedItem is not string cat) return;
        if (!_porCategoria.TryGetValue(cat, out var lista))       return;
        await BindGridAsync(dgvCategoria, lista, null);
        lblCatInfo.Text =
            $"  {cat}   |   {lista.Count} registros   |   " +
            $"Promedio: {(lista.Count > 0 ? lista.Average(x => x.Valor) : 0):F2}   |   " +
            $"Total: {lista.Sum(x => x.Valor):N2}";
    }

    // ════════════════════════════════════════════════════════════
    //  TAB 3 – ESTADÍSTICAS
    // ════════════════════════════════════════════════════════════
    private async Task ActualizarTabEstadisticasAsync()
    {
        if (dgvEstadisticas.Columns.Count == 0)
        {
            var statCols = new (string H, int W, DataGridViewAutoSizeColumnMode A)[]
            {
                ("Categoría", 0,   DataGridViewAutoSizeColumnMode.Fill),
                ("Cant.",     65,  DataGridViewAutoSizeColumnMode.None),
                ("Promedio",  100, DataGridViewAutoSizeColumnMode.None),
                ("Máximo",    100, DataGridViewAutoSizeColumnMode.None),
                ("Mínimo",    100, DataGridViewAutoSizeColumnMode.None),
                ("Total",     120, DataGridViewAutoSizeColumnMode.None),
            };
            foreach (var (h, w, a) in statCols)
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
            dgvEstadisticas.Rows.Add(
                s.Categoria, s.Cantidad,
                s.Promedio.ToString("F2"),
                s.ValorMaximo.ToString("F2"),
                s.ValorMinimo.ToString("F2"),
                s.SumaValores.ToString("N2"));

        int fuentes = _datosBase.Select(d => d.Fuente).Distinct().Count();
        lblTotalRegistros.Text  = $"Total registros: {_datos.Count}";
        lblTotalCategorias.Text = $"Categorías: {stats.Count}";
        lblTotalFuentes.Text    = $"Fuentes activas: {fuentes}";
    }

    // ════════════════════════════════════════════════════════════
    //  TAB 5 – PROCESAMIENTO
    // ════════════════════════════════════════════════════════════
    private async void BtnDetectarDuplicados_Click(object? sender, EventArgs e)
    {
        ActualizarEstadoBarra("🔍 Detectando duplicados...");
        var dupes = await Task.Run(() => DataProcessor.DetectarDuplicados(_datos));
        await BindGridAsync(dgvProcesamiento, dupes, null);
        if (dupes.Count == 0)
            lblProcInfo.Text = "✅ No se encontraron duplicados.";
        else
        {
            lblProcInfo.Text = $"⚠ {dupes.Count} duplicados encontrados.";
            btnEliminarDuplicados.Enabled = true;
        }
        ActualizarEstadoBarra($"Duplicados detectados: {dupes.Count}");
    }

    private async void BtnEliminarDuplicados_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("¿Eliminar duplicados? Esta acción no se puede deshacer.",
            "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        int antes   = _datos.Count;
        var limpia  = await Task.Run(() => DataProcessor.EliminarDuplicados(_datos));
        _datos.Clear(); _datos.AddRange(limpia);
        await ActualizarTodoAsync();
        lblProcInfo.Text = $"✅ Eliminados {antes - _datos.Count} duplicados. Quedan {_datos.Count}.";
        btnEliminarDuplicados.Enabled = false;
        ActualizarEstadoBarra($"Registros actuales: {_datos.Count}");
    }

    private async void BtnLinqWhere_Click(object? sender, EventArgs e)
    {
        string cat = txtLinqFiltro.Text.Trim();
        var res = await Task.Run(() => DataProcessor.FiltrarLinq(_datosBase, cat).ToList());
        await BindGridAsync(dgvProcesamiento, res, null);
        lblProcInfo.Text = $"LINQ .Where() → {res.Count} resultados para '{cat}'";
    }

    private async void BtnLinqGroupBy_Click(object? sender, EventArgs e)
    {
        var grupos = await Task.Run(() =>
            DataProcessor.AgruparLinq(_datosBase)
                .Select(g => new DataItem
                {
                    Id        = g.Count(),
                    Nombre    = g.Key,
                    Categoria = $"{g.Count()} items",
                    Valor     = g.Average(x => x.Valor),
                    Fuente    = "LINQ GroupBy",
                    Fecha     = DateTime.Now
                }).ToList());

        await BindGridAsync(dgvProcesamiento, grupos, null);
        lblProcInfo.Text = $"LINQ .GroupBy() → {grupos.Count} grupos";
    }

    private async void BtnLinqOrderBy_Click(object? sender, EventArgs e)
    {
        var ordenado = await Task.Run(() => DataProcessor.OrdenarLinq(_datosBase).ToList());
        await BindGridAsync(dgvProcesamiento, ordenado, null);
        lblProcInfo.Text = $"LINQ .OrderByDescending(Valor) → {ordenado.Count} registros";
    }

    // ════════════════════════════════════════════════════════════
    //  ACTUALIZACIÓN GLOBAL
    // ════════════════════════════════════════════════════════════
    private async Task ActualizarTodoAsync()
    {
        _porCategoria = DataProcessor.AgruparPorCategoria(_datos);
        _porId        = DataProcessor.IndexarPorId(_datos);

        ActualizarFuentesCheckedList();
        _datosBase  = GetDatosBase();
        _datosVista = new List<DataItem>(_datosBase);

        ReconstruirCategorias();

        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        await ActualizarTabEstadisticasAsync();

        // Actualizar Chart solo si la pestaña está visible para no bloquear el hilo
        if (tabControl1.SelectedTab == tabGraficas)
            ActualizarChart();

        lblTotalRegistros.Text  = $"Total registros: {_datos.Count}";
        lblTotalCategorias.Text = $"Categorías: {_porCategoria.Count}";
        lblTotalFuentes.Text    = $"Fuentes: {_datos.Select(d => d.Fuente).Distinct().Count()}";
    }

    private void ReconstruirCategorias()
    {
        _porCategoria = DataProcessor.AgruparPorCategoria(_datosBase);
        lstCategorias.Items.Clear();
        foreach (var cat in _porCategoria.Keys.OrderBy(k => k))
            lstCategorias.Items.Add(cat);
    }

    private List<DataItem> GetDatosBase()
    {
        if (clbFuentes.Items.Count == 0) return new List<DataItem>(_datos);
        var seleccionadas = clbFuentes.CheckedItems
                                      .Cast<string>()
                                      .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (seleccionadas.Count == 0) return new List<DataItem>(_datos);
        return _datos.Where(d => seleccionadas.Contains(d.Fuente)).ToList();
    }

    private void ActualizarFuentesCheckedList()
    {
        var prevSeleccionadas = clbFuentes.CheckedItems
                                          .Cast<string>()
                                          .ToHashSet(StringComparer.OrdinalIgnoreCase);

        clbFuentes.ItemCheck -= ClbFuentes_ItemCheck!;
        clbFuentes.Items.Clear();

        foreach (var f in _datos.Select(d => d.Fuente).Distinct().OrderBy(f => f))
        {
            bool marcada = prevSeleccionadas.Count == 0 || prevSeleccionadas.Contains(f);
            clbFuentes.Items.Add(f, marcada);
        }

        clbFuentes.ItemCheck += ClbFuentes_ItemCheck!;
    }

    private async void ClbFuentes_ItemCheck(object sender, ItemCheckEventArgs e)
    {
        BeginInvoke(async () =>
        {
            _datosBase  = GetDatosBase();
            _datosVista = new List<DataItem>(_datosBase);

            ReconstruirCategorias();
            await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
            await ActualizarTabEstadisticasAsync();

            if (tabControl1.SelectedTab == tabGraficas)
                ActualizarChart();

            var nombresActivos = clbFuentes.CheckedItems.Cast<string>().ToList();
            ActualizarEstadoBarra(
                $"Mostrando {_datosVista.Count} registros de: {string.Join(", ", nombresActivos)}");
        });
    }

    // ════════════════════════════════════════════════════════════
    //  BINDING CON DATATABLE (async, dinámico)
    // ════════════════════════════════════════════════════════════
    private async Task BindGridAsync(DataGridView dgv, List<DataItem> items, Label? contadorLabel)
    {
        contadorLabel?.Invoke(() => contadorLabel.Text = "⏳ Cargando...");

        bool limitado      = items.Count > DISPLAY_LIMIT;
        var  itemsDisplay  = limitado ? items.Take(DISPLAY_LIMIT).ToList() : items;

        var dt = await Task.Run(() => BuildDataTable(itemsDisplay));

        if (dgv.InvokeRequired)
            dgv.Invoke(() => AplicarDataTable(dgv, dt, items.Count, limitado, contadorLabel));
        else
            AplicarDataTable(dgv, dt, items.Count, limitado, contadorLabel);
    }

    private void AplicarDataTable(DataGridView dgv, DataTable dt,
        int totalReal, bool limitado, Label? contadorLabel)
    {
        dgv.DataSource = null;
        dgv.Columns.Clear();
        dgv.AutoGenerateColumns = false;

        foreach (DataColumn col in dt.Columns)
        {
            var dgvCol = new DataGridViewTextBoxColumn
            {
                Name             = col.ColumnName,
                HeaderText       = col.ColumnName,
                DataPropertyName = col.ColumnName,
                ReadOnly         = true,
                SortMode         = DataGridViewColumnSortMode.Automatic,
                MinimumWidth     = 60,
            };
            dgvCol.Width = col.ColumnName switch
            {
                "ID"        => 55,
                "Nombre"    => 220,
                "Categoría" => 130,
                "Valor"     => 95,
                "Fecha"     => 100,
                "Fuente"    => 90,
                _           => 120
            };
            if (col.ColumnName == "Nombre")
            {
                dgvCol.AutoSizeMode  = DataGridViewAutoSizeColumnMode.Fill;
                dgvCol.MinimumWidth  = 150;
            }
            dgv.Columns.Add(dgvCol);
        }

        AplicarEstiloGrid(dgv);
        dgv.DataSource = dt;

        dgv.CellFormatting -= DgvCellFormatting!;
        dgv.CellFormatting += DgvCellFormatting!;

        if (contadorLabel != null)
            contadorLabel.Text = limitado
                ? $"⚠ Mostrando {DISPLAY_LIMIT:N0} de {totalReal:N0} (usa filtros para ver más)"
                : $"{totalReal:N0} registros";
    }

    private static void DgvCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var dgv = (DataGridView)sender;
        if (!dgv.Columns.Contains("Fuente")) return;
        try
        {
            var fuenteVal = dgv.Rows[e.RowIndex].Cells["Fuente"].Value?.ToString() ?? "";
            var bg = fuenteVal switch
            {
                "json"        => Color.FromArgb(18, 50, 18),
                "csv"         => Color.FromArgb(50, 44,  8),
                "xml"         => Color.FromArgb( 8, 34, 60),
                "txt"         => Color.FromArgb(44, 16, 52),
                "postgresql"  => Color.FromArgb( 8, 24, 70),
                "mariadb"     => Color.FromArgb(54, 24,  8),
                _             => Color.FromArgb(32, 32, 48)
            };
            e.CellStyle.BackColor           = bg;
            e.CellStyle.ForeColor           = Color.FromArgb(230, 230, 240);
            e.CellStyle.SelectionBackColor  = Color.FromArgb(0, 100, 180);
            e.CellStyle.SelectionForeColor  = Color.White;
        }
        catch { /* ignorar */ }
    }

    private static DataTable BuildDataTable(List<DataItem> items)
    {
        var dt = new DataTable();
        dt.Columns.Add("ID",        typeof(int));
        dt.Columns.Add("Nombre",    typeof(string));
        dt.Columns.Add("Categoría", typeof(string));
        dt.Columns.Add("Valor",     typeof(double));
        dt.Columns.Add("Fecha",     typeof(string));
        dt.Columns.Add("Fuente",    typeof(string));

        var extraKeys = items
            .SelectMany(i => i.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k)
            .ToList();

        foreach (var key in extraKeys)
            dt.Columns.Add(key, typeof(string));

        dt.BeginLoadData();
        foreach (var item in items)
        {
            var row         = dt.NewRow();
            row["ID"]       = item.Id;
            row["Nombre"]   = item.Nombre;
            row["Categoría"]= item.Categoria;
            row["Valor"]    = item.Valor;
            row["Fecha"]    = item.Fecha.ToString("yyyy-MM-dd");
            row["Fuente"]   = item.Fuente;
            foreach (var key in extraKeys)
                row[key] = item.CamposExtra.TryGetValue(key, out var v) ? v : "";
            dt.Rows.Add(row);
        }
        dt.EndLoadData();
        return dt;
    }

    // ════════════════════════════════════════════════════════════
    //  CONFIGURACIÓN DE GRIDS
    // ════════════════════════════════════════════════════════════
    private void ConfigurarDataGridViews()
    {
        foreach (var dgv in new[] { dgvTodos, dgvCategoria, dgvProcesamiento })
        {
            dgv.AutoGenerateColumns = false;
            AplicarEstiloGrid(dgv);
        }

        dgvEstadisticas.Columns.Clear();
        var statCols = new (string H, int W, DataGridViewAutoSizeColumnMode A)[]
        {
            ("Categoría", 0,   DataGridViewAutoSizeColumnMode.Fill),
            ("Cant.",     65,  DataGridViewAutoSizeColumnMode.None),
            ("Promedio",  100, DataGridViewAutoSizeColumnMode.None),
            ("Máximo",    100, DataGridViewAutoSizeColumnMode.None),
            ("Mínimo",    100, DataGridViewAutoSizeColumnMode.None),
            ("Total",     120, DataGridViewAutoSizeColumnMode.None),
        };
        foreach (var (h, w, a) in statCols)
        {
            var col = new DataGridViewTextBoxColumn
            { HeaderText = h, ReadOnly = true, AutoSizeMode = a, MinimumWidth = 55 };
            if (a == DataGridViewAutoSizeColumnMode.None) col.Width = w;
            dgvEstadisticas.Columns.Add(col);
        }
        AplicarEstiloGrid(dgvEstadisticas);
    }

    private static void AplicarEstiloGrid(DataGridView dgv)
    {
        dgv.BackgroundColor  = Color.FromArgb(18, 18, 28);
        dgv.GridColor        = Color.FromArgb(55, 55, 75);
        dgv.BorderStyle      = BorderStyle.None;

        dgv.DefaultCellStyle.BackColor          = Color.FromArgb(28, 28, 42);
        dgv.DefaultCellStyle.ForeColor          = Color.FromArgb(230, 230, 240);
        dgv.DefaultCellStyle.Font               = new Font("Consolas", 9f);
        dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 100, 180);
        dgv.DefaultCellStyle.SelectionForeColor = Color.White;

        dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(33, 33, 50);

        dgv.ColumnHeadersDefaultCellStyle.BackColor   = Color.FromArgb(42, 42, 60);
        dgv.ColumnHeadersDefaultCellStyle.ForeColor   = Color.FromArgb(0, 200, 220);
        dgv.ColumnHeadersDefaultCellStyle.Font        = new Font("Segoe UI", 9f, FontStyle.Bold);
        dgv.ColumnHeadersHeight      = 32;
        dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        dgv.EnableHeadersVisualStyles= false;
        dgv.AllowUserToAddRows       = false;
        dgv.AllowUserToResizeRows    = false;
        dgv.SelectionMode            = DataGridViewSelectionMode.FullRowSelect;
        dgv.RowHeadersVisible        = false;
        dgv.RowTemplate.Height       = 24;
        dgv.ScrollBars               = ScrollBars.Both;
        dgv.ClipboardCopyMode        = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
    }

    // ════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════
    private static void AplicarDoubleBuffer(DataGridView dgv)
    {
        typeof(DataGridView)
            .GetProperty("DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(dgv, true);
    }

    private static Color[] PaletaColores() => new[]
    {
        Color.FromArgb(0,  200, 255), Color.FromArgb(255, 200,   0),
        Color.FromArgb(0,  255, 128), Color.FromArgb(255,  80, 100),
        Color.FromArgb(180,100, 255), Color.FromArgb(255, 150,  50),
        Color.FromArgb(0,  220, 200), Color.FromArgb(220,  80, 220),
        Color.FromArgb(80, 200,  80), Color.FromArgb(100, 160, 255),
        Color.FromArgb(255,100, 130), Color.FromArgb(50,  230, 230)
    };

    private void ActualizarEstadoBarra(string mensaje)
    {
        if (lblStatus.GetCurrentParent()?.InvokeRequired == true)
            lblStatus.GetCurrentParent().Invoke(() => lblStatus.Text = mensaje);
        else
            lblStatus.Text = mensaje;
        Application.DoEvents();
    }

    // ════════════════════════════════════════════════════════════
    //  MENÚ – Limpiar / Acerca de / Salir
    // ════════════════════════════════════════════════════════════
    private void MenuLimpiarDatos_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show("¿Limpiar todos los datos en memoria?", "Confirmar",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        _datos.Clear(); _porCategoria.Clear(); _porId.Clear();
        _datosBase.Clear(); _datosVista.Clear();
        _lastPgConnector = null; _lastMdConnector = null;

        foreach (var dgv in new[] { dgvTodos, dgvCategoria, dgvProcesamiento })
        { dgv.DataSource = null; dgv.Columns.Clear(); }

        dgvEstadisticas.Rows.Clear();
        lstCategorias.Items.Clear();
        clbFuentes.Items.Clear();

        // Reiniciar Chart
        InicializarChart();
        ActualizarChart();

        lblContadorTodos.Text = "0 registros";
        ActualizarEstadoBarra("Datos limpiados.");
    }

    private void MenuAcercaDe_Click(object sender, EventArgs e)
        => MessageBox.Show(
            "Data Fusion Arena\nAdministración y Organización de Datos\n\n" +
            "Ingeniería · 4.º Semestre · C# .NET 10 · WinForms\n\n" +
            "Datasets reales:\n" +
            "  • World Happiness Report 2023 (Gallup/ONU) → JSON\n" +
            "  • Video Game Sales – Kaggle/VGChartz (vgsales) → CSV\n" +
            "  • UCI Iris Dataset – R.A. Fisher (1936) → XML\n" +
            "  • World Athletics World Records 2023 → TXT\n\n" +
            "Fuentes: JSON · CSV · XML · TXT · PostgreSQL · MariaDB\n" +
            "Estructuras: List<T> · Dictionary<TKey,TValue>",
            "Acerca de", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void MenuSalir_Click(object sender, EventArgs e) => Close();
}

// ════════════════════════════════════════════════════════════════
//  Formulario auxiliar de conexión a BD
// ════════════════════════════════════════════════════════════════
public class FormConexionBD : Form
{
    public string CadenaConexion { get; private set; } = "";
    public string NombreTabla    { get; private set; } = "";

    private readonly TextBox txtCadena, txtTabla;

    public FormConexionBD(string motor)
    {
        Text              = $"Conexión a {motor}";
        Size              = new Size(520, 260);
        StartPosition     = FormStartPosition.CenterParent;
        FormBorderStyle   = FormBorderStyle.FixedDialog;
        MaximizeBox       = false;
        BackColor         = Color.FromArgb(30, 30, 45);
        ForeColor         = Color.White;

        string cadenaDefault = motor == "PostgreSQL"
            ? "Host=localhost;Port=5432;Database=datafusion;Username=postgres;Password=TU_PASSWORD;"
            : "Server=localhost;Port=3306;Database=datafusion;User=root;Password=TU_PASSWORD;";
        string tablaDefault = motor == "PostgreSQL" ? "videojuegos" : "felicidad_mundial";

        var lblC = new Label { Text = "Cadena de conexión:", Location = new Point(15, 20),  AutoSize = true, ForeColor = Color.Cyan };
        txtCadena = new TextBox { Location = new Point(15, 42), Width = 475, Text = cadenaDefault, BackColor = Color.FromArgb(45, 45, 65), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

        var lblT = new Label { Text = "Nombre de tabla:", Location = new Point(15, 85), AutoSize = true, ForeColor = Color.Cyan };
        txtTabla  = new TextBox { Location = new Point(15, 107), Width = 200, Text = tablaDefault, BackColor = Color.FromArgb(45, 45, 65), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

        var btnOk  = new Button { Text = "Conectar",  Location = new Point(310, 170), Width = 90, DialogResult = DialogResult.OK,     BackColor = Color.FromArgb(0, 120, 212), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var btnCan = new Button { Text = "Cancelar",  Location = new Point(410, 170), Width = 80, DialogResult = DialogResult.Cancel,  BackColor = Color.FromArgb(80, 30,  30), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnOk.Click += (_, _) => { CadenaConexion = txtCadena.Text.Trim(); NombreTabla = txtTabla.Text.Trim(); };

        Controls.AddRange(new Control[] { lblC, txtCadena, lblT, txtTabla, btnOk, btnCan });
        AcceptButton = btnOk;
        CancelButton = btnCan;
    }
}
