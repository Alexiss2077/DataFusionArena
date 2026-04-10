using DataFusionArena.Shared.Models;
using DataFusionArena.Shared.Readers;
using DataFusionArena.Shared.Database;
using DataFusionArena.Shared.Processing;

namespace DataFusionArena.WinForms;

public partial class MainForm : Form
{
    // ── Estado global ────────────────────────────────────────────
    private readonly List<DataItem> _datos = new();

    /// <summary>
    /// Vista filtrada por fuentes seleccionadas en el panel izquierdo.
    /// TODAS las operaciones (filtrar, ordenar, LINQ) operan sobre _datosBase,
    /// no sobre _datos completos.
    /// </summary>
    private List<DataItem> _datosBase = new(); // datos filtrados por fuente (checkbox)
    private List<DataItem> _datosVista = new(); // datos filtrados por campo/valor encima de _datosBase

    private Dictionary<string, List<DataItem>> _porCategoria = new();
    private Dictionary<int, DataItem> _porId = new();

    // Últimos conectores de BD (para el botón Refresh)
    private PostgreSqlConnector? _lastPgConnector;
    private MariaDbConnector? _lastMdConnector;

    private readonly string _dirDatos = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "SampleData");

    // ════════════════════════════════════════════════════════════
    //  CONSTRUCTOR
    // ════════════════════════════════════════════════════════════
    public MainForm()
    {
        InitializeComponent();
        ConfigurarDataGridViews();
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
                pnlChart.Invalidate();
        };
    }

    // ════════════════════════════════════════════════════════════
    //  CARGA DE ARCHIVOS
    // ════════════════════════════════════════════════════════════
    private void BtnCargarJson_Click(object sender, EventArgs e) => CargarArchivo(Path.Combine(_dirDatos, "products.json"), "json");
    private void BtnCargarCsv_Click(object sender, EventArgs e) => CargarArchivo(Path.Combine(_dirDatos, "sales.csv"), "csv");
    private void BtnCargarXml_Click(object sender, EventArgs e) => CargarArchivo(Path.Combine(_dirDatos, "employees.xml"), "xml");
    private void BtnCargarTxt_Click(object sender, EventArgs e) => CargarArchivo(Path.Combine(_dirDatos, "records.txt"), "txt");

    private void BtnCargarTodo_Click(object sender, EventArgs e)
    {
        CargarArchivo(Path.Combine(_dirDatos, "products.json"), "json", silencioso: true);
        CargarArchivo(Path.Combine(_dirDatos, "sales.csv"), "csv", silencioso: true);
        CargarArchivo(Path.Combine(_dirDatos, "employees.xml"), "xml", silencioso: true);
        CargarArchivo(Path.Combine(_dirDatos, "records.txt"), "txt", silencioso: true);
        ActualizarTodo();
        ActualizarEstadoBarra($"✅ Todos los archivos cargados. Total: {_datos.Count} registros.");
        MessageBox.Show($"Archivos cargados exitosamente.\nTotal de registros: {_datos.Count}",
            "Data Fusion Arena", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void MenuCargarPersonalizado_Click(object sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Selecciona un archivo de datos",
            Filter = "Archivos soportados|*.json;*.csv;*.xml;*.txt|JSON|*.json|CSV|*.csv|XML|*.xml|TXT|*.txt"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            CargarArchivo(dlg.FileName, Path.GetExtension(dlg.FileName).TrimStart('.').ToLower());
    }

    private void CargarArchivo(string ruta, string tipo, bool silencioso = false)
    {
        if (!File.Exists(ruta))
        {
            if (!silencioso)
                MessageBox.Show($"Archivo no encontrado:\n{ruta}", "Archivo no encontrado",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        List<DataItem> nuevos;
        switch (tipo)
        {
            case "json": nuevos = JsonDataReader.Leer(ruta); break;
            case "csv": nuevos = CsvDataReader.Leer(ruta); break;
            case "xml":
                nuevos = XmlDataReader.Leer(ruta);
                foreach (var item in nuevos)
                {
                    if (item.CamposExtra.TryGetValue("departamento", out var dep))
                    { item.Categoria = dep; item.CamposExtra.Remove("departamento"); }
                    if (item.CamposExtra.TryGetValue("salario", out var sal) &&
                        double.TryParse(sal, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double sv))
                    { item.Valor = sv; item.CamposExtra.Remove("salario"); }
                }
                break;
            case "txt": nuevos = TxtDataReader.Leer(ruta); break;
            default: return;
        }

        DataProcessor.AgregarDatos(_datos, nuevos);
        ActualizarTodo();

        if (!silencioso)
            ActualizarEstadoBarra($"✅ {nuevos.Count} registros cargados desde {Path.GetFileName(ruta)}. Total: {_datos.Count}");
    }

    // ════════════════════════════════════════════════════════════
    //  BASES DE DATOS
    // ════════════════════════════════════════════════════════════
    private void BtnConectarPostgres_Click(object sender, EventArgs e)
    {
        using var dlg = new FormConexionBD("PostgreSQL");
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var pg = new PostgreSqlConnector(dlg.CadenaConexion, dlg.NombreTabla);
        ActualizarEstadoBarra("Conectando a PostgreSQL...");

        if (pg.ProbarConexion(out string msg))
        {
            _lastPgConnector = pg; // guardar para refresh
            ActualizarEstadoBarra($"Cargando datos de PostgreSQL ({msg})...");
            var datos = pg.LeerDatos();
            DataProcessor.AgregarDatos(_datos, datos);
            ActualizarTodo();
            ActualizarEstadoBarra($"✅ PostgreSQL: {datos.Count} registros. {msg}");
        }
        else
        {
            MessageBox.Show($"Error:\n{msg}", "PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ActualizarEstadoBarra($"❌ {msg}");
        }
    }

    private void BtnConectarMariaDB_Click(object sender, EventArgs e)
    {
        using var dlg = new FormConexionBD("MariaDB");
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var md = new MariaDbConnector(dlg.CadenaConexion, dlg.NombreTabla);
        ActualizarEstadoBarra("Conectando a MariaDB...");

        if (md.ProbarConexion(out string msg))
        {
            _lastMdConnector = md; // guardar para refresh
            ActualizarEstadoBarra($"Cargando datos de MariaDB ({msg})...");
            var datos = md.LeerDatos();
            DataProcessor.AgregarDatos(_datos, datos);
            ActualizarTodo();
            ActualizarEstadoBarra($"✅ MariaDB: {datos.Count} registros. {msg}");
        }
        else
        {
            MessageBox.Show($"Error:\n{msg}", "MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ActualizarEstadoBarra($"❌ {msg}");
        }
    }

    /// <summary>
    /// Botón Refresh: re-descarga datos de todas las BDs conectadas.
    /// Los registros actuales de BD se eliminan y se recargan frescos.
    /// </summary>
    private void BtnRefresh_Click(object sender, EventArgs e)
    {
        bool hayAlgo = _lastPgConnector != null || _lastMdConnector != null;
        if (!hayAlgo)
        {
            MessageBox.Show("No hay bases de datos conectadas.\nConéctate primero con los botones de PostgreSQL o MariaDB.",
                "Sin conexión", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Eliminar registros que venían de BD
        _datos.RemoveAll(d => d.Fuente is "postgresql" or "mariadb");

        if (_lastPgConnector != null)
        {
            ActualizarEstadoBarra("Actualizando desde PostgreSQL...");
            try
            {
                if (_lastPgConnector.ProbarConexion(out string msg))
                {
                    var datos = _lastPgConnector.LeerDatos();
                    DataProcessor.AgregarDatos(_datos, datos);
                    ActualizarEstadoBarra($"✅ PostgreSQL actualizado: {datos.Count} registros. {msg}");
                }
                else
                {
                    ActualizarEstadoBarra($"❌ PostgreSQL: {msg}");
                }
            }
            catch (Exception ex)
            {
                ActualizarEstadoBarra($"❌ PostgreSQL error: {ex.Message}");
            }
        }

        if (_lastMdConnector != null)
        {
            ActualizarEstadoBarra("Actualizando desde MariaDB...");
            try
            {
                if (_lastMdConnector.ProbarConexion(out string msg))
                {
                    var datos = _lastMdConnector.LeerDatos();
                    DataProcessor.AgregarDatos(_datos, datos);
                    ActualizarEstadoBarra($"✅ MariaDB actualizado: {datos.Count} registros. {msg}");
                }
                else
                {
                    ActualizarEstadoBarra($"❌ MariaDB: {msg}");
                }
            }
            catch (Exception ex)
            {
                ActualizarEstadoBarra($"❌ MariaDB error: {ex.Message}");
            }
        }

        ActualizarTodo();
        ActualizarEstadoBarra($"✅ Datos actualizados. Total: {_datos.Count} registros.");
    }

    // ════════════════════════════════════════════════════════════
    //  TAB 1 – TODOS LOS DATOS
    // ════════════════════════════════════════════════════════════
    private void BtnFiltrar_Click(object sender, EventArgs e)
    {
        string campo = cmbCampoBusqueda.Text.ToLower();
        string valor = txtBusqueda.Text.Trim();

        // SIEMPRE filtramos sobre _datosBase (respeta fuentes desmarcadas)
        _datosVista = string.IsNullOrEmpty(valor)
            ? new List<DataItem>(_datosBase)
            : DataProcessor.Filtrar(_datosBase, campo, valor);

        BindGridTodos(_datosVista);
        ActualizarEstadoBarra($"Filtro '{campo}' = '{valor}' → {_datosVista.Count} resultados.");
    }

    private void BtnLimpiarFiltro_Click(object sender, EventArgs e)
    {
        txtBusqueda.Text = "";
        _datosVista = new List<DataItem>(_datosBase);
        BindGridTodos(_datosVista);
        ActualizarEstadoBarra($"Filtro limpiado. {_datosVista.Count} registros visibles.");
    }

    private void BtnOrdenar_Click(object sender, EventArgs e)
    {
        string campo = cmbCampoOrden.Text.ToLower();
        bool asc = rbAscendente.Checked;

        // Ordenar sobre la vista actual (respeta filtros de fuente y de campo)
        var ordenado = DataProcessor.Ordenar(_datosVista, campo, asc);
        BindGridTodos(ordenado);
        ActualizarEstadoBarra($"Ordenado por '{campo}' {(asc ? "↑" : "↓")}. {ordenado.Count} registros.");
    }

    // ════════════════════════════════════════════════════════════
    //  TAB 2 – POR CATEGORÍA
    // ════════════════════════════════════════════════════════════
    private void LstCategorias_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (lstCategorias.SelectedItem is not string cat) return;
        if (!_porCategoria.TryGetValue(cat, out var lista)) return;
        BindGridCategoria(lista);
        lblCatInfo.Text = $"  {cat}   |   {lista.Count} registros   |   " +
                          $"Promedio: {lista.Average(x => x.Valor):F2}   |   " +
                          $"Total: {lista.Sum(x => x.Valor):N2}";
    }

    // ════════════════════════════════════════════════════════════
    //  TAB 3 – ESTADÍSTICAS
    // ════════════════════════════════════════════════════════════
    private void ActualizarTabEstadisticas()
    {
        if (_datos.Count == 0) return;
        var stats = DataProcessor.CalcularEstadisticas(_datos);
        dgvEstadisticas.Rows.Clear();
        foreach (var s in stats.Values.OrderByDescending(x => x.SumaValores))
            dgvEstadisticas.Rows.Add(s.Categoria, s.Cantidad,
                s.Promedio.ToString("F2"), s.ValorMaximo.ToString("F2"),
                s.ValorMinimo.ToString("F2"), s.SumaValores.ToString("N2"));

        lblTotalRegistros.Text = $"Total registros: {_datos.Count}";
        lblTotalCategorias.Text = $"Categorías: {_porCategoria.Count}";
        lblTotalFuentes.Text = $"Fuentes: {_datos.Select(d => d.Fuente).Distinct().Count()}";
    }

    // ════════════════════════════════════════════════════════════
    //  TAB 4 – GRÁFICAS (GDI+ puro)
    // ════════════════════════════════════════════════════════════
    private void BtnActualizarGrafica_Click(object sender, EventArgs e) => pnlChart.Invalidate();
    private void CmbTipoGrafica_SelectedIndexChanged(object sender, EventArgs e) => pnlChart.Invalidate();

    private void PnlChart_Paint(object sender, PaintEventArgs e)
    {
        if (_datos.Count == 0)
        {
            using var fnt = new Font("Segoe UI", 13f);
            e.Graphics.DrawString("Sin datos. Cargue archivos primero.", fnt, Brushes.Gray, 40, 40);
            return;
        }

        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var stats = DataProcessor.CalcularEstadisticas(_datos)
                                 .Values
                                 .OrderByDescending(x => x.SumaValores)
                                 .ToList();
        if (stats.Count == 0) return;

        string tipo = cmbTipoGrafica.Text;
        int W = pnlChart.Width;
        int H = pnlChart.Height;

        if (tipo == "Pastel")
            DibujarPastel(g, stats, W, H);
        else
            DibujarBarras(g, stats, W, H, tipo == "Barras");
    }

    private void DibujarBarras(Graphics g, List<EstadisticasCategoria> stats, int W, int H, bool horizontal)
    {
        int mIzq = horizontal ? 165 : 55;
        int mDer = 30;
        int mTop = 45;
        int mBot = horizontal ? 25 : 75;

        int aW = W - mIzq - mDer;
        int aH = H - mTop - mBot;
        if (aW <= 0 || aH <= 0) return;

        double maxVal = stats.Max(s => s.SumaValores);
        if (maxVal <= 0) return;

        g.FillRectangle(new SolidBrush(Color.FromArgb(28, 28, 45)), mIzq, mTop, aW, aH);

        using var fntT = new Font("Segoe UI", 12f, FontStyle.Bold);
        g.DrawString("Valor total por categoría", fntT, new SolidBrush(Color.FromArgb(230, 230, 240)), mIzq, 8);

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
                double gVal = maxVal * gi / 4.0;
                string gStr = gVal >= 1_000_000 ? $"{gVal / 1_000_000:F1}M"
                            : gVal >= 1_000 ? $"{gVal / 1_000:F0}K"
                            : $"{gVal:F0}";
                g.DrawString(gStr, fntL, Brushes.DimGray, mIzq - 50, gy - 7);
            }

            int slot = aW / Math.Max(n, 1);
            int barW = Math.Max(10, slot - 16);

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
                g.DrawString(val, fntV, Brushes.White, x + (barW - vs.Width) / 2, Math.Max(mTop, y - vs.Height - 2));

                string lbl = s.Categoria.Length > 13 ? s.Categoria[..13] + "…" : s.Categoria;
                g.TranslateTransform(x + barW / 2f, mTop + aH + 5);
                g.RotateTransform(38);
                g.DrawString(lbl, fntL, Brushes.LightGray, 0, 0);
                g.ResetTransform();
            }
        }
        else
        {
            using var penGuia = new Pen(Color.FromArgb(50, 50, 70));
            for (int gi = 1; gi <= 4; gi++)
            {
                int gx = mIzq + (int)(aW * gi / 4.0);
                g.DrawLine(penGuia, gx, mTop, gx, mTop + aH);
            }

            int slot = aH / Math.Max(n, 1);
            int barH = Math.Max(8, slot - 10);

            for (int i = 0; i < n; i++)
            {
                var s = stats[i];
                int barW = (int)(s.SumaValores / maxVal * aW);
                int y = mTop + i * slot + (slot - barH) / 2;

                using var br = new SolidBrush(colores[i % colores.Length]);
                g.FillRectangle(br, mIzq, y, barW, barH);

                string lbl = s.Categoria.Length > 20 ? s.Categoria[..20] + "…" : s.Categoria;
                g.DrawString(lbl, fntL, Brushes.LightGray, 2, y + (barH - 14) / 2);

                string val = FormatVal(s.SumaValores);
                g.DrawString(val, fntV, Brushes.White, mIzq + barW + 4, y + (barH - 14) / 2);
            }
        }
    }

    private void DibujarPastel(Graphics g, List<EstadisticasCategoria> stats, int W, int H)
    {
        double total = stats.Sum(s => s.SumaValores);
        if (total <= 0) return;

        int size = Math.Min(W - 280, H - 80);
        size = Math.Max(size, 100);
        int x0 = 30;
        int y0 = (H - size) / 2;

        using var fntT = new Font("Segoe UI", 12f, FontStyle.Bold);
        g.DrawString("Distribución por categoría", fntT, new SolidBrush(Color.FromArgb(230, 230, 240)), x0, 8);

        var colores = PaletaColores();
        float start = -90f;
        using var fntL = new Font("Segoe UI", 8.5f);

        for (int i = 0; i < stats.Count; i++)
        {
            float sweep = (float)(stats[i].SumaValores / total * 360.0);
            using var br = new SolidBrush(colores[i % colores.Length]);
            g.FillPie(br, x0, y0, size, size, start, sweep);
            g.DrawPie(Pens.Black, x0, y0, size, size, start, sweep);

            int lx = x0 + size + 25;
            int ly = y0 + i * 24;
            if (ly + 16 > H) break;
            g.FillRectangle(br, lx, ly + 4, 14, 14);
            string pct = $"{stats[i].SumaValores / total:P1}";
            string lbl = stats[i].Categoria.Length > 20
                ? stats[i].Categoria[..20] + "…" : stats[i].Categoria;
            g.DrawString($"{lbl}  {pct}", fntL, Brushes.LightGray, lx + 18, ly);

            start += sweep;
        }
    }

    private static string FormatVal(double v)
        => v >= 1_000_000 ? $"{v / 1_000_000:F1}M"
         : v >= 1_000 ? $"{v / 1_000:F1}K"
         : $"{v:F0}";

    private static Color[] PaletaColores() => new[]
    {
        Color.FromArgb(0,  200, 255), Color.FromArgb(255, 200,  0),
        Color.FromArgb(0,  255, 128), Color.FromArgb(255,  80, 100),
        Color.FromArgb(180,100, 255), Color.FromArgb(255, 150,  50),
        Color.FromArgb(0,  220, 200), Color.FromArgb(220,  80, 220),
        Color.FromArgb(80, 200,  80), Color.FromArgb(100, 160, 255)
    };

    // ════════════════════════════════════════════════════════════
    //  TAB 5 – PROCESAMIENTO
    // ════════════════════════════════════════════════════════════
    private void BtnDetectarDuplicados_Click(object sender, EventArgs e)
    {
        var dupes = DataProcessor.DetectarDuplicados(_datos);
        BindGridProcesamiento(dupes);
        if (dupes.Count == 0)
            lblProcInfo.Text = "✅ No se encontraron duplicados.";
        else
        {
            lblProcInfo.Text = $"⚠ {dupes.Count} duplicados encontrados.";
            btnEliminarDuplicados.Enabled = true;
        }
        ActualizarEstadoBarra($"Duplicados detectados: {dupes.Count}");
    }

    private void BtnEliminarDuplicados_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show("¿Eliminar duplicados? Esta acción no se puede deshacer.",
            "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        int antes = _datos.Count;
        var limpia = DataProcessor.EliminarDuplicados(_datos);
        _datos.Clear(); _datos.AddRange(limpia);
        ActualizarTodo();
        lblProcInfo.Text = $"✅ Eliminados {antes - _datos.Count} duplicados. Quedan {_datos.Count}.";
        btnEliminarDuplicados.Enabled = false;
        ActualizarEstadoBarra($"Registros actuales: {_datos.Count}");
    }

    private void BtnLinqWhere_Click(object sender, EventArgs e)
    {
        string cat = txtLinqFiltro.Text.Trim();
        // Opera sobre _datosBase para respetar fuentes desmarcadas
        var res = DataProcessor.FiltrarLinq(_datosBase, cat).ToList();
        BindGridProcesamiento(res);
        lblProcInfo.Text = $"LINQ .Where() → {res.Count} resultados para '{cat}'";
    }

    private void BtnLinqGroupBy_Click(object sender, EventArgs e)
    {
        var grupos = DataProcessor.AgruparLinq(_datosBase);
        dgvProcesamiento.Rows.Clear();
        foreach (var g in grupos.OrderByDescending(g => g.Count()))
            AgregarFilaGrid(dgvProcesamiento, new DataItem
            {
                Id = g.Count(),
                Nombre = g.Key,
                Categoria = $"{g.Count()} items",
                Valor = g.Average(x => x.Valor),
                Fuente = "LINQ GroupBy",
                Fecha = DateTime.Now
            });
        lblProcInfo.Text = $"LINQ .GroupBy() → {grupos.Count()} grupos";
    }

    private void BtnLinqOrderBy_Click(object sender, EventArgs e)
    {
        var ordenado = DataProcessor.OrdenarLinq(_datosBase).ToList();
        BindGridProcesamiento(ordenado);
        lblProcInfo.Text = $"LINQ .OrderByDescending(Valor) → {ordenado.Count} registros";
    }

    // ════════════════════════════════════════════════════════════
    //  ACTUALIZACIÓN GLOBAL
    // ════════════════════════════════════════════════════════════
    private void ActualizarTodo()
    {
        _porCategoria = DataProcessor.AgruparPorCategoria(_datos);
        _porId = DataProcessor.IndexarPorId(_datos);

        // Reconstruir _datosBase respetando el estado actual de los checkboxes
        _datosBase = GetDatosBase();
        _datosVista = new List<DataItem>(_datosBase);

        BindGridTodos(_datosVista);
        ActualizarListaCategorias();
        ActualizarTabEstadisticas();
        ActualizarFuentesCheckedList();
        pnlChart.Invalidate();

        lblTotalRegistros.Text = $"Total registros: {_datos.Count}";
        lblTotalCategorias.Text = $"Categorías: {_porCategoria.Count}";
        lblTotalFuentes.Text = $"Fuentes: {_datos.Select(d => d.Fuente).Distinct().Count()}";
    }

    /// <summary>
    /// Devuelve los datos filtrados según los checkboxes de fuente activos.
    /// Si no hay ninguno seleccionado (o todos están seleccionados), devuelve todos.
    /// </summary>
    private List<DataItem> GetDatosBase()
    {
        if (clbFuentes.Items.Count == 0) return new List<DataItem>(_datos);

        var seleccionadas = clbFuentes.CheckedItems
                                      .Cast<string>()
                                      .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Si ninguna está marcada, mostramos todo para no quedarnos con vista vacía
        if (seleccionadas.Count == 0) return new List<DataItem>(_datos);

        return _datos.Where(d => seleccionadas.Contains(d.Fuente)).ToList();
    }

    private void ActualizarListaCategorias()
    {
        lstCategorias.Items.Clear();
        foreach (var cat in _porCategoria.Keys.OrderBy(k => k))
            lstCategorias.Items.Add(cat);
    }

    private void ActualizarFuentesCheckedList()
    {
        // Recordar cuáles estaban marcadas antes de recargar
        var prevSeleccionadas = clbFuentes.CheckedItems
                                          .Cast<string>()
                                          .ToHashSet(StringComparer.OrdinalIgnoreCase);

        clbFuentes.ItemCheck -= ClbFuentes_ItemCheck!;  // evitar disparos durante repoblado
        clbFuentes.Items.Clear();

        foreach (var f in _datos.Select(d => d.Fuente).Distinct().OrderBy(f => f))
        {
            // Si ya había algo seleccionado, mantener estado; si es nueva fuente → marcar
            bool marcada = prevSeleccionadas.Count == 0 || prevSeleccionadas.Contains(f);
            clbFuentes.Items.Add(f, marcada);
        }

        clbFuentes.ItemCheck += ClbFuentes_ItemCheck!;
    }

    private void ClbFuentes_ItemCheck(object sender, ItemCheckEventArgs e)
    {
        // BeginInvoke para que el CheckedItems ya tenga el nuevo estado
        BeginInvoke(() =>
        {
            _datosBase = GetDatosBase();
            _datosVista = new List<DataItem>(_datosBase);
            BindGridTodos(_datosVista);

            var nombresActivos = clbFuentes.CheckedItems.Cast<string>().ToList();
            ActualizarEstadoBarra(
                $"Mostrando {_datosVista.Count} registros de: {string.Join(", ", nombresActivos)}");
        });
    }

    // ════════════════════════════════════════════════════════════
    //  BINDING DE GRIDS
    // ════════════════════════════════════════════════════════════
    private void BindGridTodos(List<DataItem> lista)
    {
        dgvTodos.Rows.Clear();
        foreach (var item in lista) AgregarFilaGrid(dgvTodos, item);
        lblContadorTodos.Text = $"{lista.Count} registros";
    }

    private void BindGridCategoria(List<DataItem> lista)
    {
        dgvCategoria.Rows.Clear();
        foreach (var item in lista) AgregarFilaGrid(dgvCategoria, item);
    }

    private void BindGridProcesamiento(List<DataItem> lista)
    {
        dgvProcesamiento.Rows.Clear();
        foreach (var item in lista) AgregarFilaGrid(dgvProcesamiento, item);
    }

    private static void AgregarFilaGrid(DataGridView dgv, DataItem item)
    {
        int idx = dgv.Rows.Add(
            item.Id, item.Nombre, item.Categoria,
            item.Valor.ToString("F2"), item.Fecha.ToString("yyyy-MM-dd"), item.Fuente);

        dgv.Rows[idx].DefaultCellStyle.BackColor = item.Fuente switch
        {
            "json" => Color.FromArgb(20, 55, 20),
            "csv" => Color.FromArgb(55, 48, 10),
            "xml" => Color.FromArgb(10, 38, 65),
            "txt" => Color.FromArgb(48, 18, 58),
            "postgresql" => Color.FromArgb(10, 28, 75),
            "mariadb" => Color.FromArgb(58, 28, 10),
            _ => Color.FromArgb(38, 38, 38)
        };
        dgv.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(230, 230, 240);
    }

    // ════════════════════════════════════════════════════════════
    //  CONFIGURACIÓN DE GRIDS
    // ════════════════════════════════════════════════════════════
    private void ConfigurarDataGridViews()
    {
        var columnDefs = new (string Header, int Width, DataGridViewAutoSizeColumnMode Auto)[]
        {
            ("ID",        55,  DataGridViewAutoSizeColumnMode.None),
            ("Nombre",    0,   DataGridViewAutoSizeColumnMode.Fill),
            ("Categoría", 130, DataGridViewAutoSizeColumnMode.None),
            ("Valor",     90,  DataGridViewAutoSizeColumnMode.None),
            ("Fecha",     100, DataGridViewAutoSizeColumnMode.None),
            ("Fuente",    90,  DataGridViewAutoSizeColumnMode.None),
        };

        foreach (var dgv in new[] { dgvTodos, dgvCategoria, dgvProcesamiento })
        {
            dgv.Columns.Clear();
            foreach (var (header, width, auto) in columnDefs)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    HeaderText = header,
                    ReadOnly = true,
                    AutoSizeMode = auto,
                    MinimumWidth = 55
                };
                if (auto == DataGridViewAutoSizeColumnMode.None) col.Width = width;
                dgv.Columns.Add(col);
            }
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
            var col = new DataGridViewTextBoxColumn { HeaderText = h, ReadOnly = true, AutoSizeMode = a, MinimumWidth = 55 };
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
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.RowHeadersVisible = false;
        dgv.RowTemplate.Height = 24;
        dgv.ScrollBars = ScrollBars.Both;
    }

    // ════════════════════════════════════════════════════════════
    //  UI HELPERS
    // ════════════════════════════════════════════════════════════
    private void ActualizarEstadoBarra(string mensaje)
    {
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

        dgvTodos.Rows.Clear(); dgvCategoria.Rows.Clear();
        dgvEstadisticas.Rows.Clear(); dgvProcesamiento.Rows.Clear();
        lstCategorias.Items.Clear(); clbFuentes.Items.Clear();
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