using Npgsql;
using DataFusionArena.Shared.Models;

namespace DataFusionArena.Shared.Database;

/// <summary>
/// Conecta a PostgreSQL y lee datos desde cualquier tabla, mapeándolos a List&lt;DataItem&gt;.
/// Cadena de conexión: modifica Host, Database, Username y Password según tu entorno.
/// </summary>
public class PostgreSqlConnector
{
    // ── Modifica estos valores según tu instalación ──────────────
    public string CadenaConexion { get; set; } =
        "Host=localhost;Port=5432;Database=datafusion;Username=postgres;Password=tu_password;";

    public string Tabla { get; set; } = "videojuegos";

    // ── Constructor que acepta cadena personalizada ───────────────
    public PostgreSqlConnector() { }

    public PostgreSqlConnector(string cadenaConexion, string tabla = "videojuegos")
    {
        CadenaConexion = cadenaConexion;
        Tabla          = tabla;
    }

    /// <summary>Lee todos los registros de la tabla configurada.</summary>
    public List<DataItem> LeerDatos()
    {
        var lista = new List<DataItem>();

        try
        {
            using var conn = new NpgsqlConnection(CadenaConexion);
            conn.Open();
            Console.WriteLine($"[PostgreSQL] ✓  Conectado a {conn.Database}");

            // Obtener columnas disponibles en la tabla
            var columnas = ObtenerColumnas(conn, Tabla);
            if (columnas.Count == 0)
            {
                Console.WriteLine($"[PostgreSQL] ⚠  La tabla '{Tabla}' no existe o está vacía.");
                return lista;
            }

            using var cmd = new NpgsqlCommand($"SELECT * FROM {Tabla} LIMIT 1000", conn);
            using var reader = cmd.ExecuteReader();

            // Mapa de columnas disponibles (case-insensitive)
            var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
                mapa[reader.GetName(i)] = i;

            int contador = 1;
            while (reader.Read())
            {
                var item = new DataItem { Fuente = "postgresql" };

                item.Id        = LeerInt(reader, mapa, "id") ?? contador;
                item.Nombre    = LeerStr(reader, mapa, "nombre", "name", "titulo") ?? $"Registro-{contador}";
                item.Categoria = LeerStr(reader, mapa, "categoria", "category", "genero") ?? "Sin categoría";
                item.Valor     = LeerDbl(reader, mapa, "valor", "value", "precio") ?? 0;
                item.Fecha     = LeerDate(reader, mapa, "fecha", "date", "fecha_lanzamiento") ?? DateTime.Now;

                // Columnas extra → CamposExtra
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
            }

            Console.WriteLine($"[PostgreSQL] ✓  {lista.Count} registros leídos desde tabla '{Tabla}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PostgreSQL] ✗  Error: {ex.Message}");
        }

        return lista;
    }

    /// <summary>Verifica si la conexión es posible sin leer datos.</summary>
    public bool ProbarConexion(out string mensaje)
    {
        try
        {
            using var conn = new NpgsqlConnection(CadenaConexion);
            conn.Open();
            mensaje = $"Conexión exitosa a PostgreSQL · DB: {conn.Database} · Servidor: {conn.Host}";
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

    private static string? LeerStr(NpgsqlDataReader r, Dictionary<string,int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i)) return r[i].ToString();
        return null;
    }

    private static int? LeerInt(NpgsqlDataReader r, Dictionary<string,int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i) && int.TryParse(r[i].ToString(), out int v)) return v;
        return null;
    }

    private static double? LeerDbl(NpgsqlDataReader r, Dictionary<string,int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i) &&
                double.TryParse(r[i].ToString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double v)) return v;
        return null;
    }

    private static DateTime? LeerDate(NpgsqlDataReader r, Dictionary<string,int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i) && DateTime.TryParse(r[i].ToString(), out DateTime d)) return d;
        return null;
    }
}
