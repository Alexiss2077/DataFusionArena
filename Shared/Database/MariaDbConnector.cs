using MySqlConnector;
using DataFusionArena.Shared.Models;

namespace DataFusionArena.Shared.Database;

public class MariaDbConnector
{
    public string CadenaConexion { get; set; } = "";
    public string Tabla { get; set; } = "";
    public int LimiteFilas { get; set; } = 0;
    public List<string> UltimasColumnas { get; private set; } = new();
    public Dictionary<string, string> MapeoColumnas { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public MariaDbConnector() { }

    public MariaDbConnector(string cadenaConexion, string tabla)
    {
        CadenaConexion = cadenaConexion;
        Tabla = tabla;
    }

    public List<DataItem> LeerDatos()
    {
        var lista = new List<DataItem>();

        try
        {
            using var conn = new MySqlConnection(CadenaConexion);
            conn.Open();
            Console.WriteLine($"[MariaDB] ✓  Conectado a {conn.Database}");

            string sql = LimiteFilas > 0
                ? $"SELECT * FROM `{Tabla}` LIMIT {LimiteFilas}"
                : $"SELECT * FROM `{Tabla}`";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.CommandTimeout = 120;
            using var reader = cmd.ExecuteReader();

            var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
                mapa[reader.GetName(i)] = i;

            UltimasColumnas = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.GetName(i)).ToList();
            MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            {
                string? _c;
                _c = PrimeraColumna(mapa, "id");
                if (_c != null) MapeoColumnas[_c] = "id";

                _c = PrimeraColumna(mapa, "nombre", "name", "jugador", "pais", "country",
                    "titulo", "title", "producto", "descripcion", "description", "player", "empleado", "employee");
                if (_c != null) MapeoColumnas[_c] = "nombre";

                _c = PrimeraColumna(mapa, "categoria", "category", "nivel", "genero", "genre",
                    "region", "tipo", "type", "grupo", "group", "departamento", "department", "clase", "class");
                if (_c != null) MapeoColumnas[_c] = "categoria";

                _c = PrimeraColumna(mapa, "valor", "value", "puntos", "score", "puntaje",
                    "precio", "price", "monto", "amount", "ventas", "sales", "total", "suma",
                    "salario", "salary", "ventas_global", "rating", "calificacion");
                if (_c != null) MapeoColumnas[_c] = "valor";

                _c = PrimeraColumna(mapa, "fecha", "date", "fecha_registro", "fecha_reporte",
                    "fecha_lanzamiento", "created_at", "updated_at", "timestamp", "anio", "year");
                if (_c != null) MapeoColumnas[_c] = "fecha";
            }

            int contador = 1;
            while (reader.Read())
            {
                var item = new DataItem { Fuente = "mariadb" };

                item.Id = LeerInt(reader, mapa, "id") ?? contador;
                item.Nombre = LeerStr(reader, mapa, "nombre", "name", "jugador", "pais", "country",
                    "titulo", "title", "producto", "player", "empleado", "employee") ??
                    FallbackStr(reader, mapa, "id") ?? $"Registro-{contador}";
                item.Categoria = LeerStr(reader, mapa, "categoria", "category", "nivel", "genero", "genre",
                    "region", "tipo", "type", "grupo", "group", "departamento", "department") ??
                    FallbackStr(reader, mapa, "id", "nombre", "name", "jugador", "pais", "country",
                        "titulo", "title", "producto", "player", "empleado", "employee",
                        "valor", "value", "puntos", "score", "puntaje", "precio", "price",
                        "monto", "amount", "ventas", "sales", "total", "suma", "salario", "salary",
                        "ventas_global", "fecha", "date", "fecha_registro", "fecha_reporte") ?? "Sin categoría";
                item.Valor = LeerDbl(reader, mapa, "valor", "value", "puntos", "score", "puntaje",
                    "precio", "price", "monto", "amount", "ventas", "sales", "total", "suma",
                    "salario", "salary", "ventas_global", "rating", "calificacion") ?? 0;
                item.Fecha = LeerDate(reader, mapa, "fecha", "date", "fecha_registro", "fecha_reporte",
                    "fecha_lanzamiento", "created_at", "updated_at", "timestamp") ?? DateTime.Now;

                var mapeadasSet = new HashSet<string>(MapeoColumnas.Keys, StringComparer.OrdinalIgnoreCase);
                foreach (var kv in mapa)
                {
                    if (mapeadasSet.Contains(kv.Key)) continue;
                    if (string.Equals(kv.Key, "id", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!reader.IsDBNull(kv.Value))
                        item.CamposExtra[kv.Key] = reader[kv.Value]?.ToString() ?? "";
                }

                lista.Add(item);
                contador++;

                if (contador % 10_000 == 0)
                    Console.WriteLine($"[MariaDB]    ... {contador} registros leídos");
            }

            Console.WriteLine($"[MariaDB] ✓  {lista.Count} registros leídos desde tabla '{Tabla}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MariaDB] ✗  Error: {ex.Message}");
        }

        return lista;
    }

    public bool ProbarConexion(out string mensaje)
    {
        try
        {
            using var conn = new MySqlConnection(CadenaConexion);
            conn.Open();
            using var cmd = new MySqlCommand($"SELECT COUNT(*) FROM `{Tabla}`", conn);
            long total = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
            mensaje = $"Conexión exitosa · DB: {conn.Database} · Servidor: {conn.DataSource} · Filas en '{Tabla}': {total:N0}";
            return true;
        }
        catch (Exception ex)
        {
            mensaje = $"Error de conexión: {ex.Message}";
            return false;
        }
    }

    private static string? LeerStr(MySqlDataReader r, Dictionary<string, int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i)) return r[i].ToString();
        return null;
    }

    private static string? FallbackStr(MySqlDataReader r, Dictionary<string, int> m, params string[] excluir)
    {
        var exc = new HashSet<string>(excluir, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in m)
            if (!exc.Contains(kv.Key) && !r.IsDBNull(kv.Value))
            {
                var val = r[kv.Value]?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
        return null;
    }

    private static int? LeerInt(MySqlDataReader r, Dictionary<string, int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i) && int.TryParse(r[i].ToString(), out int v)) return v;
        return null;
    }

    private static double? LeerDbl(MySqlDataReader r, Dictionary<string, int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i) &&
                double.TryParse(r[i].ToString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double v)) return v;
        return null;
    }

    private static DateTime? LeerDate(MySqlDataReader r, Dictionary<string, int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i) && DateTime.TryParse(r[i].ToString(), out DateTime d)) return d;
        return null;
    }

    private static string? PrimeraColumna(Dictionary<string, int> mapa, params string[] alias)
    {
        foreach (var a in alias)
            if (mapa.ContainsKey(a)) return a;
        return null;
    }
}