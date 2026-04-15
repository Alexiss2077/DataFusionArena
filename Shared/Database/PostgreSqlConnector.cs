using Npgsql;
using DataFusionArena.Shared.Models;

namespace DataFusionArena.Shared.Database;

public class PostgreSqlConnector
{
    public string CadenaConexion { get; set; } = "";
    public string Tabla { get; set; } = "";
    public int LimiteFilas { get; set; } = 0;

    /// <summary>Nombres de columna en el orden original de la tabla.</summary>
    public List<string> UltimasColumnas { get; private set; } = new();

    /// <summary>
    /// Mapeo columna-original → clave-interna.
    /// Claves internas: "id" | "nombre" | "categoria" | "valor" | "fecha"
    /// Las columnas sin mapeo se almacenan en CamposExtra.
    /// </summary>
    public Dictionary<string, string> MapeoColumnas { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public PostgreSqlConnector() { }

    public PostgreSqlConnector(string cadenaConexion, string tabla)
    {
        CadenaConexion = cadenaConexion;
        Tabla = tabla;
    }

    public List<DataItem> LeerDatos()
    {
        var lista = new List<DataItem>();

        try
        {
            using var conn = new NpgsqlConnection(CadenaConexion);
            conn.Open();
            Console.WriteLine($"[PostgreSQL] ✓  Conectado a {conn.Database}");

            var columnas = ObtenerColumnas(conn, Tabla);
            if (columnas.Count == 0)
            {
                Console.WriteLine($"[PostgreSQL] ⚠  La tabla '{Tabla}' no existe o está vacía.");
                return lista;
            }

            string sql = LimiteFilas > 0
                ? $"SELECT * FROM {Tabla} LIMIT {LimiteFilas}"
                : $"SELECT * FROM {Tabla}";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.CommandTimeout = 120;
            using var reader = cmd.ExecuteReader();

            // ── Índice nombre-de-columna → posición ─────────────────
            var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
                mapa[reader.GetName(i)] = i;

            // ── Guardar columnas en orden original ───────────────────
            UltimasColumnas = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.GetName(i))
                .ToList();

            // ── Construir MapeoColumnas con claves en MINÚSCULAS ─────
            MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string? col;
            col = PrimeraColumna(mapa,
                "id");
            if (col != null) MapeoColumnas[col] = "id";

            col = PrimeraColumna(mapa,
                "nombre", "name", "titulo", "title", "producto",
                "pais", "country", "jugador", "player",
                "descripcion", "description", "empleado", "employee");
            if (col != null) MapeoColumnas[col] = "nombre";

            col = PrimeraColumna(mapa,
                "categoria", "category", "genero", "genre",
                "region", "tipo", "type", "grupo", "group",
                "departamento", "department", "nivel", "level",
                "clase", "class");
            if (col != null) MapeoColumnas[col] = "categoria";

            col = PrimeraColumna(mapa,
                "valor", "value", "precio", "price", "ventas_global",
                "ventas", "sales", "puntaje", "score", "puntos", "points",
                "monto", "amount", "total", "suma", "salario", "salary",
                "rating", "calificacion");
            if (col != null) MapeoColumnas[col] = "valor";

            col = PrimeraColumna(mapa,
                "fecha", "date", "fecha_lanzamiento", "anio", "year",
                "fecha_registro", "fecha_reporte",
                "created_at", "updated_at", "timestamp");
            if (col != null) MapeoColumnas[col] = "fecha";

            // ── Leer registros ───────────────────────────────────────
            var mapeadasSet = new HashSet<string>(
                MapeoColumnas.Keys, StringComparer.OrdinalIgnoreCase);

            int contador = 1;
            while (reader.Read())
            {
                var item = new DataItem { Fuente = "postgresql" };

                item.Id = LeerInt(reader, mapa, "id") ?? contador;
                item.Nombre = LeerStr(reader, mapa,
                                    "nombre", "name", "titulo", "title", "producto",
                                    "pais", "country", "jugador", "player",
                                    "empleado", "employee")
                                ?? FallbackStr(reader, mapa, "id")
                                ?? $"Registro-{contador}";
                item.Categoria = LeerStr(reader, mapa,
                                    "categoria", "category", "genero", "genre",
                                    "region", "tipo", "type", "grupo", "group",
                                    "departamento", "department")
                                ?? FallbackStr(reader, mapa,
                                    "id", "nombre", "name", "titulo", "title",
                                    "producto", "pais", "country", "jugador", "player",
                                    "empleado", "employee", "valor", "value", "precio",
                                    "price", "ventas_global", "ventas", "sales",
                                    "puntaje", "score", "puntos", "fecha", "date",
                                    "fecha_lanzamiento", "anio")
                                ?? "Sin categoría";
                item.Valor = LeerDbl(reader, mapa,
                                    "valor", "value", "precio", "price", "ventas_global",
                                    "ventas", "sales", "puntaje", "score", "puntos",
                                    "points", "monto", "amount", "total", "suma",
                                    "salario", "salary", "rating", "calificacion")
                                ?? 0;
                item.Fecha = LeerDate(reader, mapa,
                                    "fecha", "date", "fecha_lanzamiento", "anio",
                                    "fecha_registro", "fecha_reporte",
                                    "created_at", "updated_at", "timestamp")
                                ?? DateTime.Now;

                // Columnas NO mapeadas → CamposExtra
                foreach (var kv in mapa)
                {
                    if (mapeadasSet.Contains(kv.Key)) continue;
                    if (string.Equals(kv.Key, "id", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!reader.IsDBNull(kv.Value))
                        item.CamposExtra[kv.Key] = reader[kv.Value].ToString() ?? "";
                }

                lista.Add(item);
                contador++;

                if (contador % 10_000 == 0)
                    Console.WriteLine($"[PostgreSQL]    ... {contador} registros leídos");
            }

            Console.WriteLine($"[PostgreSQL] ✓  {lista.Count} registros leídos desde tabla '{Tabla}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PostgreSQL] ✗  Error: {ex.Message}");
        }

        return lista;
    }

    public bool ProbarConexion(out string mensaje)
    {
        try
        {
            using var conn = new NpgsqlConnection(CadenaConexion);
            conn.Open();
            using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {Tabla}", conn);
            long total = (long)(cmd.ExecuteScalar() ?? 0L);
            mensaje = $"Conexión exitosa · DB: {conn.Database} · Servidor: {conn.Host} · Filas en '{Tabla}': {total:N0}";
            return true;
        }
        catch (Exception ex)
        {
            mensaje = $"Error de conexión: {ex.Message}";
            return false;
        }
    }

    private List<string> ObtenerColumnas(NpgsqlConnection conn, string tabla)
    {
        var cols = new List<string>();
        using var cmd = new NpgsqlCommand(
            $"SELECT column_name FROM information_schema.columns WHERE table_name='{tabla.ToLower()}'",
            conn);
        using var r = cmd.ExecuteReader();
        while (r.Read()) cols.Add(r.GetString(0));
        return cols;
    }

    // ── Helpers de lectura ───────────────────────────────────────

    private static string? LeerStr(NpgsqlDataReader r, Dictionary<string, int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i))
                return r[i].ToString();
        return null;
    }

    private static string? FallbackStr(NpgsqlDataReader r, Dictionary<string, int> m, params string[] excluir)
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

    private static int? LeerInt(NpgsqlDataReader r, Dictionary<string, int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i) &&
                int.TryParse(r[i].ToString(), out int v))
                return v;
        return null;
    }

    private static double? LeerDbl(NpgsqlDataReader r, Dictionary<string, int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i) &&
                double.TryParse(r[i].ToString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double v))
                return v;
        return null;
    }

    private static DateTime? LeerDate(NpgsqlDataReader r, Dictionary<string, int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i) &&
                DateTime.TryParse(r[i].ToString(), out DateTime d))
                return d;
        return null;
    }

    private static string? PrimeraColumna(Dictionary<string, int> mapa, params string[] alias)
    {
        foreach (var a in alias)
            if (mapa.ContainsKey(a)) return a;
        return null;
    }
}