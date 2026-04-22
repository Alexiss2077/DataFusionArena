using DataFusionArena.Shared.Models;
using DataFusionArena.Shared.Readers;
using DataFusionArena.Shared.Database;
using DataFusionArena.Shared.Processing;

namespace DataFusionArena.ConsoleApp;

class Program
{
    static readonly List<DataItem> _datos = new();
    static Dictionary<string, List<DataItem>> _porCategoria = new();
    static Dictionary<int, DataItem> _porId = new();

    static List<(string Display, string Clave, int Ancho)> _columnas = ObtenerColumnasDefault();

    static List<string> _ultimasColumnasBD = new();
    static Dictionary<string, string> _ultimoMapeoBD = new();
    static string _ultimaFuenteBD = "";

    static readonly string _dirDatos = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleData");

    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "Data Fusion Arena – Consola";

        MostrarBanner();

        bool continuar = true;
        while (continuar)
            continuar = MostrarMenu();

        Console.WriteLine("\n¡Hasta luego! 🎮\n");
    }

    // ══════════════════════════════════════════════════════════════
    //  DETECCIÓN DINÁMICA DE COLUMNAS
    // ══════════════════════════════════════════════════════════════

    static List<(string Display, string Clave, int Ancho)> ObtenerColumnasDefault()
    {
        return new()
        {
            ("ID", "id", 6), ("Nombre", "nombre", 28), ("Categoría", "categoria", 18),
            ("Valor", "valor", 10), ("Fecha", "fecha", 12), ("Fuente", "fuente", 12)
        };
    }

    static void ReconstruirColumnas()
    {
        _columnas = new List<(string Display, string Clave, int Ancho)>();

        var todasExtras = _datos
            .SelectMany(d => d.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k)
            .ToList();

        string ultimaFuente = _datos.Count > 0 ? _datos[^1].Fuente : "";

        List<string>? readerCols = null;
        Dictionary<string, string>? readerMapeo = null;

        if (ultimaFuente == "csv" && CsvDataReader.UltimasColumnas.Count > 0)
        {
            readerCols = CsvDataReader.UltimasColumnas;
            readerMapeo = CsvDataReader.MapeoColumnas;
        }
        else if (ultimaFuente == "json" && JsonDataReader.UltimasColumnas.Count > 0)
        {
            readerCols = JsonDataReader.UltimasColumnas;
            readerMapeo = JsonDataReader.MapeoColumnas;
        }
        else if (ultimaFuente == "xml" && XmlDataReader.UltimasColumnas.Count > 0)
        {
            readerCols = XmlDataReader.UltimasColumnas;
            readerMapeo = XmlDataReader.MapeoColumnas;
        }
        else if (ultimaFuente == "txt" && TxtDataReader.UltimasColumnas.Count > 0)
        {
            readerCols = TxtDataReader.UltimasColumnas;
            readerMapeo = TxtDataReader.MapeoColumnas;
        }
        else if ((ultimaFuente == "mariadb" || ultimaFuente == "postgresql")
&& _ultimasColumnasBD.Count > 0)
        {
            readerCols = _ultimasColumnasBD;
            readerMapeo = _ultimoMapeoBD;
        }

        if (readerCols != null && readerMapeo != null)
        {
            var yaAgregadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool usarCamposExtraDirecto = ultimaFuente == "txt";
            foreach (var col in readerCols)
            {
                string clave = usarCamposExtraDirecto
                    ? col.ToLowerInvariant()
                    : (readerMapeo.TryGetValue(col, out var prop)
                        ? prop.ToLower()
                        : col.ToLowerInvariant());
                _columnas.Add((col, clave, EstimarAncho(clave, col)));
                yaAgregadas.Add(col.ToLowerInvariant());
            }
            if (!yaAgregadas.Contains("fuente"))
                _columnas.Add(("Fuente", "fuente", 12));
            foreach (var k in todasExtras.Where(k => !yaAgregadas.Contains(k.ToLowerInvariant())))
                _columnas.Add((k, k.ToLowerInvariant(), Math.Max(k.Length + 2, 8)));
        }
        else
        {
            _columnas.Add(("ID", "id", 6));
            _columnas.Add(("Nombre", "nombre", 28));
            _columnas.Add(("Categoría", "categoria", 18));
            _columnas.Add(("Valor", "valor", 10));
            _columnas.Add(("Fecha", "fecha", 12));
            _columnas.Add(("Fuente", "fuente", 12));
            var yaAgregadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "id", "nombre", "categoria", "valor", "fecha", "fuente" };
            foreach (var k in todasExtras.Where(k => !yaAgregadas.Contains(k.ToLowerInvariant())))
                _columnas.Add((k, k.ToLowerInvariant(), Math.Max(k.Length + 2, 8)));
        }

        AjustarAnchosAlContenido();
    }

    static void AjustarAnchosAlContenido()
    {
        if (_datos.Count == 0) return;
        var muestra = _datos.Count > 200 ? _datos.Take(200).ToList() : _datos;

        const int MAX_ANCHO = 50;

        for (int c = 0; c < _columnas.Count; c++)
        {
            var (display, clave, _) = _columnas[c];
            int maxLen = display.Length;
            foreach (var item in muestra)
            {
                int len = ObtenerValorCelda(item, clave).Length;
                if (len > maxLen) maxLen = len;
            }
            int minAncho = Math.Max(display.Length, 4);
            int anchoFinal = maxLen > MAX_ANCHO ? MAX_ANCHO : Math.Max(maxLen, minAncho);
            _columnas[c] = (display, clave, anchoFinal);
        }
    }

    static int EstimarAncho(string clave, string display) => clave switch
    {
        "id" => Math.Max(6, display.Length),
        "nombre" => Math.Max(20, display.Length),
        "categoria" => Math.Max(15, display.Length),
        "valor" => Math.Max(10, display.Length),
        "fecha" => Math.Max(12, display.Length),
        "fuente" => Math.Max(10, display.Length),
        _ => Math.Max(12, display.Length)
    };

    static string ObtenerValorCelda(DataItem item, string clave) => clave switch
    {
        "id" => item.Id.ToString(),
        "nombre" => item.Nombre,
        "categoria" => item.Categoria,
        "valor" => item.Valor.ToString("F2"),
        "fecha" => item.Fecha == new DateTime(item.Fecha.Year, 1, 1)
                           ? item.Fecha.Year.ToString()
                           : item.Fecha.ToString("yyyy-MM-dd"),
        "fuente" => item.Fuente,
        _ => BuscarExtra(item, clave)
    };

    static string BuscarExtra(DataItem item, string clave)
    {
        if (item.CamposExtra.TryGetValue(clave, out var v)) return v;
        foreach (var kv in item.CamposExtra)
            if (string.Equals(kv.Key, clave, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        return "";
    }

    // Devuelve los nombres originales del dataset (Display) para mostrar al usuario
    static List<string> ObtenerCamposDisponibles() =>
        _columnas.Select(c => c.Display).ToList();

    // Traduce el Display elegido por el usuario a la Clave interna para DataProcessor
    static string ResolverClave(string displayOClave)
    {
        var col = _columnas.FirstOrDefault(c =>
            string.Equals(c.Display, displayOClave, StringComparison.OrdinalIgnoreCase));
        if (col != default) return col.Clave;
        return displayOClave.ToLowerInvariant();
    }

    // ══════════════════════════════════════════════════════════════
    //  BANNER Y MENÚ
    // ══════════════════════════════════════════════════════════════

    static void MostrarBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
██████╗  █████╗ ████████╗ █████╗     ███████╗██╗   ██╗███████╗██╗ ██████╗ ███╗   ██╗
██╔══██╗██╔══██╗╚══██╔══╝██╔══██╗    ██╔════╝██║   ██║██╔════╝██║██╔═══██╗████╗  ██║
██║  ██║███████║   ██║   ███████║    █████╗  ██║   ██║███████╗██║██║   ██║██╔██╗ ██║
██║  ██║██╔══██║   ██║   ██╔══██║    ██╔══╝  ██║   ██║╚════██║██║██║   ██║██║╚██╗██║
██████╔╝██║  ██║   ██║   ██║  ██║    ██║     ╚██████╔╝███████║██║╚██████╔╝██║ ╚████║
╚═════╝ ╚═╝  ╚═╝   ╚═╝   ╚═╝  ╚═╝    ╚═╝      ╚═════╝ ╚══════╝╚═╝ ╚═════╝ ╚═╝  ╚═══╝
                         Administración y Organización de Datos
");
        Console.ResetColor();
    }

    static bool MostrarMenu()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n{'─',55}");
        Console.WriteLine($"  Registros cargados: {_datos.Count,-6} | Categorías: {_porCategoria.Count}");
        Console.WriteLine($"{'─',55}");
        Console.ResetColor();

        Console.WriteLine("  [1] 📂  Cargar archivos (JSON / CSV / XML / TXT)");
        Console.WriteLine("  [2] 🗄️   Conectar a bases de datos");
        Console.WriteLine("  [3] 📋  Ver todos los datos (tabla)");
        Console.WriteLine("  [4] 🔍  Filtrar datos");
        Console.WriteLine("  [5] 🔃  Ordenar datos");
        Console.WriteLine("  [6] 📦  Ver por categoría (Dictionary)");
        Console.WriteLine("  [7] 🔢  Estadísticas por categoría");
        Console.WriteLine("  [8] 📊  Gráfica de barras en consola");
        Console.WriteLine("  [9] 🧹  Detectar y eliminar duplicados");
        Console.WriteLine("  [E] 💾  Exportar datos");
        Console.WriteLine("  [L] ⚡  Bonus: operaciones LINQ");
        Console.WriteLine("  [0] 🚪  Salir");
        Console.Write("\n  Opción: ");

        string? op = Console.ReadLine()?.Trim().ToUpper();
        Console.WriteLine();

        switch (op)
        {
            case "1": CargarArchivos(); break;
            case "2": ConectarBD(); break;
            case "3": VerTodos(); break;
            case "4": FiltrarDatos(); break;
            case "5": OrdenarDatos(); break;
            case "6": VerPorCategoria(); break;
            case "7": MostrarEstadisticas(); break;
            case "8": GraficaBarras(); break;
            case "9": GestionarDuplicados(); break;
            case "E": ExportarDatos(); break;
            case "L": BonusLinq(); break;
            case "0": return false;
            default: Color(ConsoleColor.Red, "  ⚠  Opción no válida.\n"); break;
        }
        return true;
    }

    // ══════════════════════════════════════════════════════════════
    //  NIVEL 1 y 2 – Carga de archivos
    // ══════════════════════════════════════════════════════════════

    static void CargarArchivos()
    {
        Titulo("CARGAR ARCHIVOS DE DATOS");
        Console.WriteLine($"  Directorio de datos: {_dirDatos}\n");
        Console.WriteLine("  [1] JSON  – products.json");
        Console.WriteLine("  [2] CSV   – sales.csv");
        Console.WriteLine("  [3] XML   – employees.xml");
        Console.WriteLine("  [4] TXT   – records.txt");
        Console.WriteLine("  [5] Cargar TODOS los archivos");
        Console.WriteLine("  [6] Cargar archivo personalizado");
        Console.Write("\n  Opción: ");

        string? op = Console.ReadLine()?.Trim();
        Console.WriteLine();

        switch (op)
        {
            case "1": CargarJson(Path.Combine(_dirDatos, "products.json")); break;
            case "2": CargarCsv(Path.Combine(_dirDatos, "sales.csv")); break;
            case "3": CargarXml(Path.Combine(_dirDatos, "employees.xml")); break;
            case "4": CargarTxt(Path.Combine(_dirDatos, "records.txt")); break;
            case "5":
                CargarJson(Path.Combine(_dirDatos, "products.json"));
                CargarCsv(Path.Combine(_dirDatos, "sales.csv"));
                CargarXml(Path.Combine(_dirDatos, "employees.xml"));
                CargarTxt(Path.Combine(_dirDatos, "records.txt"));
                break;
            case "6": CargarArchivoPersonalizado(); break;
        }

        ActualizarIndices();
        ReconstruirColumnas();
        Color(ConsoleColor.Green, $"\n  ✅ Total acumulado: {_datos.Count} registros en memoria.");
    }

    static void CargarArchivoPersonalizado()
    {
        Console.WriteLine("  Ingresa la ruta del archivo (puedes arrastrar el archivo aquí):");
        Console.Write("  > ");
        string? input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        { Color(ConsoleColor.Yellow, "  Ruta vacía, operación cancelada."); return; }

        string ruta = input.Trim().Trim('"').Trim('\'').Trim();
        if (!File.Exists(ruta))
        { Color(ConsoleColor.Red, $"  Archivo no encontrado:\n     {ruta}"); return; }

        string ext = Path.GetExtension(ruta).ToLowerInvariant();
        Color(ConsoleColor.Cyan, $"  Archivo detectado: {Path.GetFileName(ruta)} | Extensión: {ext}");

        switch (ext)
        {
            case ".json": CargarJson(ruta); break;
            case ".csv": CargarCsv(ruta); break;
            case ".xml": CargarXml(ruta); break;
            case ".txt": CargarTxt(ruta); break;
            default:
                Color(ConsoleColor.Red, $"  Extensión '{ext}' no soportada. Formatos: .json .csv .xml .txt");
                break;
        }
    }

    static void CargarJson(string ruta) =>
        DataProcessor.AgregarDatos(_datos, JsonDataReader.Leer(ruta));

    static void CargarCsv(string ruta) =>
        DataProcessor.AgregarDatos(_datos, CsvDataReader.Leer(ruta));

    static void CargarXml(string ruta)
    {
        var nuevos = XmlDataReader.Leer(ruta);
        foreach (var item in nuevos)
        {
            if (item.CamposExtra.TryGetValue("departamento", out var dep))
            { item.Categoria = dep; item.CamposExtra.Remove("departamento"); }
            if (item.CamposExtra.TryGetValue("salario", out var sal) && double.TryParse(sal, out double s))
            { item.Valor = s; item.CamposExtra.Remove("salario"); }
        }
        DataProcessor.AgregarDatos(_datos, nuevos);
    }

    static void CargarTxt(string ruta) =>
        DataProcessor.AgregarDatos(_datos, TxtDataReader.Leer(ruta));

    // ══════════════════════════════════════════════════════════════
    //  NIVEL 3 – Bases de datos
    // ══════════════════════════════════════════════════════════════

    static void ConectarBD()
    {
        Titulo("CONEXIÓN A BASES DE DATOS");
        Console.WriteLine("  [1] PostgreSQL");
        Console.WriteLine("  [2] MariaDB");
        Console.Write("\n  Opción: ");
        string? op = Console.ReadLine()?.Trim();

        if (op == "1")
        {
            Console.Write("\n  Host   (default: localhost): ");
            string host = Console.ReadLine()?.Trim() is { Length: > 0 } h ? h : "localhost";
            Console.Write("  Puerto (default: 5432):      ");
            string port = Console.ReadLine()?.Trim() is { Length: > 0 } p ? p : "5432";
            Console.Write("  Base de datos:               ");
            string db = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(db)) { Color(ConsoleColor.Yellow, "  Base de datos vacía, cancelado."); return; }
            Console.Write("  Usuario (default: postgres):  ");
            string user = Console.ReadLine()?.Trim() is { Length: > 0 } u ? u : "postgres";
            Console.Write("  Contraseña:                  ");
            string pass = LeerContraseña();
            Console.Write("  Nombre de tabla:             ");
            string tabla = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(tabla)) { Color(ConsoleColor.Yellow, "  Tabla vacía, cancelado."); return; }

            string cadena = $"Host={host};Port={port};Database={db};Username={user};Password={pass};";
            var pg = new PostgreSqlConnector(cadena, tabla);
            Console.WriteLine("\n  Probando conexión...");
            if (pg.ProbarConexion(out string msg))
            {
                Color(ConsoleColor.Green, $"  {msg}");
                var datos = pg.LeerDatos();
                _ultimasColumnasBD = pg.UltimasColumnas;
                _ultimoMapeoBD = pg.MapeoColumnas;
                _ultimaFuenteBD = "postgresql";
                DataProcessor.AgregarDatos(_datos, datos);
                ActualizarIndices();
                ReconstruirColumnas();
                Color(ConsoleColor.Green, $"\n  ✅ {datos.Count} registros cargados desde PostgreSQL.");
            }
            else Color(ConsoleColor.Red, $"  ❌ {msg}");
        }
        else if (op == "2")
        {
            Console.Write("\n  Host   (default: localhost): ");
            string host = Console.ReadLine()?.Trim() is { Length: > 0 } h ? h : "localhost";
            Console.Write("  Puerto (default: 3306):      ");
            string port = Console.ReadLine()?.Trim() is { Length: > 0 } p ? p : "3306";
            Console.Write("  Base de datos:               ");
            string db = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(db)) { Color(ConsoleColor.Yellow, "  Base de datos vacía, cancelado."); return; }
            Console.Write("  Usuario (default: root):     ");
            string user = Console.ReadLine()?.Trim() is { Length: > 0 } u ? u : "root";
            Console.Write("  Contraseña:                  ");
            string pass = LeerContraseña();
            Console.Write("  Nombre de tabla:             ");
            string tabla = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(tabla)) { Color(ConsoleColor.Yellow, "  Tabla vacía, cancelado."); return; }

            string cadena = $"Server={host};Port={port};Database={db};User={user};Password={pass};";
            var md = new MariaDbConnector(cadena, tabla);
            Console.WriteLine("\n  Probando conexión...");
            if (md.ProbarConexion(out string msg))
            {
                Color(ConsoleColor.Green, $"  {msg}");
                var datos = md.LeerDatos();
                _ultimasColumnasBD = md.UltimasColumnas;
                _ultimoMapeoBD = md.MapeoColumnas;
                _ultimaFuenteBD = "mariadb";
                DataProcessor.AgregarDatos(_datos, datos);
                ActualizarIndices();
                ReconstruirColumnas();
                Color(ConsoleColor.Green, $"\n  ✅ {datos.Count} registros cargados desde MariaDB.");
            }
            else Color(ConsoleColor.Red, $"  ❌ {msg}");
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  NIVEL 6 – Tabla DINÁMICA
    // ══════════════════════════════════════════════════════════════

    static void VerTodos()
    {
        Titulo($"TODOS LOS DATOS ({_datos.Count} registros)");
        if (_datos.Count == 0) { Color(ConsoleColor.Yellow, "  Sin datos. Usa opción [1] para cargar."); return; }
        ImprimirTabla(_datos);
        Console.Write("\n  ENTER para continuar...");
        Console.ReadLine();
    }

    static void ImprimirTabla(List<DataItem> lista, int maxFilas = 50)
    {
        if (lista.Count == 0) { Color(ConsoleColor.Yellow, "  Sin registros."); return; }

        int anchoTotal = _columnas.Sum(c => c.Ancho) + (_columnas.Count - 1) * 3 + 2;
        string sep = new string('─', anchoTotal);

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("  ");
        for (int c = 0; c < _columnas.Count; c++)
        {
            var (display, clave, ancho) = _columnas[c];
            string hdr = display.Length > ancho ? display[..ancho] : display;
            string celda = clave is "valor" or "id" ? hdr.PadLeft(ancho) : hdr.PadRight(ancho);
            Console.Write(celda);
            if (c < _columnas.Count - 1) Console.Write(" │ ");
        }
        Console.WriteLine();
        Console.WriteLine($"  {sep}");
        Console.ResetColor();

        int mostrados = 0;
        foreach (var item in lista)
        {
            if (mostrados >= maxFilas)
            {
                Color(ConsoleColor.Yellow, $"  ... {lista.Count - maxFilas} registros más (mostrando solo {maxFilas}).");
                break;
            }

            Console.Write("  ");
            for (int c = 0; c < _columnas.Count; c++)
            {
                var (_, clave, ancho) = _columnas[c];
                string val = ObtenerValorCelda(item, clave);
                if (val.Length > ancho) val = val[..(ancho - 1)] + "…";

                if (clave == "fuente")
                {
                    Console.ForegroundColor = FuenteColor(val);
                    Console.Write(val.PadRight(ancho));
                    Console.ResetColor();
                }
                else if (clave is "valor" or "id")
                    Console.Write(val.PadLeft(ancho));
                else
                    Console.Write(val.PadRight(ancho));

                if (c < _columnas.Count - 1) Console.Write(" │ ");
            }
            Console.WriteLine();
            mostrados++;
        }

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"  {sep}");
        Console.ResetColor();
    }

    // ══════════════════════════════════════════════════════════════
    //  NIVEL 5 – Filtrado y Ordenamiento
    // ══════════════════════════════════════════════════════════════

    static void FiltrarDatos()
    {
        Titulo("FILTRAR DATOS (Sin LINQ)");
        var campos = ObtenerCamposDisponibles();
        Console.WriteLine("  Campos disponibles:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  {string.Join(" / ", campos)}");
        Console.ResetColor();
        Console.Write("\n  Campo a filtrar: ");
        string campoDisplay = Console.ReadLine()?.Trim() ?? "";
        string campo = ResolverClave(campoDisplay);
        Console.Write("  Valor a buscar: ");
        string valor = Console.ReadLine()?.Trim() ?? "";

        var resultado = DataProcessor.Filtrar(_datos, campo, valor);
        Color(ConsoleColor.Cyan, $"\n  Resultados: {resultado.Count} registros\n");
        ImprimirTabla(resultado);
    }

    static void OrdenarDatos()
    {
        Titulo("ORDENAR DATOS (QuickSort/BubbleSort sin LINQ)");
        var campos = ObtenerCamposDisponibles();
        Console.WriteLine("  Campos disponibles:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  {string.Join(" / ", campos)}");
        Console.ResetColor();
        Console.Write("\n  Campo para ordenar: ");
        string campoDisplay = Console.ReadLine()?.Trim() ?? "";
        string campo = ResolverClave(campoDisplay);
        Console.Write("  Dirección [A = ascendente / D = descendente]: ");
        bool asc = (Console.ReadLine()?.Trim().ToUpper() ?? "A") != "D";

        var ordenados = DataProcessor.Ordenar(_datos, campo, asc);
        Color(ConsoleColor.Cyan, $"\n  Ordenado por '{campoDisplay}' {(asc ? "Ascendente ↑" : "Descendente ↓")}\n");
        ImprimirTabla(ordenados);
    }

    // ══════════════════════════════════════════════════════════════
    //  NIVEL 4 – Dictionary por categoría
    // ══════════════════════════════════════════════════════════════

    static void VerPorCategoria()
    {
        Titulo("DATOS POR CATEGORÍA (Dictionary<string, List<DataItem>>)");
        if (_porCategoria.Count == 0) { Color(ConsoleColor.Yellow, "  Sin datos."); return; }

        Console.WriteLine("  Categorías disponibles:\n");
        int i = 1;
        var cats = _porCategoria.Keys.ToList();
        foreach (var c in cats)
            Console.WriteLine($"  [{i++,2}] {c,-25} → {_porCategoria[c].Count} registros");

        Console.Write("\n  Selecciona número (0 = ver todas): ");
        if (int.TryParse(Console.ReadLine()?.Trim(), out int sel) && sel > 0 && sel <= cats.Count)
        {
            string cat = cats[sel - 1];
            Color(ConsoleColor.Cyan, $"\n  Categoría: {cat}\n");
            ImprimirTabla(_porCategoria[cat]);
        }
        else
        {
            foreach (var kv in _porCategoria)
            {
                Color(ConsoleColor.Magenta, $"\n  ── {kv.Key} ({kv.Value.Count} registros) ──");
                ImprimirTabla(kv.Value, 5);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  NIVEL 5 – Estadísticas y Gráficas
    // ══════════════════════════════════════════════════════════════

    static void MostrarEstadisticas()
    {
        Titulo("ESTADÍSTICAS POR CATEGORÍA");
        if (_datos.Count == 0) { Color(ConsoleColor.Yellow, "  Sin datos."); return; }

        var stats = DataProcessor.CalcularEstadisticas(_datos);
        string sep = new string('─', 80);
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"  {sep}");
        Console.WriteLine($"  {"Categoría",-22} │ {"Cant",5} │ {"Promedio",10} │ {"Máximo",10} │ {"Mínimo",10}");
        Console.WriteLine($"  {sep}");
        Console.ResetColor();

        foreach (var s in stats.Values.OrderByDescending(x => x.SumaValores))
            Console.WriteLine($"  {s.Categoria,-22} │ {s.Cantidad,5} │ {s.Promedio,10:F2} │ {s.ValorMaximo,10:F2} │ {s.ValorMinimo,10:F2}");

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"  {sep}");
        Console.ResetColor();
    }

    static void GraficaBarras()
    {
        Titulo("GRÁFICA DE BARRAS – Valor total por categoría");
        if (_datos.Count == 0) { Color(ConsoleColor.Yellow, "  Sin datos."); return; }

        var stats = DataProcessor.CalcularEstadisticas(_datos);
        double maximo = stats.Values.Max(s => s.SumaValores);

        Console.WriteLine();
        foreach (var s in stats.Values.OrderByDescending(x => x.SumaValores))
        {
            int barras = maximo > 0 ? (int)(s.SumaValores / maximo * 50) : 0;
            Console.Write($"  {s.Categoria,-22} │ ");
            Console.ForegroundColor = ColorBarra(s.Categoria);
            Console.Write($"{new string('█', barras),-50}");
            Console.ResetColor();
            Console.WriteLine($" {s.SumaValores:N0}");
        }
        Console.WriteLine();
    }

    // ══════════════════════════════════════════════════════════════
    //  NIVEL 5 – Duplicados
    // ══════════════════════════════════════════════════════════════

    static void GestionarDuplicados()
    {
        Titulo("GESTIÓN DE DUPLICADOS");
        var dupes = DataProcessor.DetectarDuplicados(_datos);

        if (dupes.Count == 0)
        { Color(ConsoleColor.Green, $"  ✅ No se encontraron duplicados en {_datos.Count} registros."); return; }

        Color(ConsoleColor.Yellow, $"  ⚠  Se encontraron {dupes.Count} duplicados:\n");
        ImprimirTabla(dupes);

        Console.Write($"\n  ¿Eliminar duplicados? (s/N): ");
        if (Console.ReadLine()?.Trim().ToLower() == "s")
        {
            int antes = _datos.Count;
            var limpia = DataProcessor.EliminarDuplicados(_datos);
            _datos.Clear();
            _datos.AddRange(limpia);
            ActualizarIndices();
            Color(ConsoleColor.Green, $"  ✅ Eliminados {antes - _datos.Count} duplicados. Quedan {_datos.Count} registros.");
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  EXPORTAR DATOS
    // ══════════════════════════════════════════════════════════════

    static void ExportarDatos()
    {
        Titulo("EXPORTAR DATOS");
        if (_datos.Count == 0) { Color(ConsoleColor.Yellow, "  Sin datos para exportar."); return; }

        Console.WriteLine($"  Registros disponibles: {_datos.Count}\n");
        Console.WriteLine("  Formato de salida:");
        Console.WriteLine("  [1] CSV   (.csv)");
        Console.WriteLine("  [2] JSON  (.json)");
        Console.WriteLine("  [3] XML   (.xml)");
        Console.WriteLine("  [4] TXT   (.txt, pipe-separated)");
        Console.Write("\n  Opción: ");
        string? opFormato = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(opFormato) || !new[] { "1", "2", "3", "4" }.Contains(opFormato))
        { Color(ConsoleColor.Yellow, "  Opción no válida, cancelado."); return; }

        Console.Write("\n  Ruta de salida (ENTER = escritorio): ");
        string? rutaInput = Console.ReadLine()?.Trim().Trim('"').Trim('\'').Trim();

        string carpeta;
        if (string.IsNullOrWhiteSpace(rutaInput))
            carpeta = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        else if (Directory.Exists(rutaInput))
            carpeta = rutaInput;
        else
        {
            string? dir = Path.GetDirectoryName(rutaInput);
            carpeta = string.IsNullOrEmpty(dir) ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop) : dir;
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string ext = opFormato switch { "1" => ".csv", "2" => ".json", "3" => ".xml", _ => ".txt" };
        string nombreArchivo = $"DataFusionArena_Export_{timestamp}{ext}";
        string rutaSalida = Path.Combine(carpeta, nombreArchivo);

        try
        {
            var (columnas, mapeo) = ObtenerInfoExport();
            switch (opFormato)
            {
                case "1": ExportarCsv(rutaSalida, columnas, mapeo); break;
                case "2": ExportarJson(rutaSalida, columnas, mapeo); break;
                case "3": ExportarXml(rutaSalida, columnas, mapeo); break;
                case "4": ExportarTxt(rutaSalida, columnas, mapeo); break;
            }
            Color(ConsoleColor.Green, $"\n  ✅ Exportado exitosamente:\n     {rutaSalida}");
        }
        catch (Exception ex)
        {
            Color(ConsoleColor.Red, $"\n  ❌ Error al exportar: {ex.Message}");
        }
    }

    // ── Helpers de exportación ────────────────────────────────────

    static (List<string> columnas, Dictionary<string, string> mapeo) ObtenerInfoExport()
    {
        string ultimaFuente = _datos.Count > 0 ? _datos[^1].Fuente : "";

        if (ultimaFuente == "csv" && CsvDataReader.UltimasColumnas.Count > 0)
            return (new List<string>(CsvDataReader.UltimasColumnas), CsvDataReader.MapeoColumnas);
        if (ultimaFuente == "json" && JsonDataReader.UltimasColumnas.Count > 0)
            return (new List<string>(JsonDataReader.UltimasColumnas), JsonDataReader.MapeoColumnas);
        if (ultimaFuente == "xml" && XmlDataReader.UltimasColumnas.Count > 0)
            return (new List<string>(XmlDataReader.UltimasColumnas), XmlDataReader.MapeoColumnas);
        if (ultimaFuente == "txt" && TxtDataReader.UltimasColumnas.Count > 0)
            return (new List<string>(TxtDataReader.UltimasColumnas), TxtDataReader.MapeoColumnas);
        if ((ultimaFuente == "mariadb" || ultimaFuente == "postgresql") && _ultimasColumnasBD.Count > 0)
            return (new List<string>(_ultimasColumnasBD), _ultimoMapeoBD);

        var extraKeys = _datos.SelectMany(d => d.CamposExtra.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(k => k).ToList();
        if (extraKeys.Count > 0)
            return (extraKeys, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        return (new List<string> { "id", "nombre", "categoria", "valor", "fecha", "fuente" },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    static string GetValorExport(DataItem item, string col, Dictionary<string, string> mapeo)
    {
        if (item.CamposExtra.TryGetValue(col, out var v)) return v ?? "";
        if (item.CamposExtra.TryGetValue(col.ToLowerInvariant(), out v)) return v ?? "";

        string campo = mapeo.TryGetValue(col, out var m) ? m.ToLower() : col.ToLower();
        return campo switch
        {
            "id" => item.Id.ToString(),
            "nombre" => item.Nombre ?? "",
            "categoria" => item.Categoria ?? "",
            "valor" => item.Valor.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "fecha" => item.Fecha.ToString("yyyy-MM-dd"),
            "fuente" => item.Fuente ?? "",
            _ => ""
        };
    }

    static void ExportarCsv(string ruta, List<string> columnas, Dictionary<string, string> mapeo)
    {
        var lineas = new List<string>();
        lineas.Add(string.Join(",", columnas.Select(c => EscapeCsv(c))));
        foreach (var item in _datos)
            lineas.Add(string.Join(",", columnas.Select(c => EscapeCsv(GetValorExport(item, c, mapeo)))));
        File.WriteAllLines(ruta, lineas, System.Text.Encoding.UTF8);
    }

    static void ExportarJson(string ruta, List<string> columnas, Dictionary<string, string> mapeo)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[");
        for (int i = 0; i < _datos.Count; i++)
        {
            var item = _datos[i];
            sb.AppendLine("  {");
            for (int c = 0; c < columnas.Count; c++)
            {
                string col = columnas[c];
                string val = GetValorExport(item, col, mapeo);
                bool isLast = c == columnas.Count - 1;
                bool esNum = double.TryParse(val, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _)
                    && val.Length > 0;
                string jsonVal = esNum ? val : JsonStr(val);
                sb.AppendLine($"    {JsonStr(col)}: {jsonVal}{(isLast ? "" : ",")}");
            }
            sb.Append("  }");
            if (i < _datos.Count - 1) sb.AppendLine(",");
            else sb.AppendLine();
        }
        sb.AppendLine("]");
        File.WriteAllText(ruta, sb.ToString(), System.Text.Encoding.UTF8);
    }

    static void ExportarXml(string ruta, List<string> columnas, Dictionary<string, string> mapeo)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<dataset>");
        foreach (var item in _datos)
        {
            sb.AppendLine("  <registro>");
            foreach (var col in columnas)
            {
                string tag = XmlTag(col);
                string val = GetValorExport(item, col, mapeo);
                sb.AppendLine($"    <{tag}>{XmlEscape(val)}</{tag}>");
            }
            sb.AppendLine("  </registro>");
        }
        sb.AppendLine("</dataset>");
        File.WriteAllText(ruta, sb.ToString(), System.Text.Encoding.UTF8);
    }

    static void ExportarTxt(string ruta, List<string> columnas, Dictionary<string, string> mapeo)
    {
        var lineas = new List<string>();
        lineas.Add(string.Join("|", columnas));
        foreach (var item in _datos)
            lineas.Add(string.Join("|", columnas.Select(c =>
                GetValorExport(item, c, mapeo).Replace("|", " "))));
        File.WriteAllLines(ruta, lineas, System.Text.Encoding.UTF8);
    }

    static string EscapeCsv(string v) => $"\"{v.Replace("\"", "\"\"")}\"";
    static string JsonStr(string v) => $"\"{v.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "")}\"";
    static string XmlEscape(string v) => v.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
    static string XmlTag(string k)
    {
        string t = System.Text.RegularExpressions.Regex.Replace(k, @"[^a-zA-Z0-9_\-]", "_");
        if (t.Length > 0 && char.IsDigit(t[0])) t = "_" + t;
        return string.IsNullOrEmpty(t) ? "campo" : t;
    }

    // ══════════════════════════════════════════════════════════════
    //  BONUS – LINQ
    // ══════════════════════════════════════════════════════════════

    static void BonusLinq()
    {
        Titulo("BONUS – OPERACIONES LINQ");
        Console.WriteLine("  [1] .Where() – Filtrar por categoría");
        Console.WriteLine("  [2] .GroupBy() – Agrupar y contar");
        Console.WriteLine("  [3] .OrderBy() – Top 10 por valor");
        Console.Write("\n  Opción: ");
        string? op = Console.ReadLine()?.Trim();

        if (op == "1")
        {
            Console.Write("  Categoría a filtrar: ");
            string cat = Console.ReadLine()?.Trim() ?? "";
            var res = DataProcessor.FiltrarLinq(_datos, cat).ToList();
            Color(ConsoleColor.Cyan, $"\n  LINQ .Where(): {res.Count} resultados");
            ImprimirTabla(res);
        }
        else if (op == "2")
        {
            var grupos = DataProcessor.AgruparLinq(_datos);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n  LINQ .GroupBy():\n");
            Console.ResetColor();
            foreach (var g in grupos.OrderByDescending(g => g.Count()))
                Console.WriteLine($"  {g.Key,-25} → {g.Count()} registros | Promedio: {g.Average(x => x.Valor):F2}");
        }
        else if (op == "3")
        {
            var top = DataProcessor.TopN(_datos, 10).ToList();
            Color(ConsoleColor.Cyan, "\n  LINQ .OrderByDescending() – Top 10:\n");
            ImprimirTabla(top);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════

    static void ActualizarIndices()
    {
        _porCategoria = DataProcessor.AgruparPorCategoria(_datos);
        _porId = DataProcessor.IndexarPorId(_datos);
    }

    static string LeerContraseña()
    {
        var sb = new System.Text.StringBuilder();
        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace && sb.Length > 0)
            {
                sb.Remove(sb.Length - 1, 1);
                Console.Write("\b \b");
            }
            else if (key.Key != ConsoleKey.Backspace)
            {
                sb.Append(key.KeyChar);
                Console.Write('*');
            }
        }
        Console.WriteLine();
        return sb.ToString();
    }

    static void Titulo(string texto)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        string linea = new string('═', texto.Length + 2);
        Console.WriteLine($"\n  ╔{linea}╗");
        Console.WriteLine($"  ║ {texto} ║");
        Console.WriteLine($"  ╚{linea}╝\n");
        Console.ResetColor();
    }

    static void Color(ConsoleColor color, string texto)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(texto);
        Console.ResetColor();
    }

    static ConsoleColor FuenteColor(string fuente) => fuente switch
    {
        "json" => ConsoleColor.Green,
        "csv" => ConsoleColor.Yellow,
        "xml" => ConsoleColor.Cyan,
        "txt" => ConsoleColor.Magenta,
        "postgresql" => ConsoleColor.Blue,
        "mariadb" => ConsoleColor.DarkYellow,
        _ => ConsoleColor.Gray
    };

    static ConsoleColor ColorBarra(string categoria)
    {
        var colores = new[] {
            ConsoleColor.Green,    ConsoleColor.Cyan,    ConsoleColor.Yellow,
            ConsoleColor.Magenta,  ConsoleColor.Blue,    ConsoleColor.Red,
            ConsoleColor.DarkCyan, ConsoleColor.DarkGreen
        };
        return colores[Math.Abs(categoria.GetHashCode()) % colores.Length];
    }
}