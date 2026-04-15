using Npgsql;
using DataFusionArena.Shared.Models;

namespace DataFusionArena.Shared.Database;

public class PostgreSqlConnector
{
    public string CadenaConexion { get; set; } = "";
    public string Tabla { get; set; } = "";
    public int LimiteFilas { get; set; } = 0;

    public List<string> UltimasColumnas { get; private set; } = new();
    public Dictionary<string, string> MapeoColumnas { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public PostgreSqlConnector() { }
    public PostgreSqlConnector(string cadenaConexion, string tabla)
    { CadenaConexion = cadenaConexion; Tabla = tabla; }

    // ── Devuelve nombres de columna sin leer filas ───────────────
    public List<string> ObtenerNombresColumnas()
    {
        var cols = new List<string>();
        try
        {
            using var conn = new NpgsqlConnection(CadenaConexion);
            conn.Open();
            using var cmd = new NpgsqlCommand($"SELECT * FROM {Tabla} LIMIT 0", conn);
            using var r = cmd.ExecuteReader();
            for (int i = 0; i < r.FieldCount; i++) cols.Add(r.GetName(i));
        }
        catch (Exception ex)
        { Console.WriteLine($"[PostgreSQL] ObtenerNombresColumnas: {ex.Message}"); }

        UltimasColumnas = cols;
        ActualizarMapeoAutomatico();
        return cols;
    }

    // ── El usuario confirma qué columna es cada rol ──────────────
    public void SobreescribirMapeo(
        string colCategoria, string colValor,
        string colNombre, string colFecha)
    {
        var aEliminar = MapeoColumnas
            .Where(kv => kv.Value is "categoria" or "valor" or "nombre" or "fecha")
            .Select(kv => kv.Key).ToList();
        foreach (var k in aEliminar) MapeoColumnas.Remove(k);

        if (!string.IsNullOrEmpty(colCategoria)) MapeoColumnas[colCategoria] = "categoria";
        if (!string.IsNullOrEmpty(colValor)) MapeoColumnas[colValor] = "valor";
        if (!string.IsNullOrEmpty(colNombre)) MapeoColumnas[colNombre] = "nombre";
        if (!string.IsNullOrEmpty(colFecha)) MapeoColumnas[colFecha] = "fecha";
    }

    // ── Leer todos los datos ─────────────────────────────────────
    public List<DataItem> LeerDatos()
    {
        var lista = new List<DataItem>();
        try
        {
            using var conn = new NpgsqlConnection(CadenaConexion);
            conn.Open();
            Console.WriteLine($"[PostgreSQL] ✓  Conectado a {conn.Database}");

            // Verificar que la tabla existe
            var colsInfo = ObtenerColumnasInfo(conn, Tabla);
            if (colsInfo.Count == 0)
            {
                Console.WriteLine($"[PostgreSQL] ⚠  Tabla '{Tabla}' no encontrada.");
                return lista;
            }

            string sql = LimiteFilas > 0
                ? $"SELECT * FROM {Tabla} LIMIT {LimiteFilas}"
                : $"SELECT * FROM {Tabla}";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.CommandTimeout = 120;
            using var reader = cmd.ExecuteReader();

            var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++) mapa[reader.GetName(i)] = i;

            UltimasColumnas = mapa.Keys.ToList();

            if (!MapeoColumnas.Any(kv => kv.Value == "categoria"))
                ActualizarMapeoAutomatico(mapa.Keys);

            // Resolver columnas por rol
            string? colId = MapeoColumnas.FirstOrDefault(kv => kv.Value == "id").Key;
            string? colNom = MapeoColumnas.FirstOrDefault(kv => kv.Value == "nombre").Key;
            string? colCat = MapeoColumnas.FirstOrDefault(kv => kv.Value == "categoria").Key;
            string? colVal = MapeoColumnas.FirstOrDefault(kv => kv.Value == "valor").Key;
            string? colFec = MapeoColumnas.FirstOrDefault(kv => kv.Value == "fecha").Key;

            var mapeadasSet = new HashSet<string>(
                MapeoColumnas.Keys, StringComparer.OrdinalIgnoreCase);

            int contador = 1;
            while (reader.Read())
            {
                var item = new DataItem { Fuente = "postgresql" };

                // ── ID ───────────────────────────────────────────
                if (colId != null && mapa.TryGetValue(colId, out int iId) && !reader.IsDBNull(iId)
                    && int.TryParse(reader[iId].ToString(), out int idParsed))
                    item.Id = idParsed;
                else
                    item.Id = contador;

                // ── Nombre ───────────────────────────────────────
                item.Nombre = LeerStr(reader, mapa, colNom) ?? $"Registro-{contador}";

                // ── Categoría ────────────────────────────────────
                item.Categoria = LeerStr(reader, mapa, colCat) ?? "Sin categoría";

                // ── Valor numérico ───────────────────────────────
                item.Valor = LeerDbl(reader, mapa, colVal) ?? 0;

                // ── Fecha ────────────────────────────────────────
                item.Fecha = LeerDate(reader, mapa, colFec) ?? DateTime.Now;

                // ── Resto → CamposExtra ──────────────────────────
                foreach (var kv in mapa)
                {
                    if (mapeadasSet.Contains(kv.Key)) continue;
                    if (!reader.IsDBNull(kv.Value))
                        item.CamposExtra[kv.Key] = reader[kv.Value].ToString() ?? "";
                }

                lista.Add(item);
                contador++;
                if (contador % 10_000 == 0)
                    Console.WriteLine($"[PostgreSQL]    ... {contador} registros leídos");
            }
            Console.WriteLine($"[PostgreSQL] ✓  {lista.Count} registros leídos desde '{Tabla}'");
        }
        catch (Exception ex) { Console.WriteLine($"[PostgreSQL] ✗  Error: {ex.Message}"); }
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
        catch (Exception ex) { mensaje = $"Error de conexión: {ex.Message}"; return false; }
    }

    private List<string> ObtenerColumnasInfo(NpgsqlConnection conn, string tabla)
    {
        var cols = new List<string>();
        try
        {
            using var cmd = new NpgsqlCommand(
                $"SELECT column_name FROM information_schema.columns WHERE table_name='{tabla.ToLower()}'",
                conn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) cols.Add(r.GetString(0));
        }
        catch { }
        return cols;
    }

    private void ActualizarMapeoAutomatico(IEnumerable<string>? cols = null)
    {
        var fuente = cols ?? (IEnumerable<string>)UltimasColumnas;
        MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string? Find(params string[] alias)
        {
            foreach (var a in alias)
                foreach (var c in fuente)
                    if (string.Equals(c, a, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        string? c;
        c = Find("id"); if (c != null) MapeoColumnas[c] = "id";
        c = Find("nombre", "name", "titulo", "title", "pais", "country", "jugador", "player", "empleado", "employee"); if (c != null) MapeoColumnas[c] = "nombre";
        c = Find("categoria", "category", "genero", "genre", "region", "tipo", "type", "departamento", "department", "level"); if (c != null) MapeoColumnas[c] = "categoria";
        c = Find("valor", "value", "precio", "price", "ventas_global", "ventas", "sales", "score", "puntos", "salario", "puntaje", "total"); if (c != null) MapeoColumnas[c] = "valor";
        c = Find("fecha", "date", "fecha_lanzamiento", "anio", "year", "created_at", "updated_at", "timestamp"); if (c != null) MapeoColumnas[c] = "fecha";
    }

    private static string? LeerStr(NpgsqlDataReader r, Dictionary<string, int> m, string? col)
    {
        if (col == null || !m.TryGetValue(col, out int i) || r.IsDBNull(i)) return null;
        var v = r[i]?.ToString();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static double? LeerDbl(NpgsqlDataReader r, Dictionary<string, int> m, string? col)
    {
        if (col == null || !m.TryGetValue(col, out int i) || r.IsDBNull(i)) return null;
        return double.TryParse(r[i].ToString(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : (double?)null;
    }

    private static DateTime? LeerDate(NpgsqlDataReader r, Dictionary<string, int> m, string? col)
    {
        if (col == null || !m.TryGetValue(col, out int i) || r.IsDBNull(i)) return null;
        return DateTime.TryParse(r[i].ToString(), out DateTime d) ? d : (DateTime?)null;
    }
}