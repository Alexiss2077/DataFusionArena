using DataFusionArena.Shared.Models;
using DataFusionArena.Shared.Readers;
using DataFusionArena.Shared.Database;
using DataFusionArena.Shared.Processing;
using System.Data;
using System.Reflection;

namespace DataFusionArena.WinForms;

public partial class MainForm : Form
{
    // ── Estado global ────────────────────────────────────────────
    private readonly List<DataItem> _datos = new();
    private List<DataItem> _datosBase = new();   // filtrado por fuente (checkboxes)
    private List<DataItem> _datosVista = new();  // filtrado por campo/valor

    private Dictionary<string, List<DataItem>> _porCategoria = new();
    private Dictionary<int, DataItem> _porId = new();

    // Conectores BD para refresh
    private PostgreSqlConnector? _lastPgConnector;
    private MariaDbConnector? _lastMdConnector;

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

        ActualizarEstadoBarra("Listo. Cargue datos usando el menú o los botones.");
        Text = "Data Fusion Arena – Administración y Organización de Datos";

        Load += (s, e) =>
        {
            splitMain.SplitterDistance = 170;
            splitCategoria.SplitterDistance = 220;
        };

        tabControl1.SelectedIndexChanged += (s, e) =>
        {
            if (tabControl1.SelectedTab == tabGraficas) pnlChart.Invalidate();
        };
    }

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
        await CargarArchivoAsync(Path.Combine(_dirDatos, "products.json"), "json", silencioso: true);
        await CargarArchivoAsync(Path.Combine(_dirDatos, "sales.csv"), "csv", silencioso: true);
        await CargarArchivoAsync(Path.Combine(_dirDatos, "employees.xml"), "xml", silencioso: true);
        await CargarArchivoAsync(Path.Combine(_dirDatos, "records.txt"), "txt", silencioso: true);
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
                    "Archivo no encontrado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ActualizarEstadoBarra($"⏳ Leyendo {Path.GetFileName(ruta)}...");

        List<DataItem> nuevos = await Task.Run(() =>
        {
            List<DataItem> items;
            switch (tipo)
            {
                case "json": items = JsonDataReader.Leer(ruta); break;
                case "csv": items = CsvDataReader.Leer(ruta); break;
                case "xml":
                    items = XmlDataReader.Leer(ruta);
                    foreach (var item in items)
                    {
                        if (item.CamposExtra.TryGetValue("departamento", out var dep))
                        { item.Categoria = dep; item.CamposExtra.Remove("departamento"); }
                        if (item.CamposExtra.TryGetValue("salario", out var sal) &&
                            double.TryParse(sal, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out double sv))
                        { item.Valor = sv; item.CamposExtra.Remove("salario"); }
                    }
                    break;
                case "txt": items = TxtDataReader.Leer(ruta); break;
                default: return new List<DataItem>();
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
            ActualizarEstadoBarra($"❌ Error de conexión PostgreSQL.");
            return;
        }

        pg.ProbarConexion(out string msg);
        ActualizarEstadoBarra($"⏳ Cargando datos PostgreSQL...");
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
            ActualizarEstadoBarra($"✅ PostgreSQL actualizado: {datos.Count} registros.");
        }

        if (_lastMdConnector != null)
        {
            ActualizarEstadoBarra("♻ Actualizando MariaDB...");
            var datos = await Task.Run(() => _lastMdConnector.LeerDatos());
            DataProcessor.AgregarDatos(_datos, datos);
            ActualizarEstadoBarra($"✅ MariaDB actualizado: {datos.Count} registros.");
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
        bool asc = rbAscendente.Checked;

        ActualizarEstadoBarra("⏳ Ordenando...");
        var ordenado = await Task.Run(() => DataProcessor.Ordenar(_datosVista, campo, asc));
        _datosVista = ordenado;
        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        ActualizarEstadoBarra($"Ordenado por '{campo}' {(asc ? "↑" : "↓")}. {ordenado.Count} registros.");
    }

    // ════════════════════════════════════════════════════════════
    //  TAB 2 – POR CATEGORÍA
    // ════════════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════════════
    //  TAB 3 – ESTADÍSTICAS
    // ════════════════════════════════════════════════════════════
    private async Task ActualizarTabEstadisticasAsync()
    {
        // Reconstruir columnas si fueron borradas (p.ej. tras limpiar datos)
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

        if (_datosBase.Count == 0)
        {
            dgvEstadisticas.Rows.Clear();
            return;
        }

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
        lblTotalRegistros.Text = $"Total registros: {_datos.Count}";
        lblTotalCategorias.Text = $"Categorías: {stats.Count}";
        lblTotalFuentes.Text = $"Fuentes activas: {fuentes}";
    }

    // ════════════════════════════════════════════════════════════
    //  TAB 4 – GRÁFICAS (GDI+ puro — usa _datosBase)
    // ════════════════════════════════════════════════════════════
    private void BtnActualizarGrafica_Click(object sender, EventArgs e) =>
        pnlChart.Invalidate();
    private void CmbTipoGrafica_SelectedIndexChanged(object sender, EventArgs e) =>
        pnlChart.Invalidate();

    private void PnlChart_Paint(object sender, PaintEventArgs e)
    {
        var fuente = _datosBase.Count > 0 ? _datosBase : _datos;
        if (fuente.Count == 0)
        {
            using var fnt = new Font("Segoe UI", 13f);
            e.Graphics.DrawString("Sin datos. Cargue archivos primero.", fnt, Brushes.Gray, 40, 40);
            return;
        }

        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var stats = DataProcessor.CalcularEstadisticas(fuente)
                                 .Values.OrderByDescending(x => x.SumaValores).ToList();
        if (stats.Count == 0) return;

        int W = pnlChart.Width, H = pnlChart.Height;
        string tipo = cmbTipoGrafica.Text;
        if (tipo == "Pastel")
            DibujarPastel(g, stats, W, H);
        else
            DibujarBarras(g, stats, W, H, tipo == "Barras");
    }

    private void DibujarBarras(Graphics g, List<EstadisticasCategoria> stats, int W, int H, bool horizontal)
    {
        int mIzq = horizontal ? 165 : 55;
        int mDer = 30, mTop = 45, mBot = horizontal ? 25 : 75;
        int aW = W - mIzq - mDer, aH = H - mTop - mBot;
        if (aW <= 0 || aH <= 0) return;

        double maxVal = stats.Max(s => s.SumaValores);
        if (maxVal <= 0) return;

        g.FillRectangle(new SolidBrush(Color.FromArgb(28, 28, 45)), mIzq, mTop, aW, aH);

        using var fntT = new Font("Segoe UI", 12f, FontStyle.Bold);
        g.DrawString("Valor total por categoría", fntT,
            new SolidBrush(Color.FromArgb(230, 230, 240)), mIzq, 8);

        var colores = PaletaColores();
        using var fntL = new Font("Segoe UI", 8f);
        using var fntV = new Font("Segoe UI", 8f, FontStyle.Bold);
        int n = stats.Count;

        if (!horizontal)
        {
            using var penGuia = new Pen(Color.FromArgb(50, 50, 70));
            for (int gi = 1; gi <= 4; gi++)
            {
                int gy = mTop + aH - (int)(aH * gi / 4.0);
                g.DrawLine(penGuia, mIzq, gy, mIzq + aW, gy);
                string gStr = FormatVal(maxVal * gi / 4.0);
                g.DrawString(gStr, fntL, Brushes.DimGray, mIzq - 50, gy - 7);
            }
            int slot = aW / Math.Max(n, 1), barW = Math.Max(10, slot - 16);
            for (int i = 0; i < n; i++)
            {
                var s = stats[i];
                int barH = (int)(s.SumaValores / maxVal * aH);
                int x = mIzq + i * slot + (slot - barW) / 2;
                int y = mTop + aH - barH;
                using var br = new SolidBrush(colores[i % colores.Length]);
                g.FillRectangle(br, x, y, barW, barH);
                string val = FormatVal(s.SumaValores);
                var vs = g.MeasureString(val, fntV);
                g.DrawString(val, fntV, Brushes.White, x + (barW - vs.Width) / 2,
                    Math.Max(mTop, y - vs.Height - 2));
                string lbl = s.Categoria.Length > 13 ? s.Categoria[..13] + "…" : s.Categoria;
                g.TranslateTransform(x + barW / 2f, mTop + aH + 5);
                g.RotateTransform(38);
                g.DrawString(lbl, fntL, Brushes.LightGray, 0, 0);
                g.ResetTransform();
            }
        }
        else
        {
            int slot = aH / Math.Max(n, 1), barH = Math.Max(8, slot - 10);
            for (int i = 0; i < n; i++)
            {
                var s = stats[i];
                int barW = (int)(s.SumaValores / maxVal * aW);
                int y = mTop + i * slot + (slot - barH) / 2;
                using var br = new SolidBrush(colores[i % colores.Length]);
                g.FillRectangle(br, mIzq, y, barW, barH);
                string lbl = s.Categoria.Length > 20 ? s.Categoria[..20] + "…" : s.Categoria;
                g.DrawString(lbl, fntL, Brushes.LightGray, 2, y + (barH - 14) / 2);
                g.DrawString(FormatVal(s.SumaValores), fntV, Brushes.White,
                    mIzq + barW + 4, y + (barH - 14) / 2);
            }
        }
    }

    private void DibujarPastel(Graphics g, List<EstadisticasCategoria> stats, int W, int H)
    {
        double total = stats.Sum(s => s.SumaValores);
        if (total <= 0) return;

        int size = Math.Max(100, Math.Min(W - 280, H - 80));
        int x0 = 30, y0 = (H - size) / 2;

        using var fntT = new Font("Segoe UI", 12f, FontStyle.Bold);
        g.DrawString("Distribución por categoría", fntT,
            new SolidBrush(Color.FromArgb(230, 230, 240)), x0, 8);

        var colores = PaletaColores();
        using var fntL = new Font("Segoe UI", 8.5f);
        float start = -90f;

        for (int i = 0; i < stats.Count; i++)
        {
            float sweep = (float)(stats[i].SumaValores / total * 360.0);
            using var br = new SolidBrush(colores[i % colores.Length]);
            g.FillPie(br, x0, y0, size, size, start, sweep);
            g.DrawPie(Pens.Black, x0, y0, size, size, start, sweep);

            int lx = x0 + size + 25, ly = y0 + i * 24;
            if (ly + 16 > H) break;
            g.FillRectangle(br, lx, ly + 4, 14, 14);
            string pct = $"{stats[i].SumaValores / total:P1}";
            string lbl = stats[i].Categoria.Length > 20
                ? stats[i].Categoria[..20] + "…" : stats[i].Categoria;
            g.DrawString($"{lbl}  {pct}", fntL, Brushes.LightGray, lx + 18, ly);
            start += sweep;
        }
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

        int antes = _datos.Count;
        var limpia = await Task.Run(() => DataProcessor.EliminarDuplicados(_datos));
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
                             Id = g.Count(),
                             Nombre = g.Key,
                             Categoria = $"{g.Count()} items",
                             Valor = g.Average(x => x.Valor),
                             Fuente = "LINQ GroupBy",
                             Fecha = DateTime.Now
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
        _porId = DataProcessor.IndexarPorId(_datos);

        ActualizarFuentesCheckedList();
        _datosBase = GetDatosBase();
        _datosVista = new List<DataItem>(_datosBase);

        // Reconstruir categorías desde _datosBase
        ReconstruirCategorias();

        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        await ActualizarTabEstadisticasAsync();
        pnlChart.Invalidate();

        lblTotalRegistros.Text = $"Total registros: {_datos.Count}";
        lblTotalCategorias.Text = $"Categorías: {_porCategoria.Count}";
        lblTotalFuentes.Text = $"Fuentes: {_datos.Select(d => d.Fuente).Distinct().Count()}";
    }

    private void ReconstruirCategorias()
    {
        // Usa _datosBase para que el panel izquierdo filtre también por categoría
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
        // BeginInvoke para tener el nuevo estado de checkboxes
        BeginInvoke(async () =>
        {
            _datosBase = GetDatosBase();
            _datosVista = new List<DataItem>(_datosBase);

            // Propagar a TODAS las pestañas
            ReconstruirCategorias();
            await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
            await ActualizarTabEstadisticasAsync();
            pnlChart.Invalidate();

            var nombresActivos = clbFuentes.CheckedItems.Cast<string>().ToList();
            ActualizarEstadoBarra(
                $"Mostrando {_datosVista.Count} registros de: {string.Join(", ", nombresActivos)}");
        });
    }

    // ════════════════════════════════════════════════════════════
    //  BINDING CON DATATABLE (async, dinámico)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Crea y vincula un DataTable con columnas dinámicas al DataGridView indicado.
    /// Columnas fijas + todas las claves de CamposExtra que tenga el conjunto.
    /// </summary>
    private async Task BindGridAsync(DataGridView dgv, List<DataItem> items, Label? contadorLabel)
    {
        contadorLabel?.Invoke(() => contadorLabel.Text = "⏳ Cargando...");

        bool limitado = items.Count > DISPLAY_LIMIT;
        var itemsDisplay = limitado ? items.Take(DISPLAY_LIMIT).ToList() : items;

        var dt = await Task.Run(() => BuildDataTable(itemsDisplay));

        // Volver al hilo UI
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

        // Crear columnas estilizadas desde el DataTable
        foreach (DataColumn col in dt.Columns)
        {
            var dgvCol = new DataGridViewTextBoxColumn
            {
                Name = col.ColumnName,
                HeaderText = col.ColumnName,
                DataPropertyName = col.ColumnName,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.Automatic,
                MinimumWidth = 60,
            };

            // Ancho por tipo de columna
            dgvCol.Width = col.ColumnName switch
            {
                "ID" => 55,
                "Nombre" => 220,
                "Categoría" => 130,
                "Valor" => 95,
                "Fecha" => 100,
                "Fuente" => 90,
                _ => 120   // columnas extra del CamposExtra
            };

            // Columna Nombre usa Fill para aprovechar el espacio
            if (col.ColumnName == "Nombre")
            {
                dgvCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dgvCol.MinimumWidth = 150;
            }

            dgv.Columns.Add(dgvCol);
        }

        AplicarEstiloGrid(dgv);
        dgv.DataSource = dt;

        // Colorear filas por fuente vía CellFormatting (eficiente: solo celdas visibles)
        dgv.CellFormatting -= DgvCellFormatting!;
        dgv.CellFormatting += DgvCellFormatting!;

        if (contadorLabel != null)
        {
            contadorLabel.Text = limitado
                ? $"⚠ Mostrando {DISPLAY_LIMIT:N0} de {totalReal:N0} (usa filtros para ver más)"
                : $"{totalReal:N0} registros";
        }
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
                "json" => Color.FromArgb(18, 50, 18),
                "csv" => Color.FromArgb(50, 44, 8),
                "xml" => Color.FromArgb(8, 34, 60),
                "txt" => Color.FromArgb(44, 16, 52),
                "postgresql" => Color.FromArgb(8, 24, 70),
                "mariadb" => Color.FromArgb(54, 24, 8),
                _ => Color.FromArgb(32, 32, 48)
            };
            e.CellStyle.BackColor = bg;
            e.CellStyle.ForeColor = Color.FromArgb(230, 230, 240);
            e.CellStyle.SelectionBackColor = Color.FromArgb(0, 100, 180);
            e.CellStyle.SelectionForeColor = Color.White;
        }
        catch { /* ignorar */ }
    }

    /// <summary>
    /// Construye un DataTable con columnas fijas + columnas del CamposExtra.
    /// Ejecutar en hilo de fondo (no toca UI).
    /// </summary>
    private static DataTable BuildDataTable(List<DataItem> items)
    {
        var dt = new DataTable();

        // Columnas fijas base
        dt.Columns.Add("ID", typeof(int));
        dt.Columns.Add("Nombre", typeof(string));
        dt.Columns.Add("Categoría", typeof(string));
        dt.Columns.Add("Valor", typeof(double));
        dt.Columns.Add("Fecha", typeof(string));
        dt.Columns.Add("Fuente", typeof(string));

        // Columnas extra dinámicas — detectar todas las claves únicas
        var extraKeys = items
            .SelectMany(i => i.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k)
            .ToList();

        foreach (var key in extraKeys)
            dt.Columns.Add(key, typeof(string));

        // Poblar filas en lote (BeginLoadData desactiva validaciones internas)
        dt.BeginLoadData();
        foreach (var item in items)
        {
            var row = dt.NewRow();
            row["ID"] = item.Id;
            row["Nombre"] = item.Nombre;
            row["Categoría"] = item.Categoria;
            row["Valor"] = item.Valor;
            row["Fecha"] = item.Fecha.ToString("yyyy-MM-dd");
            row["Fuente"] = item.Fuente;
            foreach (var key in extraKeys)
                row[key] = item.CamposExtra.TryGetValue(key, out var v) ? v : "";
            dt.Rows.Add(row);
        }
        dt.EndLoadData();
        return dt;
    }

    // ════════════════════════════════════════════════════════════
    //  CONFIGURACIÓN DE GRIDS (estilos, sin columnas fijas)
    // ════════════════════════════════════════════════════════════
    private void ConfigurarDataGridViews()
    {
        // Grids con columnas dinámicas — solo estilo base
        foreach (var dgv in new[] { dgvTodos, dgvCategoria, dgvProcesamiento })
        {
            dgv.AutoGenerateColumns = false;
            AplicarEstiloGrid(dgv);
        }

        // Estadísticas — columnas fijas conocidas
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
            {
                HeaderText = h,
                ReadOnly = true,
                AutoSizeMode = a,
                MinimumWidth = 55
            };
            if (a == DataGridViewAutoSizeColumnMode.None) col.Width = w;
            dgvEstadisticas.Columns.Add(col);
        }
        AplicarEstiloGrid(dgvEstadisticas);
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
        dgv.AllowUserToResizeRows = false;   // +rendimiento
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.RowHeadersVisible = false;
        dgv.RowTemplate.Height = 24;
        dgv.ScrollBars = ScrollBars.Both;
        dgv.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
    }

    // ════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════
    private static void AplicarDoubleBuffer(DataGridView dgv)
    {
        // Activa DoubleBuffered via reflexión (propiedad protegida)
        typeof(DataGridView)
            .GetProperty("DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(dgv, true);
    }

    private static string FormatVal(double v)
        => v >= 1_000_000 ? $"{v / 1_000_000:F1}M"
         : v >= 1_000 ? $"{v / 1_000:F1}K"
         : $"{v:F0}";

    private static Color[] PaletaColores() => new[]
    {
        Color.FromArgb(0,  200, 255), Color.FromArgb(255, 200,   0),
        Color.FromArgb(0,  255, 128), Color.FromArgb(255,  80, 100),
        Color.FromArgb(180,100, 255), Color.FromArgb(255, 150,  50),
        Color.FromArgb(0,  220, 200), Color.FromArgb(220,  80, 220),
        Color.FromArgb(80, 200,  80), Color.FromArgb(100, 160, 255)
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
        {
            dgv.DataSource = null;
            dgv.Columns.Clear();
        }
        // Estadísticas: solo limpiar filas, conservar columnas fijas
        dgvEstadisticas.Rows.Clear();
        lstCategorias.Items.Clear();
        clbFuentes.Items.Clear();
        pnlChart.Invalidate();
        lblContadorTodos.Text = "0 registros";
        ActualizarEstadoBarra("Datos limpiados.");
    }

    private void MenuAcercaDe_Click(object sender, EventArgs e)
        => MessageBox.Show(
            "Data Fusion Arena\nAdministración y Organización de Datos\n\n" +
            "Ingeniería · 4.º Semestre · C# .NET 10 · WinForms\n\n" +
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
    public string NombreTabla { get; private set; } = "";

    private readonly TextBox txtCadena, txtTabla;

    public FormConexionBD(string motor)
    {
        Text = $"Conexión a {motor}";
        Size = new Size(520, 260);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.FromArgb(30, 30, 45);
        ForeColor = Color.White;

        string cadenaDefault = motor == "PostgreSQL"
            ? "Host=localhost;Port=5432;Database=Prueba;Username=postgres;Password=papu31;"
            : "Server=localhost;Port=3306;Database=Prueba;User=root;Password=papu31;";
        string tablaDefault = motor == "PostgreSQL" ? "videojuegos" : "puntuaciones";

        var lblC = new Label { Text = "Cadena de conexión:", Location = new Point(15, 20), AutoSize = true, ForeColor = Color.Cyan };
        txtCadena = new TextBox { Location = new Point(15, 42), Width = 475, Text = cadenaDefault, BackColor = Color.FromArgb(45, 45, 65), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

        var lblT = new Label { Text = "Nombre de tabla:", Location = new Point(15, 85), AutoSize = true, ForeColor = Color.Cyan };
        txtTabla = new TextBox { Location = new Point(15, 107), Width = 200, Text = tablaDefault, BackColor = Color.FromArgb(45, 45, 65), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

        var btnOk = new Button { Text = "Conectar", Location = new Point(310, 170), Width = 90, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(0, 120, 212), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var btnCan = new Button { Text = "Cancelar", Location = new Point(410, 170), Width = 80, DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(80, 30, 30), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnOk.Click += (_, _) => { CadenaConexion = txtCadena.Text.Trim(); NombreTabla = txtTabla.Text.Trim(); };

        Controls.AddRange(new Control[] { lblC, txtCadena, lblT, txtTabla, btnOk, btnCan });
        AcceptButton = btnOk;
        CancelButton = btnCan;
    }
}