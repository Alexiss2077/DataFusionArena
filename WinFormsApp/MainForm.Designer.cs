namespace DataFusionArena.WinForms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    // ── Menu ─────────────────────────────────────────────────────
    private MenuStrip menuStrip1;
    private ToolStripMenuItem menuArchivo, menuBaseDatos, menuAyuda;
    private ToolStripMenuItem menuCargarJson, menuCargarCsv, menuCargarXml, menuCargarTxt;
    private ToolStripMenuItem menuCargarPersonalizado, menuLimpiarDatos, menuSalir;
    private ToolStripMenuItem menuPostgres, menuMariaDB;
    private ToolStripMenuItem menuAcercaDe;
    private ToolStripMenuItem menuExportar, menuExportCsv, menuExportJson, menuExportXml, menuExportTxt;
    private ToolStripMenuItem menuExportarBD;
    private ToolStripSeparator menuSep1, menuSep2, menuSepExport, menuSepBD;

    // ── Sidebar toolbar (left vertical panel) ────────────────────
    private Panel pnlSidebar;
    private Panel pnlLogo;
    private Label lblLogoTitle;
    private Label lblLogoSub;
    private Panel pnlSidebarNav;

    // Sidebar nav buttons
    private Button btnNavLoad;
    private Button btnNavDatabase;
    private Button btnNavExport;
    private Button btnNavRefresh;
    private Button btnNavClear;

    // Sidebar file type quick-load buttons
    private Panel pnlSidebarFiles;
    private Label lblSidebarFilesTitle;
    private Button btnSbJson, btnSbCsv, btnSbXml, btnSbTxt, btnSbAll;

    // Sidebar export quick buttons
    private Panel pnlSidebarExport;
    private Label lblSidebarExportTitle;
    private Button btnSbExpCsv, btnSbExpJson, btnSbExpXml, btnSbExpTxt, btnSbExpBD;

    // Sidebar database buttons
    private Panel pnlSidebarDB;
    private Label lblSidebarDBTitle;
    private Button btnSbPostgres, btnSbMariaDB, btnSbRefresh;

    // Sidebar sources filter
    private Panel pnlSidebarSources;
    private Label lblSidebarSourcesTitle;
    private CheckedListBox clbFuentes;

    // ── Main content area ────────────────────────────────────────
    private Panel pnlMain;

    // Top stats bar
    private Panel pnlTopStats;
    private Panel pnlStatRecords, pnlStatCategories, pnlStatSources;
    private Label lblStatRecordsVal, lblStatRecordsLbl;
    private Label lblStatCategoriesVal, lblStatCategoriesLbl;
    private Label lblStatSourcesVal, lblStatSourcesLbl;

    // Tab control
    private DarkTabControl tabControl1;
    private TabPage tabTodos, tabCategoria, tabEstadisticas, tabGraficas, tabProcesamiento;

    // Tab 1 – All data
    private Panel pnlToolsTodos;
    private Label lblBuscar, lblOrdenar, lblContadorTodos;
    private ComboBox cmbCampoBusqueda, cmbCampoOrden;
    private TextBox txtBusqueda;
    private Button btnFiltrar, btnLimpiarFiltro, btnOrdenar;
    private RadioButton rbAscendente, rbDescendente;
    private DataGridView dgvTodos;

    // Tab 2 – By category
    private SplitContainer splitCategoria;
    private ListBox lstCategorias;
    private DataGridView dgvCategoria;
    private Label lblCatInfo;

    // Tab 3 – Statistics
    private Panel pnlStatsTop;
    private Label lblTotalRegistros, lblTotalCategorias, lblTotalFuentes;
    private DataGridView dgvEstadisticas;

    // Tab 4 – Charts
    private Panel pnlGraficasTop;
    private Label lblTipoGrafica, lblGrupoGrafica, lblMetricaGrafica;
    private ComboBox cmbTipoGrafica, cmbGrupoGrafica, cmbMetricaGrafica;
    private Button btnActualizarGrafica;
    private ChartPanel chartMain;

    // Tab 5 – Processing
    private Panel pnlProcHeader;
    private Button btnDetectarDuplicados, btnEliminarDuplicados;
    private Label lblProcInfo;
    private ComboBox cmbLinqCampo;
    private TextBox txtLinqFiltro;
    private Button btnLinqWhere, btnLinqGroupBy, btnLinqOrderBy, btnLinqLimpiar;
    private DataGridView dgvProcesamiento;

    // Status bar
    private StatusStrip statusStrip1;
    private ToolStripStatusLabel lblStatus;
    private ToolStripStatusLabel lblStatusRight;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        // ── Design tokens ────────────────────────────────────────
        var clrBg = Color.FromArgb(13, 13, 18);   // near-black bg
        var clrSurface = Color.FromArgb(20, 20, 28);   // surface cards
        var clrSurface2 = Color.FromArgb(26, 26, 36);   // elevated surface
        var clrSidebar = Color.FromArgb(16, 16, 23);   // sidebar bg
        var clrBorder = Color.FromArgb(36, 36, 52);   // borders
        var clrBorder2 = Color.FromArgb(48, 48, 68);   // hover border
        var clrText = Color.FromArgb(225, 225, 235);  // primary text
        var clrTextDim = Color.FromArgb(110, 110, 140);  // dimmed text
        var clrTextMuted = Color.FromArgb(68, 68, 90);   // muted text
        var clrMint = Color.FromArgb(52, 211, 153);  // accent mint/green
        var clrMintDim = Color.FromArgb(30, 120, 85);   // darker mint
        var clrAmber = Color.FromArgb(251, 191, 36);   // accent amber
        var clrAmberDim = Color.FromArgb(120, 88, 12);   // darker amber
        var clrRose = Color.FromArgb(251, 113, 133);  // accent rose
        var clrSky = Color.FromArgb(125, 211, 252);  // accent sky
        var clrPurple = Color.FromArgb(167, 139, 250);  // accent purple

        SuspendLayout();
        ClientSize = new Size(1440, 900);
        MinimumSize = new Size(1100, 700);
        BackColor = clrBg;
        ForeColor = clrText;
        Font = new Font("Segoe UI", 9f);
        Text = "Data Fusion Arena";
        WindowState = FormWindowState.Maximized;

        // ════════════════════════════════════════════════════════
        //  MENU STRIP (hidden from view but functional)
        // ════════════════════════════════════════════════════════
        menuStrip1 = new MenuStrip
        {
            BackColor = clrSidebar,
            ForeColor = clrText,
            Visible = false,   // hidden – access via sidebar
            Renderer = new MinimalMenuRenderer()
        };

        menuArchivo = MI("Archivo", clrText);
        menuCargarJson = MI("Cargar JSON", clrMint, (s, e) => BtnCargarJson_Click(s, e));
        menuCargarCsv = MI("Cargar CSV", clrAmber, (s, e) => BtnCargarCsv_Click(s, e));
        menuCargarXml = MI("Cargar XML", clrSky, (s, e) => BtnCargarXml_Click(s, e));
        menuCargarTxt = MI("Cargar TXT", clrPurple, (s, e) => BtnCargarTxt_Click(s, e));
        menuCargarPersonalizado = MI("Abrir archivo...", clrText, MenuCargarPersonalizado_Click!);
        menuSep1 = new ToolStripSeparator();
        menuExportar = MI("Exportar archivo", clrMint);
        menuExportCsv = MI("CSV", clrText, (s, e) => BtnExportarCsv_Click(s, e));
        menuExportJson = MI("JSON", clrText, (s, e) => BtnExportarJson_Click(s, e));
        menuExportXml = MI("XML", clrText, (s, e) => BtnExportarXml_Click(s, e));
        menuExportTxt = MI("TXT", clrText, (s, e) => BtnExportarTxt_Click(s, e));
        menuExportar.DropDownItems.AddRange(new ToolStripItem[]
            { menuExportCsv, menuExportJson, menuExportXml, menuExportTxt });
        menuSepBD = new ToolStripSeparator();
        menuExportarBD = MI("Exportar a Base de Datos...", clrAmber, (s, e) => BtnExportarBD_Click(s, e));
        menuSepExport = new ToolStripSeparator();
        menuLimpiarDatos = MI("Limpiar datos", clrRose, MenuLimpiarDatos_Click!);
        menuSep2 = new ToolStripSeparator();
        menuSalir = MI("Salir", clrRose, MenuSalir_Click!);
        menuArchivo.DropDownItems.AddRange(new ToolStripItem[]
        {
            menuCargarJson, menuCargarCsv, menuCargarXml, menuCargarTxt,
            menuCargarPersonalizado, menuSep1,
            menuExportar, menuSepBD, menuExportarBD,
            menuSepExport, menuLimpiarDatos, menuSep2, menuSalir
        });
        menuBaseDatos = MI("Base de Datos", clrText);
        menuPostgres = MI("PostgreSQL", clrSky, BtnConectarPostgres_Click!);
        menuMariaDB = MI("MariaDB", clrAmber, BtnConectarMariaDB_Click!);
        menuBaseDatos.DropDownItems.AddRange(new ToolStripItem[] { menuPostgres, menuMariaDB });
        menuAyuda = MI("Ayuda", clrText);
        menuAcercaDe = MI("Acerca de...", clrText, MenuAcercaDe_Click!);
        menuAyuda.DropDownItems.Add(menuAcercaDe);
        menuStrip1.Items.AddRange(new ToolStripItem[] { menuArchivo, menuBaseDatos, menuAyuda });
        MainMenuStrip = menuStrip1;

        // ════════════════════════════════════════════════════════
        //  STATUS STRIP
        // ════════════════════════════════════════════════════════
        statusStrip1 = new StatusStrip
        {
            BackColor = clrSurface,
            SizingGrip = false,
            Padding = new Padding(8, 0, 8, 0),
            Font = new Font("Segoe UI", 8f)
        };
        statusStrip1.Renderer = new MinimalMenuRenderer();
        lblStatus = new ToolStripStatusLabel("Listo — carga datos para comenzar")
        {
            ForeColor = clrTextDim,
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        lblStatusRight = new ToolStripStatusLabel("Data Fusion Arena v1.0")
        {
            ForeColor = clrTextMuted,
            TextAlign = ContentAlignment.MiddleRight
        };
        statusStrip1.Items.AddRange(new ToolStripItem[] { lblStatus, lblStatusRight });

        // ════════════════════════════════════════════════════════
        //  SIDEBAR
        // ════════════════════════════════════════════════════════
        pnlSidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 210,
            BackColor = clrSidebar
        };

        // ── Logo ──────────────────────────────────────────────
        pnlLogo = new Panel
        {
            Height = 80,
            BackColor = clrSidebar
        };
        var logoAccent = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(3, 80),
            BackColor = clrMint
        };
        lblLogoTitle = new Label
        {
            Text = "DataFusion",
            Location = new Point(16, 16),
            AutoSize = true,
            ForeColor = clrText,
            Font = new Font("Segoe UI", 15f, FontStyle.Bold)
        };
        lblLogoSub = new Label
        {
            Text = "Arena",
            Location = new Point(17, 44),
            AutoSize = true,
            ForeColor = clrMint,
            Font = new Font("Segoe UI", 10f)
        };
        pnlLogo.Controls.AddRange(new Control[] { logoAccent, lblLogoTitle, lblLogoSub });

        // ── Helpers ───────────────────────────────────────────
        Panel SbDivider() => new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = clrBorder
        };

        Label SbSection(string text) => new Label
        {
            Text = text.ToUpper(),
            Dock = DockStyle.Top,
            Height = 24,
            Padding = new Padding(14, 8, 0, 0),
            ForeColor = clrTextMuted,
            Font = new Font("Segoe UI", 6.5f, FontStyle.Bold),
            BackColor = clrSidebar
        };

        Button SbBtn(string text, Color accent, EventHandler? click = null)
        {
            var b = new Button
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = clrText,
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(28, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 38, 56);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(44, 44, 64);
            if (click != null) b.Click += click;
            b.Paint += (s, e) =>
            {
                using var br = new SolidBrush(accent);
                int cy = b.Height / 2;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(br, 12, cy - 3, 6, 6);
            };
            return b;
        }

        // ── LOAD FILES SECTION ────────────────────────────────
        pnlSidebarFiles = new Panel { Dock = DockStyle.Top, Height = 292, BackColor = clrSidebar };

        var lblFilesTitle = SbSection("Cargar datos");
        btnSbJson = SbBtn("JSON", clrMint, BtnCargarJson_Click!);
        btnSbCsv = SbBtn("CSV", clrAmber, BtnCargarCsv_Click!);
        btnSbXml = SbBtn("XML", clrSky, BtnCargarXml_Click!);
        btnSbTxt = SbBtn("TXT", clrPurple, BtnCargarTxt_Click!);
        btnSbAll = SbBtn("Cargar todo", clrMint, BtnCargarTodo_Click!);

        var btnSbImport = new Button
        {
            Text = "↑  Importar archivo...",
            Dock = DockStyle.Top,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(14, 44, 30),
            ForeColor = clrMint,
            Font = new Font("Segoe UI", 8f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 4, 0),
            Cursor = Cursors.Hand
        };
        btnSbImport.FlatAppearance.BorderSize = 0;
        btnSbImport.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 58, 42);
        btnSbImport.Click += MenuCargarPersonalizado_Click!;

        var divImport = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = clrBorder };

        var btnSbLimpiar = new Button
        {
            Text = "Limpiar datos",
            Dock = DockStyle.Top,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(180, 80, 90),
            Font = new Font("Segoe UI", 8.5f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(28, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        btnSbLimpiar.FlatAppearance.BorderSize = 0;
        btnSbLimpiar.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 38, 56);
        btnSbLimpiar.Click += MenuLimpiarDatos_Click!;
        btnSbLimpiar.Paint += (s, e) =>
        {
            using var br = new SolidBrush(Color.FromArgb(180, 80, 90));
            int cy = btnSbLimpiar.Height / 2;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.FillEllipse(br, 12, cy - 3, 6, 6);
        };

        var divLimpiar = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = clrBorder };

        // Dock=Top stacks in reverse: last added = visually topmost
        pnlSidebarFiles.Controls.AddRange(new Control[]
        {
            btnSbLimpiar, divLimpiar,
            btnSbAll, btnSbTxt, btnSbXml, btnSbCsv, btnSbJson,
            divImport, btnSbImport,
            lblFilesTitle
        });

        // ── SOURCES SECTION ───────────────────────────────────
        pnlSidebarSources = new Panel { Dock = DockStyle.Top, Height = 110, BackColor = clrSidebar };
        var sbDivSrc = SbDivider();
        var lblSrcTitle = SbSection("Fuentes activas");
        clbFuentes = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            BackColor = clrSidebar,
            ForeColor = clrText,
            BorderStyle = BorderStyle.None,
            CheckOnClick = true,
            Font = new Font("Segoe UI", 8.5f),
            ItemHeight = 22,
            Margin = new Padding(0)
        };
        clbFuentes.ItemCheck += ClbFuentes_ItemCheck!;
        pnlSidebarSources.Controls.AddRange(new Control[] { clbFuentes, lblSrcTitle, sbDivSrc });

        // ── DB SECTION ────────────────────────────────────────
        pnlSidebarDB = new Panel { Dock = DockStyle.Top, Height = 128, BackColor = clrSidebar };
        var sbDivDB = SbDivider();
        var lblDBTitle = SbSection("Base de datos");
        btnSbPostgres = SbBtn("PostgreSQL", clrSky, BtnConectarPostgres_Click!);
        btnSbMariaDB = SbBtn("MariaDB", clrAmber, BtnConectarMariaDB_Click!);
        btnSbRefresh = SbBtn("Actualizar BD", clrMint, BtnRefresh_Click!);
        pnlSidebarDB.Controls.AddRange(new Control[]
            { btnSbRefresh, btnSbMariaDB, btnSbPostgres, lblDBTitle, sbDivDB });

        // ── EXPORT SECTION ────────────────────────────────────
        pnlSidebarExport = new Panel { Dock = DockStyle.Top, Height = 210, BackColor = clrSidebar };
        var sbDivExp = SbDivider();
        var lblExpTitle = SbSection("Exportar");

        btnSbExpCsv = SbBtn("CSV", clrAmber, (s, e) => BtnExportarCsv_Click(s, e));
        btnSbExpJson = SbBtn("JSON", clrMint, (s, e) => BtnExportarJson_Click(s, e));
        btnSbExpXml = SbBtn("XML", clrSky, (s, e) => BtnExportarXml_Click(s, e));
        btnSbExpTxt = SbBtn("TXT (pipe)", clrPurple, (s, e) => BtnExportarTxt_Click(s, e));

        var divExpBD = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = clrBorder };

        btnSbExpBD = new Button
        {
            Text = "↑  Exportar a BD",
            Dock = DockStyle.Top,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(38, 26, 6),
            ForeColor = clrAmber,
            Font = new Font("Segoe UI", 8.5f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 4, 0),
            Cursor = Cursors.Hand
        };
        btnSbExpBD.FlatAppearance.BorderSize = 0;
        btnSbExpBD.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 36, 10);
        btnSbExpBD.Click += (s, e) => BtnExportarBD_Click(s, e);

        // Dock=Top reverse order: last = topmost
        pnlSidebarExport.Controls.AddRange(new Control[]
        {
            btnSbExpTxt, btnSbExpXml, btnSbExpJson, btnSbExpCsv,
            divExpBD, btnSbExpBD,
            lblExpTitle, sbDivExp
        });

        // ── Assemble sidebar ──────────────────────────────────
        // With Dock=Top, the FIRST control added goes to the TOP.
        // So add in exact visual top→bottom order:
        // Logo, Cargar datos, Exportar, Base de datos, Fuentes activas
        pnlSidebar.SuspendLayout();
        pnlSidebar.Controls.Clear();

        // All sections must be Dock=Top so they stack downward
        pnlLogo.Dock = DockStyle.Top;
        pnlSidebarFiles.Dock = DockStyle.Top;
        pnlSidebarExport.Dock = DockStyle.Top;
        pnlSidebarDB.Dock = DockStyle.Top;
        pnlSidebarSources.Dock = DockStyle.Top;

        pnlSidebar.Controls.Add(pnlSidebarSources);  // added last = bottom
        pnlSidebar.Controls.Add(pnlSidebarDB);
        pnlSidebar.Controls.Add(pnlSidebarExport);
        pnlSidebar.Controls.Add(pnlSidebarFiles);
        pnlSidebar.Controls.Add(pnlLogo);             // added last of reversed = top
        pnlLogo.Size = new Size(pnlSidebar.Width, 80);
        logoAccent.Size = new Size(3, 80);
        pnlSidebar.ResumeLayout();

        // ════════════════════════════════════════════════════════
        //  MAIN CONTENT PANEL
        // ════════════════════════════════════════════════════════
        pnlMain = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = clrBg,
            Padding = new Padding(0)
        };

        // ── TOP STATS BAR ─────────────────────────────────────
        // Panel height = 8 (top pad) + 30 (number) + 4 (gap) + 16 (label) + 8 (bottom pad) = 66
        // But we give 72 to breathe. Key: row heights must sum to <= panel height - padding.
        pnlTopStats = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = clrSurface
        };

        // Use a simple FlowLayoutPanel of vertical-stack sub-panels per stat.
        // Each sub-panel is a vertical TableLayoutPanel with fixed pixel rows.
        var statsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(20, 8, 0, 0),
            AutoSize = false
        };

        // Builds one stat block: [accent bar 2px] [number 30px] [desc 16px]
        // Total inner height = 48px, which fits inside 72-8=64px available.
        TableLayoutPanel MakeStat(string val, string desc, Color accent,
            out Label valLbl, out Label descLbl)
        {
            var t = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 3,
                Width = 210,
                Height = 56,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 0),
                Padding = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 4));   // accent bar row
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));  // number row
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));  // label row

            var bar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 32,
                Height = 2,
                BackColor = accent,
                Margin = new Padding(0, 2, 0, 0)
            };
            var barWrap = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            barWrap.Controls.Add(bar);

            valLbl = new Label
            {
                Text = val,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                ForeColor = clrText,
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            descLbl = new Label
            {
                Text = desc,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                ForeColor = clrTextDim,
                Font = new Font("Segoe UI", 8f),
                TextAlign = ContentAlignment.TopLeft
            };

            t.Controls.Add(barWrap, 0, 0);
            t.Controls.Add(valLbl, 0, 1);
            t.Controls.Add(descLbl, 0, 2);
            return t;
        }

        var statRecordsTbl = MakeStat("0", "registros totales", clrMint,
            out lblStatRecordsVal, out lblStatRecordsLbl);
        var statCategoriesTbl = MakeStat("0", "categorías", clrAmber,
            out lblStatCategoriesVal, out lblStatCategoriesLbl);
        var statSourcesTbl = MakeStat("0", "fuentes activas", clrSky,
            out lblStatSourcesVal, out lblStatSourcesLbl);

        // Thin vertical dividers between stat blocks
        Panel VDiv() => new Panel
        {
            Width = 1,
            Height = 40,
            BackColor = clrBorder,
            Margin = new Padding(0, 8, 0, 0)
        };

        statsFlow.Controls.AddRange(new Control[]
        {
            statRecordsTbl, VDiv(),
            statCategoriesTbl, VDiv(),
            statSourcesTbl
        });

        // Wire to MainForm.cs fields
        lblTotalRegistros = lblStatRecordsVal;
        lblTotalCategorias = lblStatCategoriesVal;
        lblTotalFuentes = lblStatSourcesVal;

        // Dummy panels so Designer field declarations compile
        pnlStatRecords = new Panel { Visible = false };
        pnlStatCategories = new Panel { Visible = false };
        pnlStatSources = new Panel { Visible = false };

        pnlTopStats.Controls.Add(statsFlow);
        pnlTopStats.Controls.Add(new Panel
        { Dock = DockStyle.Bottom, Height = 1, BackColor = clrBorder });

        // ── TAB CONTROL ───────────────────────────────────────
        tabControl1 = new DarkTabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9f),
            DrawMode = TabDrawMode.OwnerDrawFixed,
            ItemSize = new Size(140, 38),
            Padding = new Point(16, 8),
            SizeMode = TabSizeMode.Fixed
        };
        // DrawItem not needed — DarkTabControl paints itself via OnPaint

        tabTodos = new TabPage("Todos los datos") { BackColor = clrBg, UseVisualStyleBackColor = false };
        tabCategoria = new TabPage("Por categoría") { BackColor = clrBg, UseVisualStyleBackColor = false };
        tabEstadisticas = new TabPage("Estadísticas") { BackColor = clrBg, UseVisualStyleBackColor = false };
        tabGraficas = new TabPage("Gráficas") { BackColor = clrBg, UseVisualStyleBackColor = false };
        tabProcesamiento = new TabPage("Procesamiento") { BackColor = clrBg, UseVisualStyleBackColor = false };
        tabControl1.TabPages.AddRange(new[]
            { tabTodos, tabCategoria, tabEstadisticas, tabGraficas, tabProcesamiento });

        // ════════════════════════════════════════════════════════
        //  TAB 1 – ALL DATA
        // ════════════════════════════════════════════════════════
        pnlToolsTodos = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = clrSurface,
            Padding = new Padding(16, 12, 16, 0)
        };
        var toolsBorderBot = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = clrBorder };

        // Search area
        lblBuscar = FLabel("Buscar en", new Point(16, 18), clrTextDim);

        cmbCampoBusqueda = FCombo(new Point(88, 14), 110, clrSurface2, clrText,
            new object[] { }, -1);

        txtBusqueda = new TextBox
        {
            Location = new Point(208, 14),
            Width = 220,
            BackColor = clrSurface2,
            ForeColor = clrText,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f)
        };
        txtBusqueda.KeyPress += (s, e) => { if (e.KeyChar == (char)13) BtnFiltrar_Click(s, EventArgs.Empty); };

        btnFiltrar = FButton("Filtrar", new Point(438, 13), 80, clrMintDim, clrMint, BtnFiltrar_Click!);
        btnLimpiarFiltro = FButton("Limpiar", new Point(526, 13), 72, clrSurface2, clrTextDim, BtnLimpiarFiltro_Click!);

        // Divider
        var toolsDiv = new Panel { Location = new Point(616, 10), Size = new Size(1, 30), BackColor = clrBorder };

        // Sort area
        lblOrdenar = FLabel("Ordenar por", new Point(628, 18), clrTextDim);
        cmbCampoOrden = FCombo(new Point(720, 14), 110, clrSurface2, clrText,
            new object[] { }, -1);
        rbAscendente = FRadio("↑ Asc", new Point(840, 10), true, clrSurface);
        rbDescendente = FRadio("↓ Desc", new Point(840, 30), false, clrSurface);
        btnOrdenar = FButton("Ordenar", new Point(916, 13), 80, clrSurface2, clrTextDim, BtnOrdenar_Click!);

        lblContadorTodos = new Label
        {
            Text = "0 registros",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            ForeColor = clrMint
        };
        lblContadorTodos.Location = new Point(1010, 18);

        pnlToolsTodos.Controls.AddRange(new Control[]
        {
            toolsBorderBot,
            lblBuscar, cmbCampoBusqueda, txtBusqueda, btnFiltrar, btnLimpiarFiltro,
            toolsDiv,
            lblOrdenar, cmbCampoOrden, rbAscendente, rbDescendente, btnOrdenar,
            lblContadorTodos
        });

        dgvTodos = BuildGrid(clrBg, clrSurface, clrBorder, clrText, clrTextDim, clrMint);
        dgvTodos.Dock = DockStyle.Fill;
        tabTodos.Controls.Add(dgvTodos);
        tabTodos.Controls.Add(pnlToolsTodos);

        // ════════════════════════════════════════════════════════
        //  TAB 2 – BY CATEGORY
        // ════════════════════════════════════════════════════════
        splitCategoria = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Panel1MinSize = 180,
            FixedPanel = FixedPanel.Panel1,
            BackColor = clrBg,
            SplitterWidth = 1
        };
        splitCategoria.SplitterDistance = 220;

        var pnlCatLeft = splitCategoria.Panel1;
        pnlCatLeft.BackColor = clrSurface;

        var lblCatTitle = new Label
        {
            Text = "Categorías",
            Dock = DockStyle.Top,
            Height = 44,
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
            ForeColor = clrText,
            BackColor = clrSurface,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0)
        };
        var catTitleBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = clrBorder };

        lstCategorias = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = clrSurface,
            ForeColor = clrText,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9f),
            ItemHeight = 30,
            DrawMode = DrawMode.OwnerDrawFixed
        };
        lstCategorias.DrawItem += LstCat_DrawItem!;
        lstCategorias.SelectedIndexChanged += LstCategorias_SelectedIndexChanged!;

        pnlCatLeft.Controls.AddRange(new Control[] { lstCategorias, catTitleBorder, lblCatTitle });

        lblCatInfo = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = clrTextDim,
            BackColor = clrSurface,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0)
        };
        var catInfoBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = clrBorder };

        dgvCategoria = BuildGrid(clrBg, clrSurface, clrBorder, clrText, clrTextDim, clrAmber);
        dgvCategoria.Dock = DockStyle.Fill;
        splitCategoria.Panel2.BackColor = clrBg;
        splitCategoria.Panel2.Controls.AddRange(new Control[] { dgvCategoria, catInfoBorder, lblCatInfo });
        tabCategoria.Controls.Add(splitCategoria);

        // ════════════════════════════════════════════════════════
        //  TAB 3 – STATISTICS
        // ════════════════════════════════════════════════════════
        pnlStatsTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,  // hidden placeholder (stats shown in top stats bar now)
            BackColor = clrBg
        };
        dgvEstadisticas = BuildGrid(clrBg, clrSurface, clrBorder, clrText, clrTextDim, clrSky);
        dgvEstadisticas.Dock = DockStyle.Fill;
        tabEstadisticas.Controls.Add(dgvEstadisticas);
        tabEstadisticas.Controls.Add(pnlStatsTop);

        // ════════════════════════════════════════════════════════
        //  TAB 4 – CHARTS
        // ════════════════════════════════════════════════════════
        pnlGraficasTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = clrSurface
        };
        var grafBorderBot = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = clrBorder };

        // All controls placed at Y=10, height=26. X positions calculated left→right.
        const int gy = 10;   // top Y for all controls
        const int gh = 26;   // fixed height for combos and button
        int gx = 14;         // running X cursor

        lblTipoGrafica = new Label
        {
            Text = "Tipo",
            Location = new Point(gx, gy + 4),
            AutoSize = true,
            ForeColor = clrTextDim,
            Font = new Font("Segoe UI", 8.5f)
        };
        gx += lblTipoGrafica.PreferredWidth + 6;

        cmbTipoGrafica = new ComboBox
        {
            Location = new Point(gx, gy),
            Width = 110,
            Height = gh,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = clrSurface2,
            ForeColor = clrText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f)
        };
        cmbTipoGrafica.Items.AddRange(new object[] { "Columnas", "Barras", "Pastel" });
        cmbTipoGrafica.SelectedIndex = 0;
        cmbTipoGrafica.SelectedIndexChanged += CmbTipoGrafica_SelectedIndexChanged!;
        gx += 110 + 10;

        btnActualizarGrafica = new Button
        {
            Text = "↺",
            Location = new Point(gx, gy),
            Width = 34,
            Height = gh,
            FlatStyle = FlatStyle.Flat,
            BackColor = clrSurface2,
            ForeColor = clrMint,
            Font = new Font("Segoe UI", 11f),
            Padding = new Padding(0),
            Cursor = Cursors.Hand
        };
        btnActualizarGrafica.FlatAppearance.BorderSize = 0;
        btnActualizarGrafica.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 30, 44);
        btnActualizarGrafica.Click += BtnActualizarGrafica_Click!;
        gx += 34 + 18;

        lblGrupoGrafica = new Label
        {
            Text = "Agrupar por",
            Location = new Point(gx, gy + 4),
            AutoSize = true,
            ForeColor = clrTextDim,
            Font = new Font("Segoe UI", 8.5f)
        };
        gx += lblGrupoGrafica.PreferredWidth + 6;

        cmbGrupoGrafica = new ComboBox
        {
            Location = new Point(gx, gy),
            Width = 170,
            Height = gh,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = clrSurface2,
            ForeColor = clrText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f)
        };
        cmbGrupoGrafica.SelectedIndexChanged += CmbGrupoGrafica_SelectedIndexChanged!;
        gx += 170 + 18;

        lblMetricaGrafica = new Label
        {
            Text = "Métrica",
            Location = new Point(gx, gy + 4),
            AutoSize = true,
            ForeColor = clrTextDim,
            Font = new Font("Segoe UI", 8.5f)
        };
        gx += lblMetricaGrafica.PreferredWidth + 6;

        cmbMetricaGrafica = new ComboBox
        {
            Location = new Point(gx, gy),
            Width = 180,
            Height = gh,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = clrSurface2,
            ForeColor = clrText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f)
        };
        cmbMetricaGrafica.SelectedIndexChanged += CmbMetricaGrafica_SelectedIndexChanged!;

        pnlGraficasTop.Controls.AddRange(new Control[]
        {
            grafBorderBot,
            lblTipoGrafica, cmbTipoGrafica,
            btnActualizarGrafica,
            lblGrupoGrafica, cmbGrupoGrafica,
            lblMetricaGrafica, cmbMetricaGrafica
        });

        chartMain = new ChartPanel { Dock = DockStyle.Fill };
        tabGraficas.Controls.Add(chartMain);
        tabGraficas.Controls.Add(pnlGraficasTop);

        // ════════════════════════════════════════════════════════
        //  TAB 5 – PROCESSING
        // ════════════════════════════════════════════════════════
        pnlProcHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 104,
            BackColor = clrSurface,
            Padding = new Padding(0)
        };
        var procBorderBot = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = clrBorder };

        // Row 1 – Duplicates
        var rowDupes = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(4000, 52),
            BackColor = clrSurface
        };
        btnDetectarDuplicados = FButton("Detectar duplicados", new Point(16, 12), 168,
            Color.FromArgb(30, 80, 50), clrMint, BtnDetectarDuplicados_Click!);
        btnEliminarDuplicados = FButton("Eliminar duplicados", new Point(194, 12), 168,
            Color.FromArgb(70, 20, 30), clrRose, BtnEliminarDuplicados_Click!);
        btnEliminarDuplicados.Enabled = false;
        lblProcInfo = new Label
        {
            Text = "Selecciona una operación para comenzar.",
            AutoSize = false,
            Size = new Size(800, 26),
            Location = new Point(376, 14),
            ForeColor = clrTextDim,
            Font = new Font("Segoe UI", 8.5f),
            BackColor = Color.Transparent
        };
        rowDupes.Controls.AddRange(new Control[]
            { btnDetectarDuplicados, btnEliminarDuplicados, lblProcInfo });

        var rowSep = new Panel { Location = new Point(0, 52), Size = new Size(4000, 1), BackColor = clrBorder };

        // Row 2 – LINQ
        var rowLinq = new Panel
        {
            Location = new Point(0, 53),
            Size = new Size(4000, 50),
            BackColor = clrSurface
        };
        var lblLinqTag = new Label
        {
            Text = "LINQ",
            Location = new Point(16, 14),
            AutoSize = true,
            ForeColor = clrAmber,
            Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        var lblCampoL = FLabel("Campo", new Point(68, 16), clrTextDim);
        lblCampoL.Location = new Point(68, 16);
        cmbLinqCampo = FCombo(new Point(130, 12), 120, clrSurface2, clrText,
            new object[] { }, -1);
        var lblBuscarL = FLabel("Buscar", new Point(268, 16), clrTextDim);
        txtLinqFiltro = new TextBox
        {
            Location = new Point(322, 12),
            //Location = new Point(300, 12),
            Width = 155,
            BackColor = clrSurface2,
            ForeColor = clrText,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f)
        };
        btnLinqWhere = FButton(".Where()", new Point(492, 12), 90, Color.FromArgb(10, 50, 90), clrSky, BtnLinqWhere_Click!);
        btnLinqGroupBy = FButton(".GroupBy()", new Point(590, 12), 90, Color.FromArgb(30, 60, 20), clrMint, BtnLinqGroupBy_Click!);
        btnLinqOrderBy = FButton(".OrderBy()", new Point(688, 12), 90, Color.FromArgb(50, 30, 80), clrPurple, BtnLinqOrderBy_Click!);
        btnLinqLimpiar = FButton("Limpiar", new Point(786, 12), 72, clrSurface2, clrTextDim, BtnLinqLimpiar_Click!);
        rowLinq.Controls.AddRange(new Control[]
        {
            lblLinqTag, lblCampoL, cmbLinqCampo, lblBuscarL,
            txtLinqFiltro, btnLinqWhere, btnLinqGroupBy, btnLinqOrderBy, btnLinqLimpiar
        });

        pnlProcHeader.Controls.AddRange(new Control[] { procBorderBot, rowSep, rowLinq, rowDupes });

        dgvProcesamiento = BuildGrid(clrBg, clrSurface, clrBorder, clrText, clrTextDim, clrPurple);
        dgvProcesamiento.Dock = DockStyle.Fill;
        tabProcesamiento.Controls.Add(dgvProcesamiento);
        tabProcesamiento.Controls.Add(pnlProcHeader);

        // ════════════════════════════════════════════════════════
        //  ASSEMBLE MAIN PANEL
        // ════════════════════════════════════════════════════════
        pnlMain.Controls.Add(tabControl1);
        pnlMain.Controls.Add(pnlTopStats);

        // ════════════════════════════════════════════════════════
        //  FORM LAYOUT
        // ════════════════════════════════════════════════════════
        Controls.Add(pnlMain);
        Controls.Add(pnlSidebar);
        Controls.Add(menuStrip1);
        Controls.Add(statusStrip1);
        ResumeLayout(false);
        PerformLayout();
    }

    // ═══════════════════════════════════════════════════════════
    //  FACTORY HELPERS
    // ═══════════════════════════════════════════════════════════

    private static DataGridView BuildGrid(Color bg, Color surface, Color border,
        Color text, Color textDim, Color accent)
    {
        var dgv = new DataGridView
        {
            AutoGenerateColumns = false,
            BackgroundColor = bg,
            GridColor = border,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            RowTemplate = { Height = 28 },
            ScrollBars = ScrollBars.Both,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
        };
        dgv.DefaultCellStyle.BackColor = bg;
        dgv.DefaultCellStyle.ForeColor = text;
        dgv.DefaultCellStyle.Font = new Font("Consolas", 8.5f);
        dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 90, 160);
        dgv.DefaultCellStyle.SelectionForeColor = Color.White;
        dgv.AlternatingRowsDefaultCellStyle.BackColor = surface;
        dgv.ColumnHeadersDefaultCellStyle.BackColor = surface;
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = accent;
        dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
        dgv.ColumnHeadersHeight = 36;
        dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        dgv.EnableHeadersVisualStyles = false;

        typeof(DataGridView)
            .GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(dgv, true);

        return dgv;
    }

    private static Label FLabel(string text, Point loc, Color fore) =>
        new Label
        {
            Text = text,
            Location = loc,
            AutoSize = true,
            ForeColor = fore,
            Font = new Font("Segoe UI", 8.5f),
            BackColor = Color.Transparent
        };

    private static Button FButton(string text, Point loc, int width,
        Color bg, Color fg, EventHandler click)
    {
        var b = new Button
        {
            Text = text,
            Location = loc,
            Width = width,
            Height = 28,
            BackColor = bg,
            ForeColor = fg,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg, 0.1f);
        b.Click += click;
        return b;
    }

    private static RadioButton FRadio(string text, Point loc, bool chk, Color bg) =>
        new RadioButton
        {
            Text = text,
            Location = loc,
            AutoSize = true,
            Checked = chk,
            ForeColor = Color.FromArgb(160, 160, 185),
            BackColor = bg,
            Font = new Font("Segoe UI", 8.5f)
        };

    private static ComboBox FCombo(Point loc, int width, Color bg, Color fg,
        object[] items, int sel)
    {
        var c = new ComboBox
        {
            Location = loc,
            Width = width,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = bg,
            ForeColor = fg,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f)
        };
        c.Items.AddRange(items);
        if (sel >= 0 && items.Length > sel) c.SelectedIndex = sel;
        return c;
    }

    private static ToolStripMenuItem MI(string text, Color fore, EventHandler? click = null)
    {
        var m = new ToolStripMenuItem(text) { ForeColor = fore };
        if (click != null) m.Click += click;
        return m;
    }

    // ═══════════════════════════════════════════════════════════
    //  CUSTOM TAB DRAWING
    // ═══════════════════════════════════════════════════════════
    private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
    {
        var clrBg = Color.FromArgb(13, 13, 18);
        var clrSurface = Color.FromArgb(20, 20, 28);
        var clrText = Color.FromArgb(225, 225, 235);
        var clrTextDim = Color.FromArgb(95, 95, 125);
        var clrMint = Color.FromArgb(52, 211, 153);
        var clrBorder = Color.FromArgb(36, 36, 52);

        var tab = tabControl1;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var rect = tab.GetTabRect(e.Index);
        bool selected = e.State.HasFlag(DrawItemState.Selected);
        string text = tab.TabPages[e.Index].Text;

        // Tab background
        using var bgBrush = new SolidBrush(selected ? clrBg : clrSurface);
        g.FillRectangle(bgBrush, rect);

        // Bottom border accent for selected tab
        if (selected)
        {
            using var mintBrush = new SolidBrush(clrMint);
            g.FillRectangle(mintBrush, rect.X, rect.Bottom - 2, rect.Width, 2);
        }

        // Text
        using var textBrush = new SolidBrush(selected ? clrText : clrTextDim);
        using var font = new Font("Segoe UI", 9f, selected ? FontStyle.Bold : FontStyle.Regular);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, textBrush, rect, sf);
    }

    // ═══════════════════════════════════════════════════════════
    //  CUSTOM LIST BOX DRAWING
    // ═══════════════════════════════════════════════════════════
    private void LstCat_DrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var clrSurface = Color.FromArgb(20, 20, 28);
        var clrText = Color.FromArgb(225, 225, 235);
        var clrMint = Color.FromArgb(52, 211, 153);
        var clrBorder = Color.FromArgb(36, 36, 52);

        bool selected = (e.State & DrawItemState.Selected) != 0;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var bgBrush = new SolidBrush(selected ? Color.FromArgb(28, 60, 44) : clrSurface);
        g.FillRectangle(bgBrush, e.Bounds);

        if (selected)
        {
            using var accentBrush = new SolidBrush(clrMint);
            g.FillRectangle(accentBrush, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
        }

        string text = lstCategorias.Items[e.Index]?.ToString() ?? "";
        using var textBrush = new SolidBrush(selected ? clrMint : clrText);
        using var font = new Font("Segoe UI", 9f, selected ? FontStyle.Bold : FontStyle.Regular);
        var textRect = new Rectangle(e.Bounds.X + 14, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height);
        var sf = new StringFormat { LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, textBrush, textRect, sf);

        // Divider
        using var borderPen = new Pen(clrBorder, 1);
        g.DrawLine(borderPen, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
    }
}

// ════════════════════════════════════════════════════════════════
//  DARK TAB CONTROL — removes the white system border
// ════════════════════════════════════════════════════════════════
class DarkTabControl : TabControl
{
    private static readonly Color BgColor = Color.FromArgb(13, 13, 18);
    private static readonly Color SurfaceColor = Color.FromArgb(20, 20, 28);
    private static readonly Color BorderColor = Color.FromArgb(36, 36, 52);

    public DarkTabControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;

        // 1. Fill entire control background dark
        g.Clear(BgColor);

        // 2. Fill the tab strip area (top row of tabs) with surface color
        int tabH = ItemSize.Height + 2;
        var tabStrip = new Rectangle(0, 0, Width, tabH);
        using (var sb = new SolidBrush(SurfaceColor))
            g.FillRectangle(sb, tabStrip);

        // 3. Draw a bottom border under the tab strip
        using (var bp = new Pen(BorderColor, 1))
            g.DrawLine(bp, 0, tabH, Width, tabH);

        // 4. Draw each tab
        for (int i = 0; i < TabCount; i++)
        {
            var rect = GetTabRect(i);
            bool sel = SelectedIndex == i;
            string txt = TabPages[i].Text;

            // Tab background
            using (var tb = new SolidBrush(sel ? BgColor : SurfaceColor))
                g.FillRectangle(tb, rect);

            // Bottom accent line on selected tab
            if (sel)
            {
                using var mb = new SolidBrush(Color.FromArgb(52, 211, 153));
                g.FillRectangle(mb, rect.X, rect.Bottom - 2, rect.Width, 2);
            }

            // Tab text
            using var tf = new SolidBrush(sel
                ? Color.FromArgb(225, 225, 235)
                : Color.FromArgb(95, 95, 125));
            using var font = new Font("Segoe UI", 9f,
                sel ? FontStyle.Bold : FontStyle.Regular);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(txt, font, tf, rect, sf);
        }

        // 5. Fill the rest of the tab strip to the right of the last tab
        if (TabCount > 0)
        {
            var lastTab = GetTabRect(TabCount - 1);
            int fillX = lastTab.Right;
            int fillW = Width - fillX;
            if (fillW > 0)
            {
                using var fb = new SolidBrush(SurfaceColor);
                g.FillRectangle(fb, fillX, 0, fillW, tabH);
            }
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Suppress default background paint — OnPaint handles everything
    }
}

// ════════════════════════════════════════════════════════════════
//  MINIMAL MENU RENDERER
// ════════════════════════════════════════════════════════════════
class MinimalMenuRenderer : ToolStripProfessionalRenderer
{
    public MinimalMenuRenderer() : base(new MinimalColorTable()) { }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.ForeColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected) return;
        var g = e.Graphics;
        var r = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
        using var b = new SolidBrush(Color.FromArgb(32, 211, 153, 80));
        g.FillRectangle(b, r);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var g = e.Graphics;
        int y = e.Item.Height / 2;
        using var p = new Pen(Color.FromArgb(36, 36, 52));
        g.DrawLine(p, 8, y, e.Item.Width - 8, y);
    }
}

class MinimalColorTable : ProfessionalColorTable
{
    private static readonly Color Bg = Color.FromArgb(16, 16, 23);
    private static readonly Color Surface = Color.FromArgb(26, 26, 36);
    private static readonly Color Border = Color.FromArgb(36, 36, 52);
    private static readonly Color Hover = Color.FromArgb(28, 60, 44);

    public override Color MenuItemSelected => Hover;
    public override Color MenuItemBorder => Border;
    public override Color MenuBorder => Border;
    public override Color MenuItemSelectedGradientBegin => Hover;
    public override Color MenuItemSelectedGradientEnd => Hover;
    public override Color MenuItemPressedGradientBegin => Surface;
    public override Color MenuItemPressedGradientEnd => Surface;
    public override Color ToolStripDropDownBackground => Bg;
    public override Color ImageMarginGradientBegin => Bg;
    public override Color ImageMarginGradientMiddle => Bg;
    public override Color ImageMarginGradientEnd => Bg;
    public override Color MenuStripGradientBegin => Bg;
    public override Color MenuStripGradientEnd => Bg;
    public override Color SeparatorDark => Border;
    public override Color SeparatorLight => Border;
    public override Color StatusStripGradientBegin => Surface;
    public override Color StatusStripGradientEnd => Surface;
    public override Color ToolStripBorder => Border;
    public override Color ToolStripGradientBegin => Surface;
    public override Color ToolStripGradientMiddle => Surface;
    public override Color ToolStripGradientEnd => Surface;
}