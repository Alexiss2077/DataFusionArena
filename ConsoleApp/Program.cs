using DataFusionArena.Shared.Models;
using DataFusionArena.Shared.Readers;
using DataFusionArena.Shared.Database;
using DataFusionArena.Shared.Processing;

namespace DataFusionArena.ConsoleApp;

class Program
{
    // ── Estado global de la aplicación ──────────────────────────
    static readonly List<DataItem> _datos = new();
    static Dictionary<string, List<DataItem>> _porCategoria = new();
    static Dictionary<int, DataItem>          _porId        = new();

    // Rutas a los archivos de datos (relativas al ejecutable)
    static readonly string _dirDatos = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleData");

    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "Data Fusion Arena – Consola";

        MostrarBanner();

        bool continuar = true;
        while (continuar)
        {
            continuar = MostrarMenu();
        }

        Console.WriteLine("\n¡Hasta luego! 🎮\n");
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
 ╚═════╝ ╚═╝  ╚═╝   ╚═╝   ╚═╝  ╚═╝   ╚═╝      ╚═════╝ ╚══════╝╚═╝ ╚═════╝ ╚═╝  ╚═══╝
                         ARENA  –  Administración y Organización de Datos
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
        Console.WriteLine("  [L] ⚡  Bonus: operaciones LINQ");
        Console.WriteLine("  [0] 🚪  Salir");
        Console.Write("\n  Opción: ");

        string? op = Console.ReadLine()?.Trim().ToUpper();
        Console.WriteLine();

        switch (op)
        {
            case "1": CargarArchivos();    break;
            case "2": ConectarBD();        break;
            case "3": VerTodos();          break;
            case "4": FiltrarDatos();      break;
            case "5": OrdenarDatos();      break;
            case "6": VerPorCategoria();   break;
            case "7": MostrarEstadisticas(); break;
            case "8": GraficaBarras();     break;
            case "9": GestionarDuplicados(); break;
            case "L": BonusLinq();         break;
            case "0": return false;
            default:
                Color(ConsoleColor.Red, "  ⚠  Opción no válida.\n");
                break;
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
        Console.WriteLine("  [2] CSV   – sales.csv (columnas desordenadas)");
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
            case "2": CargarCsv(Path.Combine(_dirDatos, "sales.csv"));      break;
            case "3": CargarXml(Path.Combine(_dirDatos, "employees.xml"));  break;
            case "4": CargarTxt(Path.Combine(_dirDatos, "records.txt"));    break;
            case "5":
                CargarJson(Path.Combine(_dirDatos, "products.json"));
                CargarCsv(Path.Combine(_dirDatos, "sales.csv"));
                CargarXml(Path.Combine(_dirDatos, "employees.xml"));
                CargarTxt(Path.Combine(_dirDatos, "records.txt"));
                break;
            case "6":
                Console.Write("  Ruta completa del archivo: ");
                string? ruta = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(ruta)) break;
                string ext = Path.GetExtension(ruta).ToLower();
                switch (ext)
                {
                    case ".json": CargarJson(ruta); break;
                    case ".csv":  CargarCsv(ruta);  break;
                    case ".xml":  CargarXml(ruta);  break;
                    case ".txt":  CargarTxt(ruta);  break;
                    default: Color(ConsoleColor.Red, $"  Extensión '{ext}' no soportada."); break;
                }
                break;
        }

        ActualizarIndices();
        Color(ConsoleColor.Green, $"\n  ✅ Total acumulado: {_datos.Count} registros en memoria.");
    }

    static void CargarJson(string ruta)
    {
        var nuevos = JsonDataReader.Leer(ruta);
        DataProcessor.AgregarDatos(_datos, nuevos);
    }
    static void CargarCsv(string ruta)
    {
        var nuevos = CsvDataReader.Leer(ruta);
        DataProcessor.AgregarDatos(_datos, nuevos);
    }
    static void CargarXml(string ruta)
    {
        var nuevos = XmlDataReader.Leer(ruta);
        // Mapear "departamento" → categoria, "salario" → valor
        foreach (var item in nuevos)
        {
            if (item.CamposExtra.TryGetValue("departamento", out var dep))
            { item.Categoria = dep; item.CamposExtra.Remove("departamento"); }
            if (item.CamposExtra.TryGetValue("salario", out var sal) && double.TryParse(sal, out double s))
            { item.Valor = s; item.CamposExtra.Remove("salario"); }
        }
        DataProcessor.AgregarDatos(_datos, nuevos);
    }
    static void CargarTxt(string ruta)
    {
        var nuevos = TxtDataReader.Leer(ruta);
        DataProcessor.AgregarDatos(_datos, nuevos);
    }

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
            Console.Write("\n  Cadena PostgreSQL (ENTER para usar la predeterminada):\n  > ");
            string? cadena = Console.ReadLine()?.Trim();

            Console.Write("  Nombre de tabla (ENTER = videojuegos): ");
            string tabla = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(tabla)) tabla = "videojuegos";

            var pg = string.IsNullOrEmpty(cadena)
                ? new PostgreSqlConnector()
                : new PostgreSqlConnector(cadena, tabla);
            pg.Tabla = tabla;

            Console.WriteLine("\n  Probando conexión...");
            if (pg.ProbarConexion(out string msg))
            {
                Color(ConsoleColor.Green, $"  {msg}");
                var datos = pg.LeerDatos();
                DataProcessor.AgregarDatos(_datos, datos);
                ActualizarIndices();
                Color(ConsoleColor.Green, $"  ✅ {datos.Count} registros cargados desde PostgreSQL.");
            }
            else
                Color(ConsoleColor.Red, $"  ❌ {msg}");
        }
        else if (op == "2")
        {
            Console.Write("\n  Cadena MariaDB (ENTER para usar la predeterminada):\n  > ");
            string? cadena = Console.ReadLine()?.Trim();

            Console.Write("  Nombre de tabla (ENTER = puntuaciones): ");
            string tabla = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(tabla)) tabla = "puntuaciones";

            var md = string.IsNullOrEmpty(cadena)
                ? new MariaDbConnector()
                : new MariaDbConnector(cadena, tabla);
            md.Tabla = tabla;

            Console.WriteLine("\n  Probando conexión...");
            if (md.ProbarConexion(out string msg))
            {
                Color(ConsoleColor.Green, $"  {msg}");
                var datos = md.LeerDatos();
                DataProcessor.AgregarDatos(_datos, datos);
                ActualizarIndices();
                Color(ConsoleColor.Green, $"  ✅ {datos.Count} registros cargados desde MariaDB.");
            }
            else
                Color(ConsoleColor.Red, $"  ❌ {msg}");
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  NIVEL 6 – Visualización: Tabla
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
        string sep = new string('─', 100);
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"  {sep}");
        Console.WriteLine($"  {"ID",5} │ {"Nombre",-28} │ {"Categoría",-18} │ {"Valor",10} │ {"Fecha",-12} │ Fuente");
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
            Console.Write($"  {item.Id,5} │ {item.Nombre,-28} │ {item.Categoria,-18} │ {item.Valor,10:F2} │ {item.Fecha:yyyy-MM-dd} │ ");
            Color(FuenteColor(item.Fuente), item.Fuente);
            Console.WriteLine();
            mostrados++;
        }
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"  {sep}");
        Console.ResetColor();
    }

    // ══════════════════════════════════════════════════════════════
    //  NIVEL 5 – Filtrado
    // ══════════════════════════════════════════════════════════════

    static void FiltrarDatos()
    {
        Titulo("FILTRAR DATOS (Sin LINQ)");
        Console.Write("  Campo a filtrar [nombre / categoria / fuente / id / valor]: ");
        string campo = Console.ReadLine()?.Trim() ?? "nombre";
        Console.Write("  Valor a buscar: ");
        string valor = Console.ReadLine()?.Trim() ?? "";

        var resultado = DataProcessor.Filtrar(_datos, campo, valor);
        Color(ConsoleColor.Cyan, $"\n  Resultados: {resultado.Count} registros\n");
        ImprimirTabla(resultado);
    }

    // ══════════════════════════════════════════════════════════════
    //  NIVEL 5 – Ordenamiento (QuickSort / BubbleSort, sin LINQ)
    // ══════════════════════════════════════════════════════════════

    static void OrdenarDatos()
    {
        Titulo("ORDENAR DATOS (QuickSort/BubbleSort sin LINQ)");
        Console.Write("  Campo [id / nombre / categoria / valor / fecha]: ");
        string campo = Console.ReadLine()?.Trim() ?? "valor";
        Console.Write("  Dirección [A = ascendente / D = descendente]: ");
        bool asc = (Console.ReadLine()?.Trim().ToUpper() ?? "A") != "D";

        var ordenados = DataProcessor.Ordenar(_datos, campo, asc);
        string dir = asc ? "Ascendente ↑" : "Descendente ↓";
        Color(ConsoleColor.Cyan, $"\n  Ordenado por '{campo}' {dir}\n");
        ImprimirTabla(ordenados);
    }

    // ══════════════════════════════════════════════════════════════
    //  NIVEL 4 – Dictionary: agrupar por categoría
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
    //  NIVEL 5 – Estadísticas
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

    // ══════════════════════════════════════════════════════════════
    //  NIVEL 6 – Gráfica de barras en consola
    // ══════════════════════════════════════════════════════════════

    static void GraficaBarras()
    {
        Titulo("GRÁFICA DE BARRAS – Valor total por categoría");
        if (_datos.Count == 0) { Color(ConsoleColor.Yellow, "  Sin datos."); return; }

        var stats = DataProcessor.CalcularEstadisticas(_datos);
        double maximo = stats.Values.Max(s => s.SumaValores);
        int anchoMax = 50;

        Console.WriteLine();
        foreach (var s in stats.Values.OrderByDescending(x => x.SumaValores))
        {
            int barras = maximo > 0 ? (int)(s.SumaValores / maximo * anchoMax) : 0;
            string barra = new string('█', barras);
            Console.Write($"  {s.Categoria,-22} │ ");
            Console.ForegroundColor = ColorBarra(s.Categoria);
            Console.Write($"{barra,-50}");
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
        {
            Color(ConsoleColor.Green, $"  ✅ No se encontraron duplicados en {_datos.Count} registros.");
            return;
        }

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
        _porId        = DataProcessor.IndexarPorId(_datos);
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
        "json"       => ConsoleColor.Green,
        "csv"        => ConsoleColor.Yellow,
        "xml"        => ConsoleColor.Cyan,
        "txt"        => ConsoleColor.Magenta,
        "postgresql" => ConsoleColor.Blue,
        "mariadb"    => ConsoleColor.DarkYellow,
        _            => ConsoleColor.Gray
    };

    static ConsoleColor ColorBarra(string categoria)
    {
        var colores = new[] {
            ConsoleColor.Green, ConsoleColor.Cyan, ConsoleColor.Yellow,
            ConsoleColor.Magenta, ConsoleColor.Blue, ConsoleColor.Red,
            ConsoleColor.DarkCyan, ConsoleColor.DarkGreen
        };
        return colores[Math.Abs(categoria.GetHashCode()) % colores.Length];
    }
}
