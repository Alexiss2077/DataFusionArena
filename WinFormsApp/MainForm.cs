using DataFusionArena.Shared.Models;
using DataFusionArena.Shared.Readers;
using DataFusionArena.Shared.Database;
using DataFusionArena.Shared.Processing;
using System.Data;
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
        ("ID","id"), ("Nombre","nombre"), ("Categoría","categoria"),
        ("Valor","valor"), ("Fecha","fecha"), ("Fuente","fuente")
    };

    private string _ultimoTipoCargado = "";

    private static readonly List<(string Display, string Clave)> _colsDefault = new()
    {
        ("ID","id"), ("Nombre","nombre"), ("Categoría","categoria"),
        ("Valor","valor"), ("Fecha","fecha"), ("Fuente","fuente")
    };

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
    //  HELPERS DE COLUMNAS / COMBOBOXES
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

        bool usarCsv = _ultimoTipoCargado == "csv" && CsvDataReader.UltimasColumnas.Count > 0;
        bool usarJson = _ultimoTipoCargado == "json" && JsonDataReader.UltimasColumnas.Count > 0;
        bool usarXml = _ultimoTipoCargado == "xml" && XmlDataReader.UltimasColumnas.Count > 0;
        bool usarTxt = _ultimoTipoCargado == "txt" && TxtDataReader.UltimasColumnas.Count > 0;
        bool usarPg = _ultimoTipoCargado == "postgresql" && (_lastPgConnector?.UltimasColumnas.Count ?? 0) > 0;
        bool usarMd = _ultimoTipoCargado == "mariadb" && (_lastMdConnector?.UltimasColumnas.Count ?? 0) > 0;

        if (usarCsv)
            BuildInfoColumnasFromReader(CsvDataReader.UltimasColumnas, CsvDataReader.MapeoColumnas);
        else if (usarJson)
            BuildInfoColumnasFromReader(JsonDataReader.UltimasColumnas, JsonDataReader.MapeoColumnas);
        else if (usarXml)
            BuildInfoColumnasFromReader(XmlDataReader.UltimasColumnas, XmlDataReader.MapeoColumnas);
        else if (usarTxt)
            BuildInfoColumnasFromReader(TxtDataReader.UltimasColumnas, TxtDataReader.MapeoColumnas);
        else if (usarPg)
            BuildInfoColumnasFromConnector(_lastPgConnector!.UltimasColumnas, _lastPgConnector.MapeoColumnas);
        else if (usarMd)
            BuildInfoColumnasFromConnector(_lastMdConnector!.UltimasColumnas, _lastMdConnector.MapeoColumnas);
        else
        {
            foreach (var col in _colsDefault)
                _infoColumnas.Add(col);
            foreach (var k in _datos
                .SelectMany(d => d.CamposExtra.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k))
                _infoColumnas.Add((k, k.ToLower()));
        }
    }

    private void BuildInfoColumnasFromReader(
        List<string> columnas, Dictionary<string, string> mapeo)
    {
        var yaAgregadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in columnas)
        {
            if (mapeo.TryGetValue(col, out var prop))
            {
                string propLow = prop.ToLower();
                _infoColumnas.Add(propLow switch
                {
                    "id" => (col, "id"),
                    "nombre" => (col, "nombre"),
                    "categoria" => (col, "categoria"),
                    "valor" => (col, "valor"),
                    "fecha" => (col, "fecha"),
                    _ => (col, col.ToLowerInvariant())
                });
            }
            else
                _infoColumnas.Add((col, col.ToLowerInvariant()));
            yaAgregadas.Add(col.ToLowerInvariant());
        }
        if (!yaAgregadas.Contains("fuente"))
            _infoColumnas.Add(("Fuente", "fuente"));
        foreach (var k in _datos
            .SelectMany(d => d.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(k => !yaAgregadas.Contains(k.ToLowerInvariant()))
            .OrderBy(k => k))
        {
            _infoColumnas.Add((k, k.ToLowerInvariant()));
            yaAgregadas.Add(k.ToLowerInvariant());
        }
    }

    private void BuildInfoColumnasFromConnector(
        List<string> columnas, Dictionary<string, string> mapeo)
    {
        foreach (var col in columnas)
        {
            if (mapeo.TryGetValue(col, out var prop))
                _infoColumnas.Add((col, prop.ToLower()));
            else
                _infoColumnas.Add((col, col.ToLowerInvariant()));
        }
        var yaExisten = new HashSet<string>(
            _infoColumnas.Select(c => c.Clave), StringComparer.OrdinalIgnoreCase);
        if (!yaExisten.Contains("fuente"))
            _infoColumnas.Add(("Fuente", "fuente"));
        foreach (var k in _datos
            .SelectMany(d => d.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(k => !yaExisten.Contains(k))
            .OrderBy(k => k))
        {
            _infoColumnas.Add((k, k.ToLowerInvariant()));
            yaExisten.Add(k);
        }
    }

    private void RefrescarComboboxes()
    {
        var items = _infoColumnas.Select(c => c.Display).Distinct().ToArray<object>();

        string prevFiltro = cmbCampoBusqueda.Text;
        cmbCampoBusqueda.Items.Clear();
        cmbCampoBusqueda.Items.AddRange(items);
        int idxF = cmbCampoBusqueda.FindStringExact(prevFiltro);
        cmbCampoBusqueda.SelectedIndex = idxF >= 0 ? idxF : 0;

        string prevOrden = cmbCampoOrden.Text;
        cmbCampoOrden.Items.Clear();
        cmbCampoOrden.Items.AddRange(items);
        int idxO = cmbCampoOrden.FindStringExact(prevOrden);
        if (idxO < 0) idxO = _infoColumnas.FindIndex(c => c.Clave == "valor");
        cmbCampoOrden.SelectedIndex = Math.Max(0, idxO);

        if (cmbLinqCampo != null)
        {
            string prevLinq = cmbLinqCampo.Text;
            cmbLinqCampo.Items.Clear();
            cmbLinqCampo.Items.AddRange(items);
            int idxL = cmbLinqCampo.FindStringExact(prevLinq);
            cmbLinqCampo.SelectedIndex = Math.Max(0, idxL >= 0 ? idxL : 0);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  GRÁFICA  — agrupa por item.Categoria tal como quedó mapeado
    // ══════════════════════════════════════════════════════════════

    private void ActualizarChart()
    {
        try
        {
            var fuente = (_datosBase.Count > 0) ? _datosBase
                       : (_datos.Count > 0) ? _datos
                       : null;

            if (fuente == null || fuente.Count == 0)
            { chartMain.Limpiar(); return; }

            var agrupado = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in fuente)
            {
                string cat = string.IsNullOrWhiteSpace(item.Categoria) ? "Sin categoría" : item.Categoria;
                if (!agrupado.ContainsKey(cat)) agrupado[cat] = 0;
                agrupado[cat] += item.Valor;
            }

            var data = agrupado
                .OrderByDescending(kv => kv.Value)
                .Take(10)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();

            if (data.Count == 0) { chartMain.Limpiar(); return; }

            var tipo = cmbTipoGrafica.Text switch
            {
                "Barras" => TipoGrafica.Barras,
                "Pastel" => TipoGrafica.Pastel,
                _ => TipoGrafica.Columnas
            };

            string titulo = tipo switch
            {
                TipoGrafica.Columnas => "Valor Total por Categoría – Columnas",
                TipoGrafica.Barras => "Valor Total por Categoría – Barras",
                TipoGrafica.Pastel => "Distribución por Categoría – Pastel",
                _ => ""
            };

            chartMain.SetData(data, tipo, titulo);
        }
        catch (Exception ex)
        {
            chartMain.Limpiar();
            ActualizarEstadoBarra($"⚠ Error al renderizar gráfica: {ex.Message}");
        }
    }

    private void BtnActualizarGrafica_Click(object sender, EventArgs e) => ActualizarChart();
    private void CmbTipoGrafica_SelectedIndexChanged(object sender, EventArgs e) => ActualizarChart();

    // ══════════════════════════════════════════════════════════════
    //  CARGA DE ARCHIVOS
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
        await CargarArchivoAsync(Path.Combine(_dirDatos, "products.json"), "json", silencioso: true);
        await CargarArchivoAsync(Path.Combine(_dirDatos, "sales.csv"), "csv", silencioso: true);
        await CargarArchivoAsync(Path.Combine(_dirDatos, "employees.xml"), "xml", silencioso: true);
        await CargarArchivoAsync(Path.Combine(_dirDatos, "records.txt"), "txt", silencioso: true);
        _ultimoTipoCargado = "";
        ReconstruirInfoColumnas();
        RefrescarComboboxes();
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
            switch (tipo)
            {
                case "json": return JsonDataReader.Leer(ruta);
                case "csv": return CsvDataReader.Leer(ruta);
                case "xml":
                    var items = XmlDataReader.Leer(ruta);
                    foreach (var item in items)
                    {
                        if (item.CamposExtra.TryGetValue("departamento", out var dep))
                        { item.Categoria = dep; item.CamposExtra.Remove("departamento"); }
                        if (item.CamposExtra.TryGetValue("salario", out var sal) &&
                            double.TryParse(sal, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out double sv))
                        { item.Valor = sv; item.CamposExtra.Remove("salario"); }
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
    //  CONEXIÓN A BD — ahora con diálogo de selección de columnas
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
            pg.ProbarConexion(out string msgErr);
            MessageBox.Show($"Error:\n{msgErr}", "PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ActualizarEstadoBarra("❌ Error de conexión PostgreSQL.");
            return;
        }

        pg.ProbarConexion(out string msg);

        // Obtener columnas y mostrar diálogo de selección
        var colsDisponibles = await Task.Run(() => pg.ObtenerNombresColumnas());
        using var dlgCols = new FormSeleccionColumnas(colsDisponibles, pg.MapeoColumnas);
        if (dlgCols.ShowDialog() != DialogResult.OK) return;

        // Aplicar mapeo elegido por el usuario
        pg.SobreescribirMapeo(dlgCols.ColCategoria, dlgCols.ColValor,
                              dlgCols.ColNombre, dlgCols.ColFecha);

        ActualizarEstadoBarra("⏳ Cargando datos PostgreSQL...");
        var datos = await Task.Run(() => pg.LeerDatos());

        _lastPgConnector = pg;
        _ultimoTipoCargado = "postgresql";
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

        var colsDisponibles = await Task.Run(() => md.ObtenerNombresColumnas());
        using var dlgCols = new FormSeleccionColumnas(colsDisponibles, md.MapeoColumnas);
        if (dlgCols.ShowDialog() != DialogResult.OK) return;

        md.SobreescribirMapeo(dlgCols.ColCategoria, dlgCols.ColValor,
                              dlgCols.ColNombre, dlgCols.ColFecha);

        ActualizarEstadoBarra("⏳ Cargando datos MariaDB...");
        var datos = await Task.Run(() => md.LeerDatos());

        _lastMdConnector = md;
        _ultimoTipoCargado = "mariadb";
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
        _ultimoTipoCargado = (_lastPgConnector != null && _lastMdConnector == null) ? "postgresql"
                           : (_lastMdConnector != null && _lastPgConnector == null) ? "mariadb" : "";
        await ActualizarTodoAsync();
        ActualizarEstadoBarra($"✅ Datos actualizados. Total: {_datos.Count} registros.");
    }

    // ══════════════════════════════════════════════════════════════
    //  FILTRAR / ORDENAR
    // ══════════════════════════════════════════════════════════════

    private async void BtnFiltrar_Click(object? sender, EventArgs e)
    {
        string display = cmbCampoBusqueda.Text;
        string clave = TraducirClave(display);
        string valor = txtBusqueda.Text.Trim();
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
        ActualizarEstadoBarra($"Filtro limpiado. {_datosVista.Count} registros visibles.");
    }

    private async void BtnOrdenar_Click(object? sender, EventArgs e)
    {
        string display = cmbCampoOrden.Text;
        string clave = TraducirClave(display);
        bool asc = rbAscendente.Checked;
        ActualizarEstadoBarra("⏳ Ordenando...");
        var ordenado = await Task.Run(() => DataProcessor.Ordenar(_datosVista, clave, asc));
        _datosVista = ordenado;
        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        ActualizarEstadoBarra($"Ordenado por '{display}' {(asc ? "↑" : "↓")}. {ordenado.Count} registros.");
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
            var statCols = new (string H, int W, DataGridViewAutoSizeColumnMode A)[]
            {
                ("Categoría",0,DataGridViewAutoSizeColumnMode.Fill),
                ("Cant.",65,DataGridViewAutoSizeColumnMode.None),
                ("Promedio",100,DataGridViewAutoSizeColumnMode.None),
                ("Máximo",100,DataGridViewAutoSizeColumnMode.None),
                ("Mínimo",100,DataGridViewAutoSizeColumnMode.None),
                ("Total",120,DataGridViewAutoSizeColumnMode.None),
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
            dgvEstadisticas.Rows.Add(s.Categoria, s.Cantidad,
                s.Promedio.ToString("F2"), s.ValorMaximo.ToString("F2"),
                s.ValorMinimo.ToString("F2"), s.SumaValores.ToString("N2"));
        lblTotalRegistros.Text = $"Total registros: {_datos.Count}";
        lblTotalCategorias.Text = $"Categorías: {stats.Count}";
        lblTotalFuentes.Text = $"Fuentes activas: {_datosBase.Select(d => d.Fuente).Distinct().Count()}";
    }

    // ══════════════════════════════════════════════════════════════
    //  PROCESAMIENTO / DUPLICADOS / LINQ
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

    private static string Normalizar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        var formD = texto.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(formD.Length);
        foreach (char c in formD)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }
    private static bool ContieneNorm(string texto, string busqueda)
        => Normalizar(texto).Contains(Normalizar(busqueda));

    private async void BtnLinqWhere_Click(object? sender, EventArgs e)
    {
        string busqueda = txtLinqFiltro.Text.Trim();
        string display = cmbLinqCampo?.Text ?? "";
        string clave = TraducirClave(display);
        if (string.IsNullOrEmpty(busqueda))
        { lblProcInfo.Text = "⚠  Escribe un término antes de usar .Where()"; return; }
        var res = await Task.Run(() =>
            _datosBase.Where(d => clave switch
            {
                "nombre" => ContieneNorm(d.Nombre, busqueda),
                "fuente" => ContieneNorm(d.Fuente, busqueda),
                "id" => int.TryParse(busqueda, out int n) ? d.Id == n : d.Id.ToString().Contains(busqueda),
                "categoria" => ContieneNorm(d.Categoria, busqueda),
                "valor" => d.Valor.ToString("F2").Contains(busqueda),
                "fecha" => d.Fecha.ToString("yyyy-MM-dd").Contains(busqueda),
                _ => d.CamposExtra.TryGetValue(clave, out var ev) ? ContieneNorm(ev, busqueda) : false
            }).ToList());
        await BindGridAsync(dgvProcesamiento, res, null);
        lblProcInfo.Text = res.Count > 0
            ? $"✅ LINQ .Where({display}.Contains(\"{busqueda}\")) → {res.Count} registro(s)"
            : $"⚠  LINQ .Where({display}.Contains(\"{busqueda}\")) → Sin resultados";
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
        lblProcInfo.Text = $"✅ LINQ .GroupBy(Categoría) → {grupos.Count} grupo(s)";
        ActualizarEstadoBarra($"LINQ .GroupBy(): {grupos.Count} grupos.");
    }

    private async void BtnLinqOrderBy_Click(object? sender, EventArgs e)
    {
        var ordenado = await Task.Run(() => DataProcessor.OrdenarLinq(_datosBase).ToList());
        await BindGridAsync(dgvProcesamiento, ordenado, null);
        lblProcInfo.Text = $"✅ LINQ .OrderByDescending(Valor).ThenBy(Nombre) → {ordenado.Count} registros";
        ActualizarEstadoBarra($"LINQ .OrderBy(): {ordenado.Count} registros.");
    }

    private void BtnLinqLimpiar_Click(object? sender, EventArgs e)
    {
        txtLinqFiltro.Text = ""; dgvProcesamiento.DataSource = null;
        dgvProcesamiento.Columns.Clear(); lblProcInfo.Text = "Resultados limpiados.";
        btnEliminarDuplicados.Enabled = false;
        ActualizarEstadoBarra("Grid de procesamiento limpiado.");
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
        RefrescarComboboxes();
        ReconstruirCategorias();
        await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
        await ActualizarTabEstadisticasAsync();
        ActualizarChart();
        lblTotalRegistros.Text = $"Total registros: {_datos.Count}";
        lblTotalCategorias.Text = $"Categorías: {_porCategoria.Count}";
        lblTotalFuentes.Text = $"Fuentes: {_datos.Select(d => d.Fuente).Distinct().Count()}";
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
        var sel = clbFuentes.CheckedItems.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (sel.Count == 0) return new List<DataItem>(_datos);
        return _datos.Where(d => sel.Contains(d.Fuente)).ToList();
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
            bool marcada = esNueva || prevSel.Count == 0 || prevSel.Contains(f);
            clbFuentes.Items.Add(f, marcada);
        }
        clbFuentes.ItemCheck += ClbFuentes_ItemCheck!;
    }

    private async void ClbFuentes_ItemCheck(object sender, ItemCheckEventArgs e)
    {
        BeginInvoke(async () =>
        {
            _datosBase = GetDatosBase(); _datosVista = new List<DataItem>(_datosBase);
            ReconstruirCategorias();
            await BindGridAsync(dgvTodos, _datosVista, lblContadorTodos);
            await ActualizarTabEstadisticasAsync();
            ActualizarChart();
            var activos = clbFuentes.CheckedItems.Cast<string>().ToList();
            ActualizarEstadoBarra($"Mostrando {_datosVista.Count} registros de: {string.Join(", ", activos)}");
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
        var dt = await Task.Run(() => BuildDataTable(itemsDisplay, colInfos));
        if (dgv.InvokeRequired)
            dgv.Invoke(() => AplicarDataTable(dgv, dt, items.Count, limitado, contadorLabel, colInfos));
        else
            AplicarDataTable(dgv, dt, items.Count, limitado, contadorLabel, colInfos);
    }

    private void AplicarDataTable(DataGridView dgv, DataTable dt, int totalReal, bool limitado,
        Label? contadorLabel, List<(string Display, string Clave)> colInfos)
    {
        dgv.DataSource = null; dgv.Columns.Clear(); dgv.AutoGenerateColumns = false;
        var claveMap = colInfos.ToDictionary(c => c.Display, c => c.Clave, StringComparer.OrdinalIgnoreCase);
        string? nombreDisplay = colInfos.FirstOrDefault(c => c.Clave == "nombre").Display;
        foreach (DataColumn col in dt.Columns)
        {
            string clave = claveMap.TryGetValue(col.ColumnName, out var cv) ? cv : col.ColumnName.ToLower();
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
            dgv.Columns.Add(dgvCol);
        }
        AplicarEstiloGrid(dgv);
        dgv.DataSource = dt;
        dgv.CellFormatting -= DgvCellFormatting!;
        dgv.CellFormatting += DgvCellFormatting!;
        if (contadorLabel != null)
            contadorLabel.Text = limitado
                ? $"⚠ Mostrando {DISPLAY_LIMIT:N0} de {totalReal:N0}" : $"{totalReal:N0} registros";
    }

    private static void DgvCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var dgv = (DataGridView)sender;
        if (!dgv.Columns.Contains("Fuente")) return;
        try
        {
            var bg = dgv.Rows[e.RowIndex].Cells["Fuente"].Value?.ToString() switch
            {
                "json" => "Color.FromArgb(18,50,18)" is var _ ? Color.FromArgb(18, 50, 18) : Color.Black,
                _ => Color.FromArgb(32, 32, 48)
            };
            bg = dgv.Rows[e.RowIndex].Cells["Fuente"].Value?.ToString() switch
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
        catch { }
    }

    private static DataTable BuildDataTable(
        List<DataItem> items, List<(string Display, string Clave)> colInfos)
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
                row[display] = clave switch
                {
                    "id" => (object)item.Id,
                    "nombre" => item.Nombre,
                    "categoria" => item.Categoria,
                    "valor" => (object)item.Valor,
                    "fecha" => item.Fecha.ToString("yyyy-MM-dd"),
                    "fuente" => item.Fuente,
                    _ => BuscarEnCamposExtra(item, clave)
                };
            }
            dt.Rows.Add(row);
        }
        dt.EndLoadData();
        return dt;
    }

    private static string BuscarEnCamposExtra(DataItem item, string clave)
    {
        if (item.CamposExtra.TryGetValue(clave, out var v)) return v;
        foreach (var kv in item.CamposExtra)
            if (string.Equals(kv.Key, clave, StringComparison.OrdinalIgnoreCase)) return kv.Value;
        return "";
    }

    private void ConfigurarDataGridViews()
    {
        foreach (var dgv in new[] { dgvTodos, dgvCategoria, dgvProcesamiento })
        { dgv.AutoGenerateColumns = false; AplicarEstiloGrid(dgv); }
        dgvEstadisticas.Columns.Clear();
        var sc = new (string H, int W, DataGridViewAutoSizeColumnMode A)[]
        {
            ("Categoría",0,DataGridViewAutoSizeColumnMode.Fill),
            ("Cant.",65,DataGridViewAutoSizeColumnMode.None),
            ("Promedio",100,DataGridViewAutoSizeColumnMode.None),
            ("Máximo",100,DataGridViewAutoSizeColumnMode.None),
            ("Mínimo",100,DataGridViewAutoSizeColumnMode.None),
            ("Total",120,DataGridViewAutoSizeColumnMode.None),
        };
        foreach (var (h, w, a) in sc)
        {
            var col = new DataGridViewTextBoxColumn { HeaderText = h, ReadOnly = true, AutoSizeMode = a, MinimumWidth = 55 };
            if (a == DataGridViewAutoSizeColumnMode.None) col.Width = w;
            dgvEstadisticas.Columns.Add(col);
        }
        AplicarEstiloGrid(dgvEstadisticas);
    }

    private static void AplicarEstiloGrid(DataGridView dgv)
    {
        dgv.BackgroundColor = Color.FromArgb(18, 18, 28); dgv.GridColor = Color.FromArgb(55, 55, 75);
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
        dgv.ColumnHeadersHeight = 32; dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        dgv.EnableHeadersVisualStyles = false; dgv.AllowUserToAddRows = false;
        dgv.AllowUserToResizeRows = false; dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.RowHeadersVisible = false; dgv.RowTemplate.Height = 24; dgv.ScrollBars = ScrollBars.Both;
        dgv.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
    }

    private static void AplicarDoubleBuffer(DataGridView dgv) =>
        typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(dgv, true);

    private void ActualizarEstadoBarra(string mensaje)
    {
        if (lblStatus.GetCurrentParent()?.InvokeRequired == true)
            lblStatus.GetCurrentParent().Invoke(() => lblStatus.Text = mensaje);
        else lblStatus.Text = mensaje;
        Application.DoEvents();
    }

    private void MenuLimpiarDatos_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show("¿Limpiar todos los datos en memoria?", "Confirmar",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _datos.Clear(); _porCategoria.Clear(); _porId.Clear();
        _datosBase.Clear(); _datosVista.Clear();
        _lastPgConnector = null; _lastMdConnector = null; _ultimoTipoCargado = "";
        foreach (var dgv in new[] { dgvTodos, dgvCategoria, dgvProcesamiento })
        { dgv.DataSource = null; dgv.Columns.Clear(); }
        dgvEstadisticas.Rows.Clear(); lstCategorias.Items.Clear(); clbFuentes.Items.Clear();
        txtLinqFiltro.Text = ""; lblProcInfo.Text = "Selecciona una operación.";
        btnEliminarDuplicados.Enabled = false; chartMain.Limpiar(); lblContadorTodos.Text = "0 registros";
        _infoColumnas.Clear();
        foreach (var col in _colsDefault) _infoColumnas.Add(col);
        RefrescarComboboxes();
        ActualizarEstadoBarra("Datos limpiados.");
    }

    private void MenuAcercaDe_Click(object sender, EventArgs e) =>
        MessageBox.Show("Data Fusion Arena\nAdministración y Organización de Datos\n\n" +
            "Ingeniería · 4.º Semestre · C# .NET 10 · WinForms\n\n" +
            "Fuentes: JSON · CSV · XML · TXT · PostgreSQL · MariaDB\n" +
            "Estructuras: List<T> · Dictionary<TKey,TValue>\n" +
            "Gráficas: GDI+ propio (sin dependencias externas)",
            "Acerca de", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void MenuSalir_Click(object sender, EventArgs e) => Close();
}

// ══════════════════════════════════════════════════════════════
//  DIÁLOGO 1: Conexión BD (campos individuales)
// ══════════════════════════════════════════════════════════════

public class FormConexionBD : Form
{
    public string CadenaConexion { get; private set; } = "";
    public string NombreTabla { get; private set; } = "";
    private readonly TextBox txtHost, txtPuerto, txtBD, txtUsuario, txtContrasena, txtTabla;

    public FormConexionBD(string motor)
    {
        Text = $"Conexión a {motor}"; Size = new Size(460, 340);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        BackColor = Color.FromArgb(30, 30, 45); ForeColor = Color.White;

        bool esPg = motor == "PostgreSQL";
        string pd = esPg ? "5432" : "3306", ud = esPg ? "postgres" : "root";
        int y = 18, tx = 130, tw = 295;

        Label Lbl(string t) => new() { Text = t, AutoSize = true, ForeColor = Color.FromArgb(0, 200, 220) };
        TextBox Txt(string d, bool p = false) => new()
        {
            Width = tw,
            Text = d,
            BackColor = Color.FromArgb(45, 45, 65),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = p
        };

        var l1 = Lbl("Host:"); l1.Location = new Point(15, y + 3); txtHost = Txt("localhost"); txtHost.Location = new Point(tx, y); y += 35;
        var l2 = Lbl("Puerto:"); l2.Location = new Point(15, y + 3); txtPuerto = Txt(pd); txtPuerto.Location = new Point(tx, y); y += 35;
        var l3 = Lbl("Base de datos:"); l3.Location = new Point(15, y + 3); txtBD = Txt(""); txtBD.Location = new Point(tx, y); y += 35;
        var l4 = Lbl("Usuario:"); l4.Location = new Point(15, y + 3); txtUsuario = Txt(ud); txtUsuario.Location = new Point(tx, y); y += 35;
        var l5 = Lbl("Contraseña:"); l5.Location = new Point(15, y + 3); txtContrasena = Txt("", true); txtContrasena.Location = new Point(tx, y); y += 35;
        var l6 = Lbl("Tabla:"); l6.Location = new Point(15, y + 3); txtTabla = Txt(""); txtTabla.Location = new Point(tx, y); y += 45;

        var ok = new Button
        {
            Text = "Conectar",
            Location = new Point(260, y),
            Width = 80,
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        var can = new Button
        {
            Text = "Cancelar",
            Location = new Point(355, y),
            Width = 80,
            DialogResult = DialogResult.Cancel,
            BackColor = Color.FromArgb(80, 30, 30),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        ok.Click += (_, _) => {
            string h = string.IsNullOrWhiteSpace(txtHost.Text) ? "localhost" : txtHost.Text.Trim();
            string p = string.IsNullOrWhiteSpace(txtPuerto.Text) ? pd : txtPuerto.Text.Trim();
            string db = txtBD.Text.Trim(), user = string.IsNullOrWhiteSpace(txtUsuario.Text) ? ud : txtUsuario.Text.Trim();
            string pass = txtContrasena.Text;
            CadenaConexion = esPg ? $"Host={h};Port={p};Database={db};Username={user};Password={pass};"
                               : $"Server={h};Port={p};Database={db};User={user};Password={pass};";
            NombreTabla = txtTabla.Text.Trim();
        };
        Controls.AddRange(new Control[] { l1, txtHost, l2, txtPuerto, l3, txtBD, l4, txtUsuario, l5, txtContrasena, l6, txtTabla, ok, can });
        AcceptButton = ok; CancelButton = can;
    }
}

// ══════════════════════════════════════════════════════════════
//  DIÁLOGO 2: El usuario elige qué columna es categoría,
//  cuál es el valor numérico, nombre y fecha.
//  Esto garantiza que la gráfica siempre tenga sentido.
// ══════════════════════════════════════════════════════════════

public class FormSeleccionColumnas : Form
{
    public string ColCategoria { get; private set; } = "";
    public string ColValor { get; private set; } = "";
    public string ColNombre { get; private set; } = "";
    public string ColFecha { get; private set; } = "";

    public FormSeleccionColumnas(List<string> columnas, Dictionary<string, string> mapeoActual)
    {
        Text = "Mapeo de columnas para análisis"; Size = new Size(460, 300);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        BackColor = Color.FromArgb(30, 30, 45); ForeColor = Color.White;

        var opciones = new List<string> { "(ninguna)" }; opciones.AddRange(columnas);
        var arr = opciones.ToArray<object>();

        string sC = mapeoActual.FirstOrDefault(kv => kv.Value == "categoria").Key ?? "";
        string sV = mapeoActual.FirstOrDefault(kv => kv.Value == "valor").Key ?? "";
        string sN = mapeoActual.FirstOrDefault(kv => kv.Value == "nombre").Key ?? "";
        string sF = mapeoActual.FirstOrDefault(kv => kv.Value == "fecha").Key ?? "";

        int y = 15, lx = 15, cx = 210, lw = 190, cw = 215;

        Label Lbl(string t, string s) => new()
        {
            Text = s.Length > 0 ? $"{t}  (sugerido: {s})" : t,
            Location = new Point(lx, y),
            Size = new Size(lw, 36),
            ForeColor = Color.FromArgb(0, 200, 220),
            Font = new Font("Segoe UI", 8.5f)
        };

        ComboBox Cmb(string s)
        {
            var c = new ComboBox
            {
                Location = new Point(cx, y + 7),
                Width = cw,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 65),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            c.Items.AddRange(arr);
            c.SelectedIndex = s.Length > 0 ? Math.Max(0, opciones.IndexOf(s)) : 0;
            return c;
        }

        var lC = Lbl("📊 Categoría (eje X / agrupación):", sC); var cC = Cmb(sC); y += 42;
        var lV = Lbl("🔢 Valor numérico (eje Y / suma):", sV); var cV = Cmb(sV); y += 42;
        var lN = Lbl("🏷  Nombre / etiqueta:", sN); var cN = Cmb(sN); y += 42;
        var lF = Lbl("📅 Fecha:", sF); var cF = Cmb(sF); y += 50;

        var note = new Label
        {
            Text = "ℹ  Deja en (ninguna) si la columna no aplica para tu tabla.",
            Location = new Point(lx, y),
            Size = new Size(420, 18),
            ForeColor = Color.FromArgb(140, 140, 160),
            Font = new Font("Segoe UI", 8f)
        }; y += 28;

        var ok = new Button
        {
            Text = "✔ Confirmar",
            Location = new Point(235, y),
            Width = 100,
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        var can = new Button
        {
            Text = "Cancelar",
            Location = new Point(345, y),
            Width = 80,
            DialogResult = DialogResult.Cancel,
            BackColor = Color.FromArgb(80, 30, 30),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };

        ok.Click += (_, _) => {
            ColCategoria = cC.Text == "(ninguna)" ? "" : cC.Text;
            ColValor = cV.Text == "(ninguna)" ? "" : cV.Text;
            ColNombre = cN.Text == "(ninguna)" ? "" : cN.Text;
            ColFecha = cF.Text == "(ninguna)" ? "" : cF.Text;
        };

        ClientSize = new Size(445, y + 50);
        Controls.AddRange(new Control[] { lC, cC, lV, cV, lN, cN, lF, cF, note, ok, can });
        AcceptButton = ok; CancelButton = can;
    }
}