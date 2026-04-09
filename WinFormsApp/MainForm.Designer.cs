using System.Windows.Forms.DataVisualization.Charting;

namespace DataFusionArena.WinForms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    // ── Controles ────────────────────────────────────────────────
    // Menu
    private MenuStrip          menuStrip1;
    private ToolStripMenuItem  menuArchivo, menuBaseDatos, menuAyuda;
    private ToolStripMenuItem  menuCargarJson, menuCargarCsv, menuCargarXml, menuCargarTxt;
    private ToolStripMenuItem  menuCargarPersonalizado, menuSep1, menuLimpiarDatos, menuSep2, menuSalir;
    private ToolStripMenuItem  menuPostgres, menuMariaDB;
    private ToolStripMenuItem  menuAcercaDe;

    // ToolStrip
    private ToolStrip          toolStrip1;
    private ToolStripButton    tsBtnCargarTodo, tsBtnJson, tsBtnCsv, tsBtnXml, tsBtnTxt;
    private ToolStripSeparator tsSep1, tsSep2;
    private ToolStripButton    tsBtnPostgres, tsBtnMariaDB;

    // Main layout
    private SplitContainer     splitMain;

    // Left panel
    private GroupBox           grpFuentes;
    private CheckedListBox     clbFuentes;
    private Label              lblFuentesTitulo;

    // Right TabControl
    private TabControl         tabControl1;
    private TabPage            tabTodos, tabCategoria, tabEstadisticas, tabGraficas, tabProcesamiento;

    // ── Tab 1: Todos los datos ───────────────────────────────────
    private Panel              pnlToolsTodos;
    private Label              lblBuscar;
    private ComboBox           cmbCampoBusqueda;
    private TextBox            txtBusqueda;
    private Button             btnFiltrar, btnLimpiarFiltro;
    private Label              lblOrdenar;
    private ComboBox           cmbCampoOrden;
    private RadioButton        rbAscendente, rbDescendente;
    private Button             btnOrdenar;
    private DataGridView       dgvTodos;
    private Label              lblContadorTodos;

    // ── Tab 2: Por Categoría ─────────────────────────────────────
    private SplitContainer     splitCategoria;
    private ListBox            lstCategorias;
    private Label              lblCatTitulo;
    private DataGridView       dgvCategoria;
    private Label              lblCatInfo;

    // ── Tab 3: Estadísticas ──────────────────────────────────────
    private Panel              pnlStatsTop;
    private Label              lblTotalRegistros, lblTotalCategorias, lblTotalFuentes;
    private DataGridView       dgvEstadisticas;

    // ── Tab 4: Gráficas ──────────────────────────────────────────
    private Panel              pnlGraficasTop;
    private Label              lblTipoGrafica;
    private ComboBox           cmbTipoGrafica;
    private Button             btnActualizarGrafica;
    private Chart              chart1;

    // ── Tab 5: Procesamiento ─────────────────────────────────────
    private Panel              pnlProcTop;
    private Button             btnDetectarDuplicados, btnEliminarDuplicados;
    private GroupBox           grpLinq;
    private Label              lblLinqFiltro;
    private TextBox            txtLinqFiltro;
    private Button             btnLinqWhere, btnLinqGroupBy, btnLinqOrderBy;
    private Label              lblProcInfo;
    private DataGridView       dgvProcesamiento;

    // Status bar
    private StatusStrip        statusStrip1;
    private ToolStripStatusLabel lblStatus;

    // ─────────────────────────────────────────────────────────────
    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        // ── Colores base ─────────────────────────────────────────
        var bgDark    = Color.FromArgb(22,  22,  35);
        var bgMid     = Color.FromArgb(30,  30,  45);
        var bgLight   = Color.FromArgb(45,  45,  65);
        var fgWhite   = Color.White;
        var fgCyan    = Color.Cyan;
        var fgYellow  = Color.FromArgb(255, 200, 50);
        var accentBlue = Color.FromArgb(0,  120, 212);

        // ══════════════════════════════════════════════════════════
        //  FORM
        // ══════════════════════════════════════════════════════════
        SuspendLayout();
        ClientSize   = new Size(1300, 820);
        MinimumSize  = new Size(1100, 720);
        BackColor    = bgDark;
        ForeColor    = fgWhite;
        Font         = new Font("Segoe UI", 9f);
        Text         = "Data Fusion Arena";

        // ══════════════════════════════════════════════════════════
        //  MENU STRIP
        // ══════════════════════════════════════════════════════════
        menuStrip1  = new MenuStrip { BackColor = bgMid, ForeColor = fgWhite, Renderer = new DarkMenuRenderer() };

        menuArchivo = new ToolStripMenuItem("📂 Archivo") { ForeColor = fgWhite };
        menuCargarJson   = new ToolStripMenuItem("Cargar JSON",  null, (s,e) => BtnCargarJson_Click(s,e))   { ForeColor = Color.LightGreen };
        menuCargarCsv    = new ToolStripMenuItem("Cargar CSV",   null, (s,e) => BtnCargarCsv_Click(s,e))    { ForeColor = Color.Yellow };
        menuCargarXml    = new ToolStripMenuItem("Cargar XML",   null, (s,e) => BtnCargarXml_Click(s,e))    { ForeColor = Color.LightSkyBlue };
        menuCargarTxt    = new ToolStripMenuItem("Cargar TXT",   null, (s,e) => BtnCargarTxt_Click(s,e))    { ForeColor = Color.Violet };
        menuCargarPersonalizado = new ToolStripMenuItem("Cargar archivo...", null, MenuCargarPersonalizado_Click!) { ForeColor = fgWhite };
        menuSep1         = new ToolStripSeparator();
        menuLimpiarDatos = new ToolStripMenuItem("Limpiar datos", null, MenuLimpiarDatos_Click!)             { ForeColor = Color.Tomato };
        menuSep2         = new ToolStripSeparator();
        menuSalir        = new ToolStripMenuItem("Salir",          null, MenuSalir_Click!)                   { ForeColor = Color.Tomato };
        menuArchivo.DropDownItems.AddRange(new ToolStripItem[] {
            menuCargarJson, menuCargarCsv, menuCargarXml, menuCargarTxt,
            menuCargarPersonalizado, menuSep1, menuLimpiarDatos, menuSep2, menuSalir });

        menuBaseDatos = new ToolStripMenuItem("🗄️ Base de Datos") { ForeColor = fgWhite };
        menuPostgres  = new ToolStripMenuItem("🐘 Conectar PostgreSQL", null, BtnConectarPostgres_Click!) { ForeColor = Color.LightSkyBlue };
        menuMariaDB   = new ToolStripMenuItem("🐬 Conectar MariaDB",    null, BtnConectarMariaDB_Click!)  { ForeColor = Color.Orange };
        menuBaseDatos.DropDownItems.AddRange(new ToolStripItem[] { menuPostgres, menuMariaDB });

        menuAyuda     = new ToolStripMenuItem("❓ Ayuda") { ForeColor = fgWhite };
        menuAcercaDe  = new ToolStripMenuItem("Acerca de...", null, MenuAcercaDe_Click!) { ForeColor = fgWhite };
        menuAyuda.DropDownItems.Add(menuAcercaDe);

        menuStrip1.Items.AddRange(new ToolStripItem[] { menuArchivo, menuBaseDatos, menuAyuda });
        MainMenuStrip = menuStrip1;

        // ══════════════════════════════════════════════════════════
        //  TOOL STRIP
        // ══════════════════════════════════════════════════════════
        toolStrip1 = new ToolStrip { BackColor = bgLight, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(5,0,0,0) };

        tsBtnCargarTodo = TsBtn("⬇ Cargar Todo",    fgYellow,  BtnCargarTodo_Click!);
        tsSep1          = new ToolStripSeparator();
        tsBtnJson       = TsBtn("JSON",              Color.LightGreen,    BtnCargarJson_Click!);
        tsBtnCsv        = TsBtn("CSV",               Color.Yellow,        BtnCargarCsv_Click!);
        tsBtnXml        = TsBtn("XML",               Color.LightSkyBlue,  BtnCargarXml_Click!);
        tsBtnTxt        = TsBtn("TXT",               Color.Violet,        BtnCargarTxt_Click!);
        tsSep2          = new ToolStripSeparator();
        tsBtnPostgres   = TsBtn("🐘 PostgreSQL",     Color.LightSkyBlue,  BtnConectarPostgres_Click!);
        tsBtnMariaDB    = TsBtn("🐬 MariaDB",        Color.Orange,        BtnConectarMariaDB_Click!);

        toolStrip1.Items.AddRange(new ToolStripItem[] {
            tsBtnCargarTodo, tsSep1,
            tsBtnJson, tsBtnCsv, tsBtnXml, tsBtnTxt,
            tsSep2, tsBtnPostgres, tsBtnMariaDB });

        // ══════════════════════════════════════════════════════════
        //  STATUS STRIP
        // ══════════════════════════════════════════════════════════
        statusStrip1 = new StatusStrip { BackColor = bgLight };
        lblStatus    = new ToolStripStatusLabel("Listo.") { ForeColor = Color.LightGray, Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusStrip1.Items.Add(lblStatus);

        // ══════════════════════════════════════════════════════════
        //  SPLIT CONTAINER (main)
        // ══════════════════════════════════════════════════════════
        splitMain = new SplitContainer
        {
            Dock             = DockStyle.Fill,
            SplitterDistance = 180,
            Panel1MinSize    = 160,
            BackColor        = bgDark
        };

        // ── Panel Izquierdo: Fuentes ──────────────────────────────
        grpFuentes = new GroupBox
        {
            Text      = "Fuentes cargadas",
            Dock      = DockStyle.Fill,
            ForeColor = fgCyan,
            BackColor = bgMid,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
            Padding   = new Padding(8)
        };
        clbFuentes = new CheckedListBox
        {
            Dock          = DockStyle.Fill,
            BackColor     = bgMid,
            ForeColor     = fgWhite,
            BorderStyle   = BorderStyle.None,
            CheckOnClick  = true,
            Font          = new Font("Consolas", 9f)
        };
        clbFuentes.ItemCheck += ClbFuentes_ItemCheck!;
        grpFuentes.Controls.Add(clbFuentes);
        splitMain.Panel1.Controls.Add(grpFuentes);
        splitMain.Panel1.BackColor = bgDark;

        // ══════════════════════════════════════════════════════════
        //  TAB CONTROL
        // ══════════════════════════════════════════════════════════
        tabControl1 = new TabControl { Dock = DockStyle.Fill, BackColor = bgMid };
        tabTodos          = new TabPage("📋 Todos los Datos")  { BackColor = bgMid, ForeColor = fgWhite };
        tabCategoria      = new TabPage("📁 Por Categoría")    { BackColor = bgMid, ForeColor = fgWhite };
        tabEstadisticas   = new TabPage("📊 Estadísticas")     { BackColor = bgMid, ForeColor = fgWhite };
        tabGraficas       = new TabPage("📈 Gráficas")         { BackColor = bgMid, ForeColor = fgWhite };
        tabProcesamiento  = new TabPage("⚙️ Procesamiento")    { BackColor = bgMid, ForeColor = fgWhite };
        tabControl1.TabPages.AddRange(new[] { tabTodos, tabCategoria, tabEstadisticas, tabGraficas, tabProcesamiento });
        splitMain.Panel2.Controls.Add(tabControl1);
        splitMain.Panel2.BackColor = bgDark;

        // ══════════════════════════════════════════════════════════
        //  TAB 1 – Todos los datos
        // ══════════════════════════════════════════════════════════
        pnlToolsTodos = new Panel { Dock = DockStyle.Top, Height = 55, BackColor = bgLight, Padding = new Padding(8, 8, 8, 4) };

        lblBuscar       = Lbl("Buscar:", new Point(8, 18), fgCyan);
        cmbCampoBusqueda = new ComboBox { Location = new Point(65, 14), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = bgLight, ForeColor = fgWhite, FlatStyle = FlatStyle.Flat };
        cmbCampoBusqueda.Items.AddRange(new object[] { "nombre", "categoria", "fuente", "id", "valor" });
        cmbCampoBusqueda.SelectedIndex = 0;

        txtBusqueda     = new TextBox { Location = new Point(185, 15), Width = 180, BackColor = bgLight, ForeColor = fgWhite, BorderStyle = BorderStyle.FixedSingle };
        btnFiltrar      = Btn("🔍 Filtrar",    new Point(375, 13), 90, accentBlue,   BtnFiltrar_Click!);
        btnLimpiarFiltro = Btn("✖ Limpiar",   new Point(475, 13), 80, Color.FromArgb(100,40,40), BtnLimpiarFiltro_Click!);

        lblOrdenar      = Lbl("Ordenar:", new Point(580, 18), fgCyan);
        cmbCampoOrden   = new ComboBox { Location = new Point(640, 14), Width = 105, DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = bgLight, ForeColor = fgWhite, FlatStyle = FlatStyle.Flat };
        cmbCampoOrden.Items.AddRange(new object[] { "valor", "nombre", "categoria", "fecha", "id" });
        cmbCampoOrden.SelectedIndex = 0;

        rbAscendente    = Rb("↑ Asc",  new Point(756, 10), true,  bgLight);
        rbDescendente   = Rb("↓ Desc", new Point(756, 28), false, bgLight);
        btnOrdenar      = Btn("Ordenar", new Point(825, 13), 80, Color.FromArgb(40,80,40), BtnOrdenar_Click!);

        lblContadorTodos = new Label { Text = "0 registros", AutoSize = true, Location = new Point(920, 18),
            ForeColor = fgYellow, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };

        pnlToolsTodos.Controls.AddRange(new Control[] {
            lblBuscar, cmbCampoBusqueda, txtBusqueda, btnFiltrar, btnLimpiarFiltro,
            lblOrdenar, cmbCampoOrden, rbAscendente, rbDescendente, btnOrdenar, lblContadorTodos });

        dgvTodos = new DataGridView { Dock = DockStyle.Fill };
        tabTodos.Controls.Add(dgvTodos);
        tabTodos.Controls.Add(pnlToolsTodos);

        // ══════════════════════════════════════════════════════════
        //  TAB 2 – Por Categoría
        // ══════════════════════════════════════════════════════════
        splitCategoria = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 200, BackColor = bgMid };

        lblCatTitulo = new Label { Text = "Categorías", Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = fgCyan, Font = new Font("Segoe UI", 10f, FontStyle.Bold), BackColor = bgLight, Padding = new Padding(8,0,0,0) };
        lstCategorias = new ListBox { Dock = DockStyle.Fill, BackColor = bgMid, ForeColor = fgWhite,
            BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9.5f) };
        lstCategorias.SelectedIndexChanged += LstCategorias_SelectedIndexChanged!;
        splitCategoria.Panel1.Controls.AddRange(new Control[] { lstCategorias, lblCatTitulo });

        lblCatInfo  = new Label { Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = fgYellow, BackColor = bgLight, Padding = new Padding(8,0,0,0), Font = new Font("Segoe UI", 9f) };
        dgvCategoria = new DataGridView { Dock = DockStyle.Fill };
        splitCategoria.Panel2.Controls.AddRange(new Control[] { dgvCategoria, lblCatInfo });
        tabCategoria.Controls.Add(splitCategoria);

        // ══════════════════════════════════════════════════════════
        //  TAB 3 – Estadísticas
        // ══════════════════════════════════════════════════════════
        pnlStatsTop = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = bgLight, Padding = new Padding(10, 8, 0, 0) };

        lblTotalRegistros  = new Label { Text = "Total registros: 0",  AutoSize = true, Location = new Point(10, 10), ForeColor = fgYellow, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        lblTotalCategorias = new Label { Text = "Categorías: 0",       AutoSize = true, Location = new Point(200, 10), ForeColor = fgCyan,   Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        lblTotalFuentes    = new Label { Text = "Fuentes: 0",          AutoSize = true, Location = new Point(360, 10), ForeColor = Color.Violet, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        pnlStatsTop.Controls.AddRange(new Control[] { lblTotalRegistros, lblTotalCategorias, lblTotalFuentes });

        dgvEstadisticas = new DataGridView { Dock = DockStyle.Fill };
        tabEstadisticas.Controls.Add(dgvEstadisticas);
        tabEstadisticas.Controls.Add(pnlStatsTop);

        // ══════════════════════════════════════════════════════════
        //  TAB 4 – Gráficas
        // ══════════════════════════════════════════════════════════
        pnlGraficasTop = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = bgLight, Padding = new Padding(10,8,0,0) };
        lblTipoGrafica = Lbl("Tipo de gráfica:", new Point(8, 14), fgCyan);
        cmbTipoGrafica = new ComboBox { Location = new Point(135, 11), Width = 120, DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = bgLight, ForeColor = fgWhite, FlatStyle = FlatStyle.Flat };
        cmbTipoGrafica.Items.AddRange(new object[] { "Columnas", "Barras", "Pastel", "Línea" });
        cmbTipoGrafica.SelectedIndex = 0;
        cmbTipoGrafica.SelectedIndexChanged += CmbTipoGrafica_SelectedIndexChanged!;

        btnActualizarGrafica = Btn("🔄 Actualizar", new Point(270, 10), 110, accentBlue, BtnActualizarGrafica_Click!);
        pnlGraficasTop.Controls.AddRange(new Control[] { lblTipoGrafica, cmbTipoGrafica, btnActualizarGrafica });

        chart1 = new Chart { Dock = DockStyle.Fill, BackColor = bgDark };
        chart1.ChartAreas.Add(new ChartArea("Default"));
        chart1.Legends.Add(new Legend("Default") { BackColor = Color.FromArgb(30,30,45), ForeColor = Color.White });
        tabGraficas.Controls.Add(chart1);
        tabGraficas.Controls.Add(pnlGraficasTop);

        // ══════════════════════════════════════════════════════════
        //  TAB 5 – Procesamiento
        // ══════════════════════════════════════════════════════════
        pnlProcTop = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = bgLight, Padding = new Padding(10,8,0,0) };

        btnDetectarDuplicados = Btn("🔍 Detectar duplicados",  new Point(10, 10), 160, Color.FromArgb(80,60,10),  BtnDetectarDuplicados_Click!);
        btnEliminarDuplicados = Btn("🗑 Eliminar duplicados",  new Point(180, 10), 160, Color.FromArgb(80,20,20), BtnEliminarDuplicados_Click!) ;
        btnEliminarDuplicados.Enabled = false;

        grpLinq = new GroupBox { Text = "⚡ Bonus LINQ", Location = new Point(380, 5), Size = new Size(530, 75),
            ForeColor = fgYellow, BackColor = bgLight, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        lblLinqFiltro   = Lbl("Categoría:", new Point(10, 26), fgCyan);
        txtLinqFiltro   = new TextBox { Location = new Point(78, 23), Width = 130, BackColor = Color.FromArgb(45,45,65), ForeColor = fgWhite, BorderStyle = BorderStyle.FixedSingle };
        btnLinqWhere    = Btn(".Where()",   new Point(220, 22), 90, accentBlue,                   BtnLinqWhere_Click!);
        btnLinqGroupBy  = Btn(".GroupBy()", new Point(320, 22), 90, Color.FromArgb(40,80,40),     BtnLinqGroupBy_Click!);
        btnLinqOrderBy  = Btn(".OrderBy()", new Point(420, 22), 90, Color.FromArgb(60,40,80),     BtnLinqOrderBy_Click!);
        grpLinq.Controls.AddRange(new Control[] { lblLinqFiltro, txtLinqFiltro, btnLinqWhere, btnLinqGroupBy, btnLinqOrderBy });

        lblProcInfo = new Label { Text = "Selecciona una operación.", AutoSize = true, Location = new Point(930, 18),
            ForeColor = fgYellow, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };

        pnlProcTop.Controls.AddRange(new Control[] { btnDetectarDuplicados, btnEliminarDuplicados, grpLinq, lblProcInfo });

        dgvProcesamiento = new DataGridView { Dock = DockStyle.Fill };
        tabProcesamiento.Controls.Add(dgvProcesamiento);
        tabProcesamiento.Controls.Add(pnlProcTop);

        // ══════════════════════════════════════════════════════════
        //  ENSAMBLAR FORM
        // ══════════════════════════════════════════════════════════
        Controls.Add(splitMain);
        Controls.Add(toolStrip1);
        Controls.Add(menuStrip1);
        Controls.Add(statusStrip1);

        ResumeLayout(false);
        PerformLayout();
    }

    // ──────────────────────────────────────────────────────────────
    //  Helpers de construcción de controles
    // ──────────────────────────────────────────────────────────────
    private static Label Lbl(string text, Point loc, Color fore)
        => new() { Text = text, Location = loc, AutoSize = true, ForeColor = fore };

    private static Button Btn(string text, Point loc, int width, Color bg, EventHandler click)
    {
        var b = new Button
        {
            Text      = text,
            Location  = loc,
            Width     = width,
            Height    = 26,
            BackColor = bg,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 8.5f)
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += click;
        return b;
    }

    private static RadioButton Rb(string text, Point loc, bool chk, Color bg)
        => new() { Text = text, Location = loc, AutoSize = true, Checked = chk, ForeColor = Color.White, BackColor = bg };

    private static ToolStripButton TsBtn(string text, Color fore, EventHandler click)
    {
        var b = new ToolStripButton(text) { ForeColor = fore, BackColor = Color.Transparent, DisplayStyle = ToolStripItemDisplayStyle.Text };
        b.Click += click;
        return b;
    }
}

// ──────────────────────────────────────────────────────────────
//  Custom renderer para menú oscuro
// ──────────────────────────────────────────────────────────────
class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }
}

class DarkColorTable : ProfessionalColorTable
{
    private static readonly Color BgDark  = Color.FromArgb(30, 30, 45);
    private static readonly Color BgMid   = Color.FromArgb(45, 45, 65);
    private static readonly Color Accent  = Color.FromArgb(0, 120, 212);

    public override Color MenuItemSelected           => BgMid;
    public override Color MenuItemBorder             => Accent;
    public override Color MenuBorder                 => BgMid;
    public override Color MenuItemSelectedGradientBegin => BgMid;
    public override Color MenuItemSelectedGradientEnd   => BgMid;
    public override Color MenuItemPressedGradientBegin  => Accent;
    public override Color MenuItemPressedGradientEnd    => Accent;
    public override Color ToolStripDropDownBackground   => BgDark;
    public override Color ImageMarginGradientBegin   => BgDark;
    public override Color ImageMarginGradientMiddle  => BgDark;
    public override Color ImageMarginGradientEnd     => BgDark;
    public override Color MenuStripGradientBegin     => BgDark;
    public override Color MenuStripGradientEnd       => BgDark;
    public override Color SeparatorDark              => Color.FromArgb(60, 60, 80);
    public override Color SeparatorLight             => Color.FromArgb(60, 60, 80);
}
