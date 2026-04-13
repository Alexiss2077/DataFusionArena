using System.Windows.Forms.DataVisualization.Charting;

namespace DataFusionArena.WinForms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    // ── Controles ────────────────────────────────────────────────
    private MenuStrip menuStrip1;
    private ToolStripMenuItem menuArchivo, menuBaseDatos, menuAyuda;
    private ToolStripMenuItem menuCargarJson, menuCargarCsv, menuCargarXml, menuCargarTxt;
    private ToolStripMenuItem menuCargarPersonalizado, menuLimpiarDatos, menuSalir;
    private ToolStripMenuItem menuPostgres, menuMariaDB;
    private ToolStripMenuItem menuAcercaDe;
    private ToolStripSeparator menuSep1, menuSep2;

    private ToolStrip toolStrip1;
    private ToolStripButton tsBtnCargarTodo, tsBtnJson, tsBtnCsv, tsBtnXml, tsBtnTxt;
    private ToolStripButton tsBtnPostgres, tsBtnMariaDB, tsBtnRefresh;
    private ToolStripSeparator tsSep1, tsSep2, tsSep3;

    private SplitContainer splitMain;
    private GroupBox grpFuentes;
    private CheckedListBox clbFuentes;

    private TabControl tabControl1;
    private TabPage tabTodos, tabCategoria, tabEstadisticas, tabGraficas, tabProcesamiento;

    // Tab 1
    private Panel pnlToolsTodos;
    private Label lblBuscar, lblOrdenar, lblContadorTodos;
    private ComboBox cmbCampoBusqueda, cmbCampoOrden;
    private TextBox txtBusqueda;
    private Button btnFiltrar, btnLimpiarFiltro, btnOrdenar;
    private RadioButton rbAscendente, rbDescendente;
    private DataGridView dgvTodos;

    // Tab 2
    private SplitContainer splitCategoria;
    private ListBox lstCategorias;
    private DataGridView dgvCategoria;
    private Label lblCatInfo;

    // Tab 3
    private Panel pnlStatsTop;
    private Label lblTotalRegistros, lblTotalCategorias, lblTotalFuentes;
    private DataGridView dgvEstadisticas;

    // ── TAB 4 – Gráficas: ahora usa el control Chart de DataVisualization ──
    private Panel pnlGraficasTop;
    private Label lblTipoGrafica;
    private ComboBox cmbTipoGrafica;
    private Button btnActualizarGrafica;
    private Chart chartMain;   // ← REEMPLAZA al Panel pnlChart anterior

    // Tab 5 – dos paneles separados para mejor layout
    private Panel pnlDuplicados;   // fila superior: botones de duplicados
    private Panel pnlLinqArea;     // fila inferior: controles LINQ
    private Button btnDetectarDuplicados, btnEliminarDuplicados;
    private Label lblProcInfo;
    private ComboBox cmbLinqCampo;          // selector de campo (Categoría / Nombre / Fuente)
    private TextBox txtLinqFiltro;
    private Button btnLinqWhere, btnLinqGroupBy, btnLinqOrderBy, btnLinqLimpiar;
    private DataGridView dgvProcesamiento;

    private StatusStrip statusStrip1;
    private ToolStripStatusLabel lblStatus;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        var bgDark   = Color.FromArgb(18,  18,  28);
        var bgMid    = Color.FromArgb(28,  28,  42);
        var bgLight  = Color.FromArgb(42,  42,  60);
        var bgPanel  = Color.FromArgb(35,  35,  52);
        var fgWhite  = Color.FromArgb(230, 230, 240);
        var fgCyan   = Color.FromArgb(0,   200, 220);
        var fgYellow = Color.FromArgb(255, 200,  50);
        var accentBlue = Color.FromArgb(0,  110, 200);

        // ── Form ─────────────────────────────────────────────────
        SuspendLayout();
        ClientSize  = new Size(1400, 860);
        MinimumSize = new Size(1100, 700);
        BackColor   = bgDark;
        ForeColor   = fgWhite;
        Font        = new Font("Segoe UI", 9f);
        Text        = "Data Fusion Arena";
        WindowState = FormWindowState.Maximized;

        // ── MenuStrip ────────────────────────────────────────────
        menuStrip1 = new MenuStrip { BackColor = bgMid, ForeColor = fgWhite, Renderer = new DarkMenuRenderer() };

        menuArchivo            = MI("📂 Archivo", fgWhite);
        menuCargarJson         = MI("JSON",             Color.LightGreen, (s, e) => BtnCargarJson_Click(s, e));
        menuCargarCsv          = MI("CSV",              Color.Yellow,     (s, e) => BtnCargarCsv_Click(s, e));
        menuCargarXml          = MI("XML",              Color.LightSkyBlue,(s, e)=> BtnCargarXml_Click(s, e));
        menuCargarTxt          = MI("TXT",              Color.Violet,     (s, e) => BtnCargarTxt_Click(s, e));
        menuCargarPersonalizado= MI("Cargar archivo...",fgWhite,  MenuCargarPersonalizado_Click!);
        menuSep1               = new ToolStripSeparator();
        menuLimpiarDatos       = MI("Limpiar datos",    Color.Tomato, MenuLimpiarDatos_Click!);
        menuSep2               = new ToolStripSeparator();
        menuSalir              = MI("Salir",            Color.Tomato, MenuSalir_Click!);
        menuArchivo.DropDownItems.AddRange(new ToolStripItem[] {
            menuCargarJson, menuCargarCsv, menuCargarXml, menuCargarTxt,
            menuCargarPersonalizado, menuSep1, menuLimpiarDatos, menuSep2, menuSalir });

        menuBaseDatos = MI("🗄️ Base de Datos", fgWhite);
        menuPostgres  = MI("🐘 PostgreSQL", Color.LightSkyBlue, BtnConectarPostgres_Click!);
        menuMariaDB   = MI("🐬 MariaDB",    Color.Orange,       BtnConectarMariaDB_Click!);
        menuBaseDatos.DropDownItems.AddRange(new ToolStripItem[] { menuPostgres, menuMariaDB });

        menuAyuda    = MI("❓ Ayuda", fgWhite);
        menuAcercaDe = MI("Acerca de...", fgWhite, MenuAcercaDe_Click!);
        menuAyuda.DropDownItems.Add(menuAcercaDe);
        menuStrip1.Items.AddRange(new ToolStripItem[] { menuArchivo, menuBaseDatos, menuAyuda });
        MainMenuStrip = menuStrip1;

        // ── ToolStrip ────────────────────────────────────────────
        toolStrip1 = new ToolStrip { BackColor = bgLight, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(6, 2, 0, 2) };
        tsBtnCargarTodo = TSB("⬇  Cargar Todo", fgYellow,                      BtnCargarTodo_Click!);
        tsSep1          = new ToolStripSeparator();
        tsBtnJson       = TSB("JSON",            Color.LightGreen,              BtnCargarJson_Click!);
        tsBtnCsv        = TSB("CSV",             Color.Yellow,                  BtnCargarCsv_Click!);
        tsBtnXml        = TSB("XML",             Color.LightSkyBlue,            BtnCargarXml_Click!);
        tsBtnTxt        = TSB("TXT",             Color.Violet,                  BtnCargarTxt_Click!);
        tsSep2          = new ToolStripSeparator();
        tsBtnPostgres   = TSB("🐘 PostgreSQL",  Color.LightSkyBlue,            BtnConectarPostgres_Click!);
        tsBtnMariaDB    = TSB("🐬 MariaDB",     Color.Orange,                  BtnConectarMariaDB_Click!);
        tsSep3          = new ToolStripSeparator();
        tsBtnRefresh    = TSB("🔄 Actualizar BD", Color.FromArgb(0, 220, 180), BtnRefresh_Click!);
        tsBtnRefresh.ToolTipText = "Vuelve a descargar los datos de las bases de datos conectadas";
        toolStrip1.Items.AddRange(new ToolStripItem[] {
            tsBtnCargarTodo, tsSep1,
            tsBtnJson, tsBtnCsv, tsBtnXml, tsBtnTxt, tsSep2,
            tsBtnPostgres, tsBtnMariaDB, tsSep3,
            tsBtnRefresh });

        // ── StatusStrip ──────────────────────────────────────────
        statusStrip1 = new StatusStrip { BackColor = bgLight };
        lblStatus    = new ToolStripStatusLabel("Listo.") { ForeColor = Color.LightGray, Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusStrip1.Items.Add(lblStatus);

        // ── SplitContainer principal ─────────────────────────────
        splitMain = new SplitContainer
        {
            Dock         = DockStyle.Fill,
            Panel1MinSize= 140,
            FixedPanel   = FixedPanel.Panel1,
            BackColor    = bgDark
        };

        grpFuentes = new GroupBox { Text = "Fuentes cargadas", Dock = DockStyle.Fill, ForeColor = fgCyan, BackColor = bgPanel, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Padding = new Padding(6) };
        clbFuentes = new CheckedListBox { Dock = DockStyle.Fill, BackColor = bgPanel, ForeColor = fgWhite, BorderStyle = BorderStyle.None, CheckOnClick = true, Font = new Font("Consolas", 9f), ItemHeight = 22 };
        clbFuentes.ItemCheck += ClbFuentes_ItemCheck!;
        grpFuentes.Controls.Add(clbFuentes);
        splitMain.Panel1.Controls.Add(grpFuentes);
        splitMain.Panel1.BackColor = bgDark;

        // ── TabControl ───────────────────────────────────────────
        tabControl1    = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f) };
        tabTodos       = new TabPage("📋  Todos los Datos")  { BackColor = bgMid, ForeColor = fgWhite, UseVisualStyleBackColor = false };
        tabCategoria   = new TabPage("📁  Por Categoría")    { BackColor = bgMid, ForeColor = fgWhite, UseVisualStyleBackColor = false };
        tabEstadisticas= new TabPage("📊  Estadísticas")     { BackColor = bgMid, ForeColor = fgWhite, UseVisualStyleBackColor = false };
        tabGraficas    = new TabPage("📈  Gráficas")         { BackColor = bgMid, ForeColor = fgWhite, UseVisualStyleBackColor = false };
        tabProcesamiento=new TabPage("⚙️  Procesamiento")    { BackColor = bgMid, ForeColor = fgWhite, UseVisualStyleBackColor = false };
        tabControl1.TabPages.AddRange(new[] { tabTodos, tabCategoria, tabEstadisticas, tabGraficas, tabProcesamiento });
        splitMain.Panel2.Controls.Add(tabControl1);
        splitMain.Panel2.BackColor = bgDark;

        // ════════════════════════════════════════════════════════
        //  TAB 1 – Todos los datos
        // ════════════════════════════════════════════════════════
        pnlToolsTodos = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = bgLight, Padding = new Padding(8, 8, 8, 0) };

        lblBuscar       = Lbl("Buscar:", new Point(8, 15), fgCyan);
        cmbCampoBusqueda= Cmb(new Point(68, 11), 105, bgDark, fgWhite, new object[]{"nombre","categoria","fuente","id","valor"}, 0);
        txtBusqueda     = Txt(new Point(182, 12), 190, bgDark, fgWhite);
        btnFiltrar      = Btn("🔍 Filtrar",  new Point(382, 11),  90, accentBlue,               BtnFiltrar_Click!);
        btnLimpiarFiltro= Btn("✖ Limpiar",   new Point(480, 11),  80, Color.FromArgb(90,35,35),  BtnLimpiarFiltro_Click!);
        lblOrdenar      = Lbl("Ordenar:", new Point(578, 15), fgCyan);
        cmbCampoOrden   = Cmb(new Point(645, 11), 105, bgDark, fgWhite, new object[]{"valor","nombre","categoria","fecha","id"}, 0);
        rbAscendente    = Rb("↑ Asc",  new Point(760, 8),  true,  bgLight);
        rbDescendente   = Rb("↓ Desc", new Point(760, 26), false, bgLight);
        btnOrdenar      = Btn("Ordenar", new Point(830, 11),  85, Color.FromArgb(30,75,30),     BtnOrdenar_Click!);
        lblContadorTodos= new Label { Text = "0 registros", AutoSize = true, Location = new Point(930, 15), ForeColor = fgYellow, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        pnlToolsTodos.Controls.AddRange(new Control[] { lblBuscar, cmbCampoBusqueda, txtBusqueda, btnFiltrar, btnLimpiarFiltro, lblOrdenar, cmbCampoOrden, rbAscendente, rbDescendente, btnOrdenar, lblContadorTodos });

        dgvTodos = new DataGridView { Dock = DockStyle.Fill };
        tabTodos.Controls.Add(dgvTodos);
        tabTodos.Controls.Add(pnlToolsTodos);

        // ════════════════════════════════════════════════════════
        //  TAB 2 – Por categoría
        // ════════════════════════════════════════════════════════
        splitCategoria = new SplitContainer { Dock = DockStyle.Fill, Panel1MinSize = 160, FixedPanel = FixedPanel.Panel1, BackColor = bgMid };

        var lblCatTitulo = new Label { Text = "  Categorías", Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleLeft, ForeColor = fgCyan, BackColor = bgLight, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
        lstCategorias = new ListBox { Dock = DockStyle.Fill, BackColor = bgPanel, ForeColor = fgWhite, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9.5f), ItemHeight = 24 };
        lstCategorias.SelectedIndexChanged += LstCategorias_SelectedIndexChanged!;
        splitCategoria.Panel1.Controls.AddRange(new Control[] { lstCategorias, lblCatTitulo });
        splitCategoria.Panel1.BackColor = bgPanel;

        lblCatInfo    = new Label { Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleLeft, ForeColor = fgYellow, BackColor = bgLight, Padding = new Padding(8,0,0,0), Font = new Font("Segoe UI", 9f) };
        dgvCategoria  = new DataGridView { Dock = DockStyle.Fill };
        splitCategoria.Panel2.Controls.AddRange(new Control[] { dgvCategoria, lblCatInfo });
        splitCategoria.Panel2.BackColor = bgMid;
        tabCategoria.Controls.Add(splitCategoria);

        // ════════════════════════════════════════════════════════
        //  TAB 3 – Estadísticas
        // ════════════════════════════════════════════════════════
        pnlStatsTop      = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = bgLight, Padding = new Padding(10, 0, 0, 0) };
        lblTotalRegistros= new Label { Text = "Total registros: 0", AutoSize = true, Location = new Point(10, 12), ForeColor = fgYellow,       Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        lblTotalCategorias=new Label { Text = "Categorías: 0",      AutoSize = true, Location = new Point(200, 12), ForeColor = fgCyan,         Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        lblTotalFuentes  = new Label { Text = "Fuentes: 0",         AutoSize = true, Location = new Point(370, 12), ForeColor = Color.Violet,   Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        pnlStatsTop.Controls.AddRange(new Control[] { lblTotalRegistros, lblTotalCategorias, lblTotalFuentes });

        dgvEstadisticas  = new DataGridView { Dock = DockStyle.Fill };
        tabEstadisticas.Controls.Add(dgvEstadisticas);
        tabEstadisticas.Controls.Add(pnlStatsTop);

        // ════════════════════════════════════════════════════════
        //  TAB 4 – Gráficas con control Chart (DataVisualization)
        // ════════════════════════════════════════════════════════
        pnlGraficasTop = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = bgLight, Padding = new Padding(10, 8, 0, 0) };
        lblTipoGrafica = Lbl("Tipo:", new Point(8, 15), fgCyan);
        cmbTipoGrafica = Cmb(new Point(55, 11), 120, bgDark, fgWhite, new object[]{"Columnas","Barras","Pastel"}, 0);
        cmbTipoGrafica.SelectedIndexChanged += CmbTipoGrafica_SelectedIndexChanged!;
        btnActualizarGrafica = Btn("🔄 Actualizar", new Point(190, 11), 115, accentBlue, BtnActualizarGrafica_Click!);
        pnlGraficasTop.Controls.AddRange(new Control[] { lblTipoGrafica, cmbTipoGrafica, btnActualizarGrafica });

        // ── Control Chart (reemplaza Panel pnlChart con GDI+) ──────────────
        chartMain = new Chart
        {
            Dock            = DockStyle.Fill,
            BackColor       = Color.FromArgb(18, 18, 28),
            BorderlineColor = Color.FromArgb(42, 42, 60),
            BorderlineDashStyle = ChartDashStyle.Solid
        };
        tabGraficas.Controls.Add(chartMain);
        tabGraficas.Controls.Add(pnlGraficasTop);
        // ── Fin Tab 4 ────────────────────────────────────────────

        // ════════════════════════════════════════════════════════
        //  TAB 5 – Procesamiento  (layout: 2 paneles top + grid fill)
        // ════════════════════════════════════════════════════════

        // ── Panel A: Duplicados (48 px) ──────────────────────────
        pnlDuplicados = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 48,
            BackColor = bgLight,
        };

        btnDetectarDuplicados = Btn("🔍 Detectar duplicados", new Point(10, 11), 165,
            Color.FromArgb(75, 55, 10), BtnDetectarDuplicados_Click!);

        btnEliminarDuplicados = Btn("🗑 Eliminar duplicados", new Point(184, 11), 165,
            Color.FromArgb(80, 20, 20), BtnEliminarDuplicados_Click!);
        btnEliminarDuplicados.Enabled = false;

        lblProcInfo = new Label
        {
            Text      = "Selecciona una operación.",
            AutoSize  = false,
            Size      = new Size(900, 22),
            Location  = new Point(362, 14),
            ForeColor = fgYellow,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        pnlDuplicados.Controls.AddRange(new Control[]
            { btnDetectarDuplicados, btnEliminarDuplicados, lblProcInfo });

        // ── Separador A (entre paneles, 2 px) ────────────────────
        var sepEntrePaneles = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 2,
            BackColor = Color.FromArgb(55, 55, 80)
        };

        // ── Panel B: Bonus LINQ (58 px) ──────────────────────────
        pnlLinqArea = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 58,
            BackColor = Color.FromArgb(20, 20, 34),
        };

        // Borde superior cyan para delinearlo visualmente
        var bordeLinqTop = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 2,
            BackColor = Color.FromArgb(0, 140, 160)   // cyan oscuro
        };

        // Borde inferior cyan (separa el panel del DataGridView)
        var bordeLinqBot = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 3,
            BackColor = Color.FromArgb(0, 140, 160)
        };

        // Etiqueta de sección
        var lblLinqTitulo = new Label
        {
            Text      = "⚡ Bonus LINQ",
            AutoSize  = true,
            Location  = new Point(10, 18),
            ForeColor = fgYellow,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        // Selector de campo (ahora incluye ID)
        var lblCampoLinq = Lbl("Campo:", new Point(130, 19), fgCyan);
        cmbLinqCampo = Cmb(new Point(183, 15), 115, bgDark, fgWhite,
            new object[] { "Categoría", "Nombre", "Fuente", "ID" }, 0);

        // Cuadro de búsqueda
        var lblBuscarLinq = Lbl("Buscar:", new Point(307, 19), fgCyan);
        txtLinqFiltro = Txt(new Point(360, 16), 150, bgDark, fgWhite);
        txtLinqFiltro.Font = new Font("Consolas", 9f);

        // Botones LINQ — centrados verticalmente en 58px: (58-26)/2 = 16
        btnLinqWhere   = Btn(".Where()",   new Point(520, 16),  88, accentBlue,
            BtnLinqWhere_Click!);
        btnLinqGroupBy = Btn(".GroupBy()", new Point(615, 16),  88, Color.FromArgb(30, 75, 30),
            BtnLinqGroupBy_Click!);
        btnLinqOrderBy = Btn(".OrderBy()", new Point(710, 16),  88, Color.FromArgb(55, 35, 85),
            BtnLinqOrderBy_Click!);
        btnLinqLimpiar = Btn("✖ Limpiar",  new Point(805, 16),  78, Color.FromArgb(70, 25, 25),
            BtnLinqLimpiar_Click!);

        pnlLinqArea.Controls.AddRange(new Control[]
        {
            bordeLinqTop, bordeLinqBot,
            lblLinqTitulo, lblCampoLinq, cmbLinqCampo,
            lblBuscarLinq, txtLinqFiltro,
            btnLinqWhere, btnLinqGroupBy, btnLinqOrderBy, btnLinqLimpiar
        });

        // ── DataGridView (ocupa todo el espacio restante) ─────────
        dgvProcesamiento = new DataGridView { Dock = DockStyle.Fill };

        // IMPORTANTE – orden de Controls.Add:
        // WinForms doca los controles en z-order inverso (el último añadido queda arriba).
        // Fill siempre ocupa el espacio que dejan los Top.
        tabProcesamiento.Controls.Add(dgvProcesamiento);    // Fill  → va al fondo
        tabProcesamiento.Controls.Add(pnlLinqArea);          // Top   → segundo desde arriba
        tabProcesamiento.Controls.Add(sepEntrePaneles);      // Top   → separador entre paneles
        tabProcesamiento.Controls.Add(pnlDuplicados);        // Top   → primero desde arriba

        // ── Ensamblar ────────────────────────────────────────────
        Controls.Add(splitMain);
        Controls.Add(toolStrip1);
        Controls.Add(menuStrip1);
        Controls.Add(statusStrip1);
        ResumeLayout(false);
        PerformLayout();
    }

    // ── Helpers de construcción ───────────────────────────────────
    private static Label Lbl(string text, Point loc, Color fore)
        => new() { Text = text, Location = loc, AutoSize = true, ForeColor = fore };

    private static Button Btn(string text, Point loc, int width, Color bg, EventHandler click)
    {
        var b = new Button { Text = text, Location = loc, Width = width, Height = 26, BackColor = bg, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f) };
        b.FlatAppearance.BorderSize = 0;
        b.Click += click;
        return b;
    }

    private static RadioButton Rb(string text, Point loc, bool chk, Color bg)
        => new() { Text = text, Location = loc, AutoSize = true, Checked = chk, ForeColor = Color.White, BackColor = bg };

    private static ComboBox Cmb(Point loc, int width, Color bg, Color fg, object[] items, int sel)
    {
        var c = new ComboBox { Location = loc, Width = width, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = bg, ForeColor = fg, FlatStyle = FlatStyle.Flat };
        c.Items.AddRange(items);
        c.SelectedIndex = sel;
        return c;
    }

    private static TextBox Txt(Point loc, int width, Color bg, Color fg)
        => new() { Location = loc, Width = width, BackColor = bg, ForeColor = fg, BorderStyle = BorderStyle.FixedSingle };

    private static ToolStripButton TSB(string text, Color fore, EventHandler click)
    {
        var b = new ToolStripButton(text) { ForeColor = fore, BackColor = Color.Transparent, DisplayStyle = ToolStripItemDisplayStyle.Text, Font = new Font("Segoe UI", 9f) };
        b.Click += click;
        return b;
    }

    private static ToolStripMenuItem MI(string text, Color fore, EventHandler? click = null)
    {
        var m = new ToolStripMenuItem(text) { ForeColor = fore };
        if (click != null) m.Click += click;
        return m;
    }
}

class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }
}

class DarkColorTable : ProfessionalColorTable
{
    private static readonly Color BgDark  = Color.FromArgb(28, 28, 42);
    private static readonly Color BgMid   = Color.FromArgb(42, 42, 60);
    private static readonly Color Accent  = Color.FromArgb(0,  110, 200);
    public override Color MenuItemSelected                  => BgMid;
    public override Color MenuItemBorder                    => Accent;
    public override Color MenuBorder                        => BgMid;
    public override Color MenuItemSelectedGradientBegin     => BgMid;
    public override Color MenuItemSelectedGradientEnd       => BgMid;
    public override Color MenuItemPressedGradientBegin      => Accent;
    public override Color MenuItemPressedGradientEnd        => Accent;
    public override Color ToolStripDropDownBackground       => BgDark;
    public override Color ImageMarginGradientBegin          => BgDark;
    public override Color ImageMarginGradientMiddle         => BgDark;
    public override Color ImageMarginGradientEnd            => BgDark;
    public override Color MenuStripGradientBegin            => BgDark;
    public override Color MenuStripGradientEnd              => BgDark;
    public override Color SeparatorDark                     => Color.FromArgb(55, 55, 75);
    public override Color SeparatorLight                    => Color.FromArgb(55, 55, 75);
}
