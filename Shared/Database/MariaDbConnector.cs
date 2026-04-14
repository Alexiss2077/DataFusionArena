using MySqlConnector;
using DataFusionArena.Shared.Models;

namespace DataFusionArena.Shared.Database;

/// <summary>
/// Conecta a MariaDB (o MySQL) y lee datos desde cualquier tabla, mapeándolos a List&lt;DataItem&gt;.
/// Compatible con HeidiSQL: Host=localhost, Port=3306.
/// </summary>
public class MariaDbConnector
{
    public string CadenaConexion { get; set; } =
        "Server=localhost;Port=3306;Database=datafusion;User=root;Password=tu_password;";

    public string Tabla { get; set; } = "puntuaciones";

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

    public MariaDbConnector() { }

    public MariaDbConnector(string cadenaConexion, string tabla = "puntuaciones")
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
            using var conn = new MySqlConnection(CadenaConexion);
            conn.Open();
            Console.WriteLine($"[MariaDB] ✓  Conectado a {conn.Database}");

            // Sin LIMIT a menos que LimiteFilas > 0
            string sql = LimiteFilas > 0
                ? $"SELECT * FROM `{Tabla}` LIMIT {LimiteFilas}"
                : $"SELECT * FROM `{Tabla}`";

            using var cmd = new MySqlCommand(sql, conn);
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
                _c = PrimeraColumna(mapa, "nombre", "name", "jugador"); if (_c != null) MapeoColumnas[_c] = "nombre";
                _c = PrimeraColumna(mapa, "categoria", "category", "nivel"); if (_c != null) MapeoColumnas[_c] = "categoria";
                _c = PrimeraColumna(mapa, "valor", "value", "puntos", "score"); if (_c != null) MapeoColumnas[_c] = "valor";
                _c = PrimeraColumna(mapa, "fecha", "date", "fecha_registro"); if (_c != null) MapeoColumnas[_c] = "fecha";
            }

            int contador = 1;
            while (reader.Read())
            {
                var item = new DataItem { Fuente = "mariadb" };

                item.Id = LeerInt(reader, mapa, "id") ?? contador;
                item.Nombre = LeerStr(reader, mapa, "nombre", "name", "jugador") ?? FallbackStr(reader, mapa, "id") ?? $"Registro-{contador}";
                item.Categoria = LeerStr(reader, mapa, "categoria", "category", "nivel") ?? FallbackStr(reader, mapa, "id", "nombre", "name", "jugador", "valor", "value", "puntos", "score", "fecha", "date", "fecha_registro") ?? "Sin categoría";
                item.Valor = LeerDbl(reader, mapa, "valor", "value", "puntos", "score") ?? 0;
                item.Fecha = LeerDate(reader, mapa, "fecha", "date", "fecha_registro") ?? DateTime.Now;

                foreach (var kv in mapa)
                {
                    string c = kv.Key.ToLower();
                    if (c is "id" or "nombre" or "name" or "jugador"
                          or "categoria" or "category" or "nivel"
                          or "valor" or "value" or "puntos" or "score"
                          or "fecha" or "date" or "fecha_registro") continue;
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

    // ──────────────────────────────────────────────────────────────
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

    /// <summary>Devuelve el primer alias que exista en el mapa de columnas, o null si ninguno.</summary>
    private static string? PrimeraColumna(Dictionary<string, int> mapa, params string[] alias)
    {
        foreach (var a in alias)
            if (mapa.ContainsKey(a)) return a;
        return null;
    }
}