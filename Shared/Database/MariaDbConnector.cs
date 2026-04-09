using MySqlConnector;
using DataFusionArena.Shared.Models;

namespace DataFusionArena.Shared.Database;

/// <summary>
/// Conecta a MariaDB (o MySQL) y lee datos desde cualquier tabla, mapeándolos a List&lt;DataItem&gt;.
/// Compatible con HeidiSQL: Host=localhost, Port=3306.
/// </summary>
public class MariaDbConnector
{
    // ── Modifica estos valores según tu instalación ──────────────
    public string CadenaConexion { get; set; } =
        "Server=localhost;Port=3306;Database=datafusion;User=root;Password=tu_password;";

    public string Tabla { get; set; } = "puntuaciones";

    public MariaDbConnector() { }

    public MariaDbConnector(string cadenaConexion, string tabla = "puntuaciones")
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
            using var conn = new MySqlConnection(CadenaConexion);
            conn.Open();
            Console.WriteLine($"[MariaDB] ✓  Conectado a {conn.Database}");

            using var cmd = new MySqlCommand($"SELECT * FROM `{Tabla}` LIMIT 1000", conn);
            using var reader = cmd.ExecuteReader();

            var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
                mapa[reader.GetName(i)] = i;

            int contador = 1;
            while (reader.Read())
            {
                var item = new DataItem { Fuente = "mariadb" };

                item.Id        = LeerInt(reader, mapa, "id")                               ?? contador;
                item.Nombre    = LeerStr(reader, mapa, "nombre", "name", "jugador")        ?? $"Registro-{contador}";
                item.Categoria = LeerStr(reader, mapa, "categoria", "category", "nivel")   ?? "Sin categoría";
                item.Valor     = LeerDbl(reader, mapa, "valor", "value", "puntos", "score") ?? 0;
                item.Fecha     = LeerDate(reader, mapa, "fecha", "date", "fecha_registro")  ?? DateTime.Now;

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
            mensaje = $"Conexión exitosa a MariaDB · DB: {conn.Database} · Servidor: {conn.DataSource}";
            return true;
        }
        catch (Exception ex)
        {
            mensaje = $"Error de conexión: {ex.Message}";
            return false;
        }
    }

    // ──────────────────────────────────────────────────────────────
    private static string? LeerStr(MySqlDataReader r, Dictionary<string,int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i)) return r[i].ToString();
        return null;
    }

    private static int? LeerInt(MySqlDataReader r, Dictionary<string,int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i) && int.TryParse(r[i].ToString(), out int v)) return v;
        return null;
    }

    private static double? LeerDbl(MySqlDataReader r, Dictionary<string,int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i) &&
                double.TryParse(r[i].ToString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double v)) return v;
        return null;
    }

    private static DateTime? LeerDate(MySqlDataReader r, Dictionary<string,int> m, params string[] claves)
    {
        foreach (var c in claves)
            if (m.TryGetValue(c, out int i) && !r.IsDBNull(i) && DateTime.TryParse(r[i].ToString(), out DateTime d)) return d;
        return null;
    }
}
