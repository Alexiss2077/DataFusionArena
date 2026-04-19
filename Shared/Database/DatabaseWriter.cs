using Npgsql;
using MySqlConnector;
using DataFusionArena.Shared.Models;
using System.Globalization;

namespace DataFusionArena.Shared.Database;

/// <summary>
/// Escribe registros DataItem en PostgreSQL o MariaDB.
/// Crea la tabla automáticamente si no existe.
/// </summary>
public static class DatabaseWriter
{
    // ══════════════════════════════════════════════════════════════
    //  POSTGRESQL
    // ══════════════════════════════════════════════════════════════

    public static WriteResult EscribirEnPostgreSQL(
        string cadenaConexion,
        string tabla,
        List<DataItem> datos,
        List<string> columnas,
        Dictionary<string, string> mapeo,
        IProgress<int>? progreso = null)
    {
        var result = new WriteResult();
        try
        {
            using var conn = new NpgsqlConnection(cadenaConexion);
            conn.Open();

            // Determinar columnas extras únicas del dataset
            var extraKeys = datos
                .SelectMany(d => d.CamposExtra.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k)
                .ToList();

            // Crear tabla si no existe
            CrearTablaPostgreSQL(conn, tabla, extraKeys);

            // Insertar por lotes
            int total = datos.Count;
            int insertados = 0;
            int errores = 0;

            using var tx = conn.BeginTransaction();
            try
            {
                foreach (var item in datos)
                {
                    try
                    {
                        InsertarItemPostgreSQL(conn, tx, tabla, item, extraKeys);
                        insertados++;
                        if (insertados % 100 == 0)
                            progreso?.Report((int)(insertados * 100.0 / total));
                    }
                    catch
                    {
                        errores++;
                    }
                }
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            progreso?.Report(100);
            result.Insertados = insertados;
            result.Errores = errores;
            result.Exito = true;
            result.Mensaje = $"✅ {insertados} registros insertados en '{tabla}'. Errores: {errores}.";
        }
        catch (Exception ex)
        {
            result.Exito = false;
            result.Mensaje = $"❌ Error: {ex.Message}";
        }
        return result;
    }

    public static async Task<WriteResult> EscribirEnPostgreSQLAsync(
        string cadenaConexion,
        string tabla,
        List<DataItem> datos,
        List<string> columnas,
        Dictionary<string, string> mapeo,
        IProgress<int>? progreso = null)
    {
        return await Task.Run(() =>
            EscribirEnPostgreSQL(cadenaConexion, tabla, datos, columnas, mapeo, progreso));
    }

    private static void CrearTablaPostgreSQL(NpgsqlConnection conn, string tabla, List<string> extraKeys)
    {
        // Construir columnas extras como TEXT
        string extraCols = extraKeys.Count > 0
            ? ",\n    " + string.Join(",\n    ", extraKeys.Select(k =>
                $"\"{SanitizarNombre(k)}\" TEXT"))
            : "";

        string sql = $@"
CREATE TABLE IF NOT EXISTS ""{tabla}"" (
    id INTEGER,
    nombre TEXT,
    categoria TEXT,
    valor DOUBLE PRECISION,
    fecha DATE,
    fuente TEXT{extraCols}
);";
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }

    private static void InsertarItemPostgreSQL(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string tabla,
        DataItem item,
        List<string> extraKeys)
    {
        var colNames = new List<string> { "id", "nombre", "categoria", "valor", "fecha", "fuente" };
        var paramNames = new List<string> { "@id", "@nombre", "@categoria", "@valor", "@fecha", "@fuente" };

        foreach (var k in extraKeys)
        {
            colNames.Add($"\"{SanitizarNombre(k)}\"");
            paramNames.Add("@extra_" + SanitizarNombre(k));
        }

        string sql = $@"INSERT INTO ""{tabla}"" ({string.Join(", ", colNames)})
VALUES ({string.Join(", ", paramNames)})
ON CONFLICT DO NOTHING;";

        using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@nombre", item.Nombre ?? "");
        cmd.Parameters.AddWithValue("@categoria", item.Categoria ?? "");
        cmd.Parameters.AddWithValue("@valor", item.Valor);
        cmd.Parameters.AddWithValue("@fecha", NpgsqlTypes.NpgsqlDbType.Date, item.Fecha.Date);
        cmd.Parameters.AddWithValue("@fuente", item.Fuente ?? "");

        foreach (var k in extraKeys)
        {
            string val = item.CamposExtra.TryGetValue(k, out var v) ? v ?? "" : "";
            cmd.Parameters.AddWithValue("@extra_" + SanitizarNombre(k), val);
        }

        cmd.ExecuteNonQuery();
    }

    // ══════════════════════════════════════════════════════════════
    //  MARIADB
    // ══════════════════════════════════════════════════════════════

    public static WriteResult EscribirEnMariaDB(
        string cadenaConexion,
        string tabla,
        List<DataItem> datos,
        List<string> columnas,
        Dictionary<string, string> mapeo,
        IProgress<int>? progreso = null)
    {
        var result = new WriteResult();
        try
        {
            using var conn = new MySqlConnection(cadenaConexion);
            conn.Open();

            var extraKeys = datos
                .SelectMany(d => d.CamposExtra.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k)
                .ToList();

            CrearTablaMariaDB(conn, tabla, extraKeys);

            int total = datos.Count;
            int insertados = 0;
            int errores = 0;

            using var tx = conn.BeginTransaction();
            try
            {
                foreach (var item in datos)
                {
                    try
                    {
                        InsertarItemMariaDB(conn, tx, tabla, item, extraKeys);
                        insertados++;
                        if (insertados % 100 == 0)
                            progreso?.Report((int)(insertados * 100.0 / total));
                    }
                    catch
                    {
                        errores++;
                    }
                }
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            progreso?.Report(100);
            result.Insertados = insertados;
            result.Errores = errores;
            result.Exito = true;
            result.Mensaje = $"✅ {insertados} registros insertados en `{tabla}`. Errores: {errores}.";
        }
        catch (Exception ex)
        {
            result.Exito = false;
            result.Mensaje = $"❌ Error: {ex.Message}";
        }
        return result;
    }

    public static async Task<WriteResult> EscribirEnMariaDBAsync(
        string cadenaConexion,
        string tabla,
        List<DataItem> datos,
        List<string> columnas,
        Dictionary<string, string> mapeo,
        IProgress<int>? progreso = null)
    {
        return await Task.Run(() =>
            EscribirEnMariaDB(cadenaConexion, tabla, datos, columnas, mapeo, progreso));
    }

    private static void CrearTablaMariaDB(MySqlConnection conn, string tabla, List<string> extraKeys)
    {
        string extraCols = extraKeys.Count > 0
            ? ",\n    " + string.Join(",\n    ", extraKeys.Select(k =>
                $"`{SanitizarNombre(k)}` TEXT"))
            : "";

        string sql = $@"
CREATE TABLE IF NOT EXISTS `{tabla}` (
    `id` INT,
    `nombre` TEXT,
    `categoria` TEXT,
    `valor` DOUBLE,
    `fecha` DATE,
    `fuente` VARCHAR(50){extraCols}
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
        using var cmd = new MySqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }

    private static void InsertarItemMariaDB(
        MySqlConnection conn,
        MySqlTransaction tx,
        string tabla,
        DataItem item,
        List<string> extraKeys)
    {
        var colNames = new List<string> { "`id`", "`nombre`", "`categoria`", "`valor`", "`fecha`", "`fuente`" };
        var paramNames = new List<string> { "@id", "@nombre", "@categoria", "@valor", "@fecha", "@fuente" };

        foreach (var k in extraKeys)
        {
            colNames.Add($"`{SanitizarNombre(k)}`");
            paramNames.Add("@extra_" + SanitizarNombre(k));
        }

        string sql = $@"INSERT IGNORE INTO `{tabla}` ({string.Join(", ", colNames)})
VALUES ({string.Join(", ", paramNames)});";

        using var cmd = new MySqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@nombre", item.Nombre ?? "");
        cmd.Parameters.AddWithValue("@categoria", item.Categoria ?? "");
        cmd.Parameters.AddWithValue("@valor", item.Valor);
        cmd.Parameters.AddWithValue("@fecha", item.Fecha.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@fuente", item.Fuente ?? "");

        foreach (var k in extraKeys)
        {
            string val = item.CamposExtra.TryGetValue(k, out var v) ? v ?? "" : "";
            cmd.Parameters.AddWithValue("@extra_" + SanitizarNombre(k), val);
        }

        cmd.ExecuteNonQuery();
    }

    // ══════════════════════════════════════════════════════════════
    //  OBTENER TABLAS DISPONIBLES
    // ══════════════════════════════════════════════════════════════

    public static List<string> ObtenerTablasPostgreSQL(string cadenaConexion)
    {
        var tablas = new List<string>();
        try
        {
            using var conn = new NpgsqlConnection(cadenaConexion);
            conn.Open();
            using var cmd = new NpgsqlCommand(
                "SELECT table_name FROM information_schema.tables " +
                "WHERE table_schema = 'public' AND table_type = 'BASE TABLE' " +
                "ORDER BY table_name;", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) tablas.Add(r.GetString(0));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PostgreSQL] ObtenerTablas: {ex.Message}");
        }
        return tablas;
    }

    public static List<string> ObtenerTablasMariaDB(string cadenaConexion)
    {
        var tablas = new List<string>();
        try
        {
            using var conn = new MySqlConnection(cadenaConexion);
            conn.Open();
            using var cmd = new MySqlCommand(
                $"SELECT table_name FROM information_schema.tables " +
                $"WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE' " +
                $"ORDER BY table_name;", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) tablas.Add(r.GetString(0));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MariaDB] ObtenerTablas: {ex.Message}");
        }
        return tablas;
    }

    // ══════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════

    private static string SanitizarNombre(string nombre)
    {
        // Reemplazar caracteres no alfanuméricos por _
        var sb = new System.Text.StringBuilder();
        foreach (char c in nombre)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        string s = sb.ToString().Trim('_');
        if (s.Length == 0) return "campo";
        if (char.IsDigit(s[0])) s = "_" + s;
        return s.ToLowerInvariant();
    }
}

public class WriteResult
{
    public bool Exito { get; set; }
    public string Mensaje { get; set; } = "";
    public int Insertados { get; set; }
    public int Errores { get; set; }
}