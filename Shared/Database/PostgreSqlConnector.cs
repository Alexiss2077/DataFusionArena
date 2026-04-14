using Npgsql;
using DataFusionArena.Shared.Models;

namespace DataFusionArena.Shared.Database;

/// <summary>
/// Conecta a PostgreSQL y lee datos desde cualquier tabla, mapeándolos a List&lt;DataItem&gt;.
/// Cadena de conexión: modifica Host, Database, Username y Password según tu entorno.
/// </summary>
public class PostgreSqlConnector
{
    public string CadenaConexion { get; set; } =
        "Host=localhost;Port=5432;Database=datafusion;Username=postgres;Password=tu_password;";

    public string Tabla { get; set; } = "videojuegos";

    /// <summary>Límite de filas. 0 = sin límite.</summary>
    public int LimiteFilas { get; set; } = 0;

    /// <summary>Nombres de columna en el orden original de la tabla (se puebla tras LeerDatos).</summary>
    public List<string> UltimasColumnas { get; private set; } = new();

    /// <summary>
    /// Mapeo columna-BD → propiedad-DataItem ("id","nombre","categoria","valor","fecha").
    /// Las columnas que no aparecen aquí van a CamposExtra.
    /// </summary>
    public Dictionary<string, string> MapeoColumnas { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public PostgreSqlConnector() { }

    public PostgreSqlConnector(string cadenaConexion, string tabla = "videojuegos")
    {
        CadenaConexion = cadenaConexion;
        Tabla = tabla;
    }

    /// <summary>Lee todos los registros de la tabla configurada (sin límite por defecto).</summary>
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

            // Sin LIMIT a menos que LimiteFilas > 0
            string sql = LimiteFilas > 0
                ? $"SELECT * FROM {Tabla} LIMIT {LimiteFilas}"
                : $"SELECT * FROM {Tabla}";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.CommandTimeout = 120;
            using var reader = cmd.ExecuteReader();

            var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
                mapa[reader.GetName(i)] = i;

            // ── Exponer metadatos de columnas para la UI ──────────────────
            UltimasColumnas = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.GetName(i)).ToList();
            MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            {
                string? _c;
                _c = PrimeraColumna(mapa, "id"); if (_c != null) MapeoColumnas[_c] = "id";
                _c = PrimeraColumna(mapa, "nombre", "name", "titulo"); if (_c != null) MapeoColumnas[_c] = "nombre";
                _c = PrimeraColumna(mapa, "categoria", "category", "genero"); if (_c != null) MapeoColumnas[_c] = "categoria";
                _c = PrimeraColumna(mapa, "valor", "value", "precio"); if (_c != null) MapeoColumnas[_c] = "valor";
                _c = PrimeraColumna(mapa, "fecha", "date", "fecha_lanzamiento"); if (_c != null) MapeoColumnas[_c] = "fecha";
            }

            int contador = 1;
            while (reader.Read())
            {
                var item = new DataItem { Fuente = "postgresql" };

                item.Id = LeerInt(reader, mapa, "id") ?? contador;
                item.Nombre = LeerStr(reader, mapa, "nombre", "name", "titulo") ?? FallbackStr(reader, mapa, "id") ?? $"Registro-{contador}";
                item.Categoria = LeerStr(reader, mapa, "categoria", "category", "genero") ?? FallbackStr(reader, mapa, "id", "nombre", "name", "titulo", "valor", "value", "precio", "fecha", "date") ?? "Sin categoría";
                item.Valor = LeerDbl(reader, mapa, "valor", "value", "precio") ?? 0;
                item.Fecha = LeerDate(reader, mapa, "fecha", "date", "fecha_lanzamiento") ?? DateTime.Now;

                foreach (var kv in mapa)
                {
                    string c = kv.Key.ToLower();
                    if (c is "id" or "nombre" or "name" or "titulo"
                          or "categoria" or "category" or "genero"
                          or "valor" or "value" or "precio"
                          or "fecha" or "date" or "fecha_lanzamiento") continue;
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

    // ──────────────────────────────────────────────────────────────
    private List<string> ObtenerColumnas(NpgsqlConnection conn, string tabla)
    {
        var cols = new List<string>();
        using var cmd = new NpgsqlCommand(
            $"SELECT column_name FROM information_schema.columns WHERE table_name='{tabla.ToLower()}'", conn);
        using var r = cmd.ExecuteReader();
        while (r.Read()) cols.Add(r.GetString(0));
        return cols;
    }

    private static string? LeerStr(NpgsqlDataReader r, Dictionary<string, int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i)) return r[i].ToString();
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
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i) && int.TryParse(r[i].ToString(), out int v)) return v;
        return null;
    }

    private static double? LeerDbl(NpgsqlDataReader r, Dictionary<string, int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i) &&
                double.TryParse(r[i].ToString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double v)) return v;
        return null;
    }

    private static DateTime? LeerDate(NpgsqlDataReader r, Dictionary<string, int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i) && DateTime.TryParse(r[i].ToString(), out DateTime d)) return d;
        return null;
    }

    /// <summary>Devuelve el primer alias que exista en el mapa de columnas, o null si ninguno.</summary>
    private static string? PrimeraColumna(Dictionary<string, int> mapa, params string[] alias)
    {
        foreach (var a in alias)
            if (mapa.ContainsKey(a)) return a;
        return null;
    }
}