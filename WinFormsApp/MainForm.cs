using System.Windows.Forms.DataVisualization.Charting;
using DataFusionArena.Shared.Models;
using DataFusionArena.Shared.Readers;
using DataFusionArena.Shared.Database;
using DataFusionArena.Shared.Processing;

namespace DataFusionArena.WinForms;

public partial class MainForm : Form
{
    // ── Estado global ────────────────────────────────────────────
    private readonly List<DataItem> _datos       = new();
    private List<DataItem>          _datosVista  = new();  // lista filtrada/ordenada actual
    private Dictionary<string, List<DataItem>> _porCategoria = new();
    private Dictionary<int, DataItem>          _porId        = new();

    private readonly string _dirDatos = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "SampleData");

    public MainForm()
    {
        InitializeComponent();
        ConfigurarDataGridViews();
        ConfigurarChart();
        ActualizarEstadoBarra("Listo. Cargue datos usando el menú o los botones.");
        Text = "Data Fusion Arena – Administración y Organización de Datos";
    }

    // ══════════════════════════════════════════════════════════════
    //  TOOLBAR / MENÚ – Carga de archivos
    // ══════════════════════════════════════════════════════════════

    private void BtnCargarJson_Click(object sender, EventArgs e)
        => CargarArchivo(Path.Combine(_dirDatos, "products.json"), "json");

    private void BtnCargarCsv_Click(object sender, EventArgs e)
        => CargarArchivo(Path.Combine(_dirDatos, "sales.csv"), "csv");

    private void BtnCargarXml_Click(object sender, EventArgs e)
        => CargarArchivo(Path.Combine(_dirDatos, "employees.xml"), "xml");

    private void BtnCargarTxt_Click(object sender, EventArgs e)
        => CargarArchivo(Path.Combine(_dirDatos, "records.txt"), "txt");

    private void BtnCargarTodo_Click(object sender, EventArgs e)
    {
        CargarArchivo(Path.Combine(_dirDatos, "products.json"),  "json",  silencioso: true);
        CargarArchivo(Path.Combine(_dirDatos, "sales.csv"),      "csv",   silencioso: true);
        CargarArchivo(Path.Combine(_dirDatos, "employees.xml"),  "xml",   silencioso: true);
        CargarArchivo(Path.Combine(_dirDatos, "records.txt"),    "txt",   silencioso: true);
        ActualizarTodo();
        ActualizarEstadoBarra($"✅ Todos los archivos cargados. Total: {_datos.Count} registros.");
        MessageBox.Show($"Archivos cargados exitosamente.\nTotal de registros: {_datos.Count}",
            "Data Fusion Arena", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void MenuCargarPersonalizado_Click(object sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Selecciona un archivo de datos",
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
            case "json":
                nuevos = JsonDataReader.Leer(ruta);
                break;
            case "csv":
                nuevos = CsvDataReader.Leer(ruta);
                break;
            case "xml":
                nuevos = XmlDataReader.Leer(ruta);
                // Normalizar campos XML de empleados
                foreach (var item in nuevos)
                {
                    if (item.CamposExtra.TryGetValue("departamento", out var dep))
                    { item.Categoria = dep; item.CamposExtra.Remove("departamento"); }
                    if (item.CamposExtra.TryGetValue("salario", out var sal) &&
                        double.TryParse(sal, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double s))
                    { item.Valor = s; item.CamposExtra.Remove("salario"); }
                }
                break;
            case "txt":
                nuevos = TxtDataReader.Leer(ruta);
                break;
            default:
                return;
        }

        DataProcessor.AgregarDatos(_datos, nuevos);
        ActualizarTodo();

        if (!silencioso)
            ActualizarEstadoBarra($"✅ {nuevos.Count} registros cargados desde {Path.GetFileName(ruta)}. Total: {_datos.Count}");
    }

    // ══════════════════════════════════════════════════════════════
    //  BASES DE DATOS
    // ══════════════════════════════════════════════════════════════

    private void BtnConectarPostgres_Click(object sender, EventArgs e)
    {
        using var dlg = new FormConexionBD("PostgreSQL");
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var pg = new PostgreSqlConnector(dlg.CadenaConexion, dlg.NombreTabla);
        ActualizarEstadoBarra("Conectando a PostgreSQL...");

        if (pg.ProbarConexion(out string msg))
        {
            var datos = pg.LeerDatos();
            DataProcessor.AgregarDatos(_datos, datos);
            ActualizarTodo();
            ActualizarEstadoBarra($"✅ PostgreSQL: {datos.Count} registros cargados. {msg}");
        }
        else
        {
            MessageBox.Show($"Error de conexión:\n{msg}", "PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ActualizarEstadoBarra($"❌ Error PostgreSQL: {msg}");
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
            var datos = md.LeerDatos();
            DataProcessor.AgregarDatos(_datos, datos);
            ActualizarTodo();
            ActualizarEstadoBarra($"✅ MariaDB: {datos.Count} registros cargados. {msg}");
        }
        else
        {
            MessageBox.Show($"Error de conexión:\n{msg}", "MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ActualizarEstadoBarra($"❌ Error MariaDB: {msg}");
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  TAB 1 – TODOS LOS DATOS (DataGridView principal)
    // ══════════════════════════════════════════════════════════════

    private void BtnFiltrar_Click(object sender, EventArgs e)
    {
        string campo = cmbCampoBusqueda.Text.ToLower();
        string valor = txtBusqueda.Text.Trim();

        _datosVista = string.IsNullOrEmpty(valor)
            ? new List<DataItem>(_datos)
            : DataProcessor.Filtrar(_datos, campo, valor);

        BindGridTodos(_datosVista);
        ActualizarEstadoBarra($"Filtro '{campo}' = '{valor}' → {_datosVista.Count} resultados.");
    }

    private void BtnLimpiarFiltro_Click(object sender, EventArgs e)
    {
        txtBusqueda.Text = "";
        _datosVista = new List<DataItem>(_datos);
        BindGridTodos(_datosVista);
        ActualizarEstadoBarra($"Filtro limpiado. Mostrando {_datos.Count} registros.");
    }

    private void BtnOrdenar_Click(object sender, EventArgs e)
    {
        string campo = cmbCampoOrden.Text.ToLower();
        bool asc     = rbAscendente.Checked;
        var fuente   = _datosVista.Count > 0 ? _datosVista : _datos;
        var ordenado = DataProcessor.Ordenar(fuente, campo, asc);
        BindGridTodos(ordenado);
        ActualizarEstadoBarra($"Ordenado por '{campo}' {(asc ? "↑ asc" : "↓ desc")}. {ordenado.Count} registros.");
    }

    // ══════════════════════════════════════════════════════════════
    //  TAB 2 – POR CATEGORÍA (Dictionary)
    // ══════════════════════════════════════════════════════════════

    private void LstCategorias_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (lstCategorias.SelectedItem is not string cat) return;
        if (!_porCategoria.TryGetValue(cat, out var lista)) return;

        BindGridCategoria(lista);
        lblCatInfo.Text = $"Categoría: {cat}  |  {lista.Count} registros  |  " +
                          $"Promedio: {lista.Average(x => x.Valor):F2}  |  " +
                          $"Total: {lista.Sum(x => x.Valor):N2}";
    }

    // ══════════════════════════════════════════════════════════════
    //  TAB 3 – ESTADÍSTICAS
    // ══════════════════════════════════════════════════════════════

    private void ActualizarTabEstadisticas()
    {
        if (_datos.Count == 0) return;
        var stats = DataProcessor.CalcularEstadisticas(_datos);

        dgvEstadisticas.Rows.Clear();
        foreach (var s in stats.Values.OrderByDescending(x => x.SumaValores))
        {
            dgvEstadisticas.Rows.Add(
                s.Categoria, s.Cantidad,
                s.Promedio.ToString("F2"),
                s.ValorMaximo.ToString("F2"),
                s.ValorMinimo.ToString("F2"),
                s.SumaValores.ToString("N2")
            );
        }

        // Totales
        lblTotalRegistros.Text  = $"Total registros: {_datos.Count}";
        lblTotalCategorias.Text = $"Categorías: {_porCategoria.Count}";
        lblTotalFuentes.Text    = $"Fuentes: {_datos.Select(d => d.Fuente).Distinct().Count()}";
    }

    // ══════════════════════════════════════════════════════════════
    //  TAB 4 – GRÁFICAS (Chart)
    // ══════════════════════════════════════════════════════════════

    private void BtnActualizarGrafica_Click(object sender, EventArgs e)
        => ActualizarGrafica();

    private void CmbTipoGrafica_SelectedIndexChanged(object sender, EventArgs e)
        => ActualizarGrafica();

    private void ActualizarGrafica()
    {
        if (_datos.Count == 0)
        {
            MessageBox.Show("No hay datos. Cargue archivos primero.", "Sin datos",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        chart1.Series.Clear();
        chart1.Titles.Clear();
        chart1.Titles.Add("Data Fusion Arena – Análisis de Datos");

        var stats = DataProcessor.CalcularEstadisticas(_datos);
        string tipo = cmbTipoGrafica.Text;

        var series = new Series("Datos")
        {
            ChartType = tipo switch
            {
                "Barras"   => SeriesChartType.Bar,
                "Columnas" => SeriesChartType.Column,
                "Pastel"   => SeriesChartType.Pie,
                "Línea"    => SeriesChartType.Line,
                _          => SeriesChartType.Column
            },
            IsValueShownAsLabel = true,
            LabelFormat         = "#,##0"
        };

        // Agrupar por categoría: suma de valores
        foreach (var s in stats.Values.OrderByDescending(x => x.SumaValores))
        {
            int idx = series.Points.AddXY(s.Categoria, s.SumaValores);
            // Color diferente por categoría
            series.Points[idx].Color = ColorPorIndice(idx);
        }

        chart1.Series.Add(series);
        chart1.ChartAreas[0].AxisX.LabelStyle.Angle = -35;
        chart1.ChartAreas[0].AxisY.LabelStyle.Format = "#,##0";
        chart1.ChartAreas[0].BackColor = Color.FromArgb(30, 30, 30);
        chart1.BackColor = Color.FromArgb(22, 22, 35);

        ActualizarEstadoBarra($"Gráfica '{tipo}' actualizada con {stats.Count} categorías.");
    }

    // ══════════════════════════════════════════════════════════════
    //  TAB 5 – PROCESAMIENTO (duplicados, LINQ)
    // ══════════════════════════════════════════════════════════════

    private void BtnDetectarDuplicados_Click(object sender, EventArgs e)
    {
        var dupes = DataProcessor.DetectarDuplicados(_datos);
        BindGridProcesamiento(dupes);

        if (dupes.Count == 0)
            lblProcInfo.Text = "✅ No se encontraron duplicados.";
        else
        {
            lblProcInfo.Text = $"⚠ Se encontraron {dupes.Count} duplicados.";
            btnEliminarDuplicados.Enabled = true;
        }
        ActualizarEstadoBarra($"Duplicados detectados: {dupes.Count}");
    }

    private void BtnEliminarDuplicados_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show($"¿Eliminar duplicados? Esta acción no se puede deshacer.",
            "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        int antes = _datos.Count;
        var limpia = DataProcessor.EliminarDuplicados(_datos);
        _datos.Clear();
        _datos.AddRange(limpia);
        ActualizarTodo();
        lblProcInfo.Text = $"✅ Eliminados {antes - _datos.Count} duplicados. Quedan {_datos.Count} registros.";
        btnEliminarDuplicados.Enabled = false;
        ActualizarEstadoBarra($"Duplicados eliminados. Registros actuales: {_datos.Count}");
    }

    private void BtnLinqWhere_Click(object sender, EventArgs e)
    {
        string cat = txtLinqFiltro.Text.Trim();
        var res     = DataProcessor.FiltrarLinq(_datos, cat).ToList();
        BindGridProcesamiento(res);
        lblProcInfo.Text = $"LINQ .Where() → {res.Count} resultados para '{cat}'";
    }

    private void BtnLinqGroupBy_Click(object sender, EventArgs e)
    {
        var grupos = DataProcessor.AgruparLinq(_datos);
        // Mostrar resumen en grid
        dgvProcesamiento.Rows.Clear();
        // Temporalmente usar columnas distintas
        foreach (var g in grupos.OrderByDescending(g => g.Count()))
        {
            var item = new DataItem
            {
                Id        = g.Count(),
                Nombre    = g.Key,
                Categoria = $"{g.Count()} items",
                Valor     = g.Average(x => x.Valor),
                Fuente    = "LINQ GroupBy",
                Fecha     = DateTime.Now
            };
            AgregarFilaGrid(dgvProcesamiento, item);
        }
        lblProcInfo.Text = $"LINQ .GroupBy() → {grupos.Count()} grupos";
    }

    private void BtnLinqOrderBy_Click(object sender, EventArgs e)
    {
        var ordenado = DataProcessor.OrdenarLinq(_datos).ToList();
        BindGridProcesamiento(ordenado);
        lblProcInfo.Text = $"LINQ .OrderByDescending(Valor) → {ordenado.Count} registros";
    }

    // ══════════════════════════════════════════════════════════════
    //  HELPERS DE ACTUALIZACIÓN GLOBAL
    // ══════════════════════════════════════════════════════════════

    private void ActualizarTodo()
    {
        _porCategoria = DataProcessor.AgruparPorCategoria(_datos);
        _porId        = DataProcessor.IndexarPorId(_datos);
        _datosVista   = new List<DataItem>(_datos);

        BindGridTodos(_datos);
        ActualizarListaCategorias();
        ActualizarTabEstadisticas();
        ActualizarGrafica();
        ActualizarFuentesCheckedList();

        lblTotalRegistros.Text  = $"Total registros: {_datos.Count}";
        lblTotalCategorias.Text = $"Categorías: {_porCategoria.Count}";
        lblTotalFuentes.Text    = $"Fuentes: {_datos.Select(d => d.Fuente).Distinct().Count()}";
    }

    private void ActualizarListaCategorias()
    {
        lstCategorias.Items.Clear();
        foreach (var cat in _porCategoria.Keys.OrderBy(k => k))
            lstCategorias.Items.Add(cat);
    }

    private void ActualizarFuentesCheckedList()
    {
        clbFuentes.Items.Clear();
        var fuentes = _datos.Select(d => d.Fuente).Distinct().OrderBy(f => f);
        foreach (var f in fuentes)
            clbFuentes.Items.Add(f, true);
    }

    private void ClbFuentes_ItemCheck(object sender, ItemCheckEventArgs e)
    {
        // Filtrar por fuentes seleccionadas
        BeginInvoke(() =>
        {
            var seleccionadas = clbFuentes.CheckedItems.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (e.NewValue == CheckState.Checked)
                seleccionadas.Add(clbFuentes.Items[e.Index].ToString()!);
            else
                seleccionadas.Remove(clbFuentes.Items[e.Index].ToString()!);

            var filtrado = _datos.Where(d => seleccionadas.Contains(d.Fuente)).ToList();
            BindGridTodos(filtrado);
            ActualizarEstadoBarra($"Mostrando {filtrado.Count} registros de fuentes: {string.Join(", ", seleccionadas)}");
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  BINDING DE GRIDS
    // ══════════════════════════════════════════════════════════════

    private void BindGridTodos(List<DataItem> lista)
    {
        dgvTodos.Rows.Clear();
        foreach (var item in lista)
            AgregarFilaGrid(dgvTodos, item);
        lblContadorTodos.Text = $"{lista.Count} registros";
    }

    private void BindGridCategoria(List<DataItem> lista)
    {
        dgvCategoria.Rows.Clear();
        foreach (var item in lista)
            AgregarFilaGrid(dgvCategoria, item);
    }

    private void BindGridProcesamiento(List<DataItem> lista)
    {
        dgvProcesamiento.Rows.Clear();
        foreach (var item in lista)
            AgregarFilaGrid(dgvProcesamiento, item);
    }

    private static void AgregarFilaGrid(DataGridView dgv, DataItem item)
    {
        int idx = dgv.Rows.Add(
            item.Id, item.Nombre, item.Categoria,
            item.Valor.ToString("F2"), item.Fecha.ToString("yyyy-MM-dd"), item.Fuente
        );

        // Colorear fila según fuente
        dgv.Rows[idx].DefaultCellStyle.BackColor = item.Fuente switch
        {
            "json"       => Color.FromArgb(20, 60, 20),
            "csv"        => Color.FromArgb(60, 50, 10),
            "xml"        => Color.FromArgb(10, 40, 70),
            "txt"        => Color.FromArgb(50, 20, 60),
            "postgresql" => Color.FromArgb(10, 30, 80),
            "mariadb"    => Color.FromArgb(60, 30, 10),
            _            => Color.FromArgb(40, 40, 40)
        };
        dgv.Rows[idx].DefaultCellStyle.ForeColor = Color.White;
    }

    // ══════════════════════════════════════════════════════════════
    //  HELPERS UI
    // ══════════════════════════════════════════════════════════════

    private void ActualizarEstadoBarra(string mensaje)
    {
        lblStatus.Text = mensaje;
        Application.DoEvents();
    }

    private void ConfigurarDataGridViews()
    {
        string[] cols = { "ID", "Nombre", "Categoría", "Valor", "Fecha", "Fuente" };
        int[]    widths = { 55, 240, 155, 90, 100, 100 };

        foreach (var dgv in new[] { dgvTodos, dgvCategoria, dgvProcesamiento })
        {
            dgv.Columns.Clear();
            for (int i = 0; i < cols.Length; i++)
                dgv.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = cols[i],
                    Width      = widths[i],
                    ReadOnly   = true
                });

            dgv.BackgroundColor        = Color.FromArgb(22, 22, 35);
            dgv.GridColor              = Color.FromArgb(60, 60, 80);
            dgv.DefaultCellStyle.BackColor = Color.FromArgb(30, 30, 45);
            dgv.DefaultCellStyle.ForeColor = Color.White;
            dgv.DefaultCellStyle.Font  = new Font("Consolas", 9f);
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 65);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.Cyan;
            dgv.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
            dgv.EnableHeadersVisualStyles = false;
            dgv.AllowUserToAddRows        = false;
            dgv.SelectionMode             = DataGridViewSelectionMode.FullRowSelect;
            dgv.RowHeadersVisible         = false;
        }

        // Columnas específicas para estadísticas
        dgvEstadisticas.Columns.Clear();
        foreach (var (titulo, w) in new[] {
            ("Categoría",55), ("Cant.",60), ("Promedio",90), ("Máximo",90), ("Mínimo",90), ("Total",110) })
        {
            dgvEstadisticas.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = titulo, Width = w+55, ReadOnly = true });
        }
        CopiarEstiloGrid(dgvEstadisticas, dgvTodos);
    }

    private static void CopiarEstiloGrid(DataGridView destino, DataGridView origen)
    {
        destino.BackgroundColor = origen.BackgroundColor;
        destino.GridColor       = origen.GridColor;
        destino.DefaultCellStyle.BackColor = origen.DefaultCellStyle.BackColor;
        destino.DefaultCellStyle.ForeColor = origen.DefaultCellStyle.ForeColor;
        destino.DefaultCellStyle.Font      = origen.DefaultCellStyle.Font;
        destino.ColumnHeadersDefaultCellStyle.BackColor = origen.ColumnHeadersDefaultCellStyle.BackColor;
        destino.ColumnHeadersDefaultCellStyle.ForeColor = origen.ColumnHeadersDefaultCellStyle.ForeColor;
        destino.ColumnHeadersDefaultCellStyle.Font      = origen.ColumnHeadersDefaultCellStyle.Font;
        destino.EnableHeadersVisualStyles = false;
        destino.AllowUserToAddRows        = false;
        destino.SelectionMode             = DataGridViewSelectionMode.FullRowSelect;
        destino.RowHeadersVisible         = false;
    }

    private void ConfigurarChart()
    {
        chart1.BackColor = Color.FromArgb(22, 22, 35);
        if (chart1.ChartAreas.Count == 0) chart1.ChartAreas.Add(new ChartArea("Default"));
        var area = chart1.ChartAreas[0];
        area.BackColor        = Color.FromArgb(30, 30, 45);
        area.AxisX.LabelStyle.ForeColor = Color.LightGray;
        area.AxisY.LabelStyle.ForeColor = Color.LightGray;
        area.AxisX.LineColor   = Color.SlateGray;
        area.AxisY.LineColor   = Color.SlateGray;
        area.AxisX.MajorGrid.LineColor = Color.FromArgb(50, 50, 70);
        area.AxisY.MajorGrid.LineColor = Color.FromArgb(50, 50, 70);
    }

    private static Color ColorPorIndice(int i)
    {
        Color[] colores =
        {
            Color.FromArgb(0, 200, 255), Color.FromArgb(255, 200, 0),
            Color.FromArgb(0, 255, 128), Color.FromArgb(255, 80, 100),
            Color.FromArgb(180, 100, 255), Color.FromArgb(255, 150, 50),
            Color.FromArgb(0, 220, 200), Color.FromArgb(220, 80, 220)
        };
        return colores[i % colores.Length];
    }

    // ══════════════════════════════════════════════════════════════
    //  MENÚ – Limpiar / Acerca de
    // ══════════════════════════════════════════════════════════════

    private void MenuLimpiarDatos_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show("¿Limpiar todos los datos en memoria?", "Confirmar",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        _datos.Clear();
        _porCategoria.Clear();
        _porId.Clear();
        _datosVista.Clear();
        dgvTodos.Rows.Clear();
        dgvCategoria.Rows.Clear();
        dgvEstadisticas.Rows.Clear();
        dgvProcesamiento.Rows.Clear();
        lstCategorias.Items.Clear();
        clbFuentes.Items.Clear();
        chart1.Series.Clear();
        lblContadorTodos.Text = "0 registros";
        ActualizarEstadoBarra("Datos limpiados.");
    }

    private void MenuAcercaDe_Click(object sender, EventArgs e)
        => MessageBox.Show(
            "Data Fusion Arena\nAdministración y Organización de Datos\n\n" +
            "Ingeniería · 4.º Semestre\nC# .NET 10 · WinForms\n\n" +
            "Fuentes soportadas: JSON · CSV · XML · TXT · PostgreSQL · MariaDB\n" +
            "Estructuras: List<T> · Dictionary<TKey,TValue>",
            "Acerca de", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void MenuSalir_Click(object sender, EventArgs e) => Close();
}

// ══════════════════════════════════════════════════════════════
//  Formulario auxiliar para cadenas de conexión a BD
// ══════════════════════════════════════════════════════════════
public class FormConexionBD : Form
{
    public string CadenaConexion { get; private set; } = "";
    public string NombreTabla    { get; private set; } = "";

    private readonly TextBox txtCadena;
    private readonly TextBox txtTabla;

    public FormConexionBD(string motor)
    {
        Text            = $"Conexión a {motor}";
        Size            = new Size(520, 260);
        StartPosition   = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        BackColor       = Color.FromArgb(30, 30, 45);
        ForeColor       = Color.White;

        string cadenaDefault = motor == "PostgreSQL"
            ? "Host=localhost;Port=5432;Database=datafusion;Username=postgres;Password=tu_password;"
            : "Server=localhost;Port=3306;Database=datafusion;User=root;Password=tu_password;";

        string tablaDefault = motor == "PostgreSQL" ? "videojuegos" : "puntuaciones";

        var lblCadena = new Label { Text = "Cadena de conexión:", Location = new Point(15, 20), AutoSize = true, ForeColor = Color.Cyan };
        txtCadena = new TextBox { Location = new Point(15, 42), Width = 475, Text = cadenaDefault,
            BackColor = Color.FromArgb(45, 45, 65), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

        var lblTabla = new Label { Text = "Nombre de tabla:", Location = new Point(15, 85), AutoSize = true, ForeColor = Color.Cyan };
        txtTabla = new TextBox { Location = new Point(15, 107), Width = 200, Text = tablaDefault,
            BackColor = Color.FromArgb(45, 45, 65), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

        var btnOk  = new Button { Text = "Conectar", Location = new Point(310, 170), Width = 90, DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(0, 120, 212), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var btnCan = new Button { Text = "Cancelar", Location = new Point(410, 170), Width = 80, DialogResult = DialogResult.Cancel,
            BackColor = Color.FromArgb(80, 30, 30), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };

        btnOk.Click += (_, _) => { CadenaConexion = txtCadena.Text.Trim(); NombreTabla = txtTabla.Text.Trim(); };

        Controls.AddRange(new Control[] { lblCadena, txtCadena, lblTabla, txtTabla, btnOk, btnCan });
        AcceptButton = btnOk;
        CancelButton = btnCan;
    }
}
