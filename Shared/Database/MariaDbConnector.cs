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
    { CadenaConexion = cadenaConexion; Tabla = tabla; }

    // ── Devuelve solo los nombres de columna (sin leer filas) ────
    public List<string> ObtenerNombresColumnas()
    {
        var cols = new List<string>();
        try
        {
            using var conn = new MySqlConnection(CadenaConexion);
            conn.Open();
            using var cmd = new MySqlCommand(
                $"SELECT * FROM `{Tabla}` LIMIT 0", conn);
            using var r = cmd.ExecuteReader();
            for (int i = 0; i < r.FieldCount; i++) cols.Add(r.GetName(i));
        }
        catch (Exception ex) { Console.WriteLine($"[MariaDB] ObtenerNombresColumnas: {ex.Message}"); }

        // Guardar también en UltimasColumnas para que el mapeo automático esté listo
        if (cols.Count > 0) UltimasColumnas = cols;
        ActualizarMapeoAutomatico();
        return cols;
    }

    // ── El usuario elige explícitamente qué columna es qué ──────
    public void SobreescribirMapeo(
        string colCategoria, string colValor,
        string colNombre, string colFecha)
    {
        // Limpiar entradas previas de las 4 claves estándar
        var keysAEliminar = MapeoColumnas
            .Where(kv => kv.Value is "categoria" or "valor" or "nombre" or "fecha")
            .Select(kv => kv.Key)
            .ToList();
        foreach (var k in keysAEliminar) MapeoColumnas.Remove(k);

        if (!string.IsNullOrEmpty(colCategoria)) MapeoColumnas[colCategoria] = "categoria";
        if (!string.IsNullOrEmpty(colValor)) MapeoColumnas[colValor] = "valor";
        if (!string.IsNullOrEmpty(colNombre)) MapeoColumnas[colNombre] = "nombre";
        if (!string.IsNullOrEmpty(colFecha)) MapeoColumnas[colFecha] = "fecha";
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
            for (int i = 0; i < reader.FieldCount; i++) mapa[reader.GetName(i)] = i;

            UltimasColumnas = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.GetName(i)).ToList();

            // Si el mapeo ya fue sobreescrito por el usuario, usarlo directamente.
            // Si no, hacer el mapeo automático.
            if (!MapeoColumnas.Any(kv => kv.Value == "categoria"))
                ActualizarMapeoAutomatico(mapa);

            var mapeadasSet = new HashSet<string>(
                MapeoColumnas.Keys, StringComparer.OrdinalIgnoreCase);

            // Columnas destino según el mapeo actual
            string? colId = MapeoColumnas.FirstOrDefault(kv => kv.Value == "id").Key;
            string? colNom = MapeoColumnas.FirstOrDefault(kv => kv.Value == "nombre").Key;
            string? colCat = MapeoColumnas.FirstOrDefault(kv => kv.Value == "categoria").Key;
            string? colVal = MapeoColumnas.FirstOrDefault(kv => kv.Value == "valor").Key;
            string? colFec = MapeoColumnas.FirstOrDefault(kv => kv.Value == "fecha").Key;

            int contador = 1;
            while (reader.Read())
            {
                var item = new DataItem { Fuente = "mariadb" };

                item.Id = (colId != null && mapa.TryGetValue(colId, out int iId) && !reader.IsDBNull(iId)
                                    && int.TryParse(reader[iId].ToString(), out int idV)) ? idV : contador;
                item.Nombre = LeerStrCol(reader, mapa, colNom) ?? $"Registro-{contador}";
                item.Categoria = LeerStrCol(reader, mapa, colCat) ?? "Sin categoría";
                item.Valor = LeerDblCol(reader, mapa, colVal) ?? 0;
                item.Fecha = LeerDateCol(reader, mapa, colFec) ?? DateTime.Now;

                // Resto de columnas → CamposExtra
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
        catch (Exception ex) { Console.WriteLine($"[MariaDB] ✗  Error: {ex.Message}"); }
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
        catch (Exception ex) { mensaje = $"Error de conexión: {ex.Message}"; return false; }
    }

    // ── Mapeo automático (por nombre de columna conocido) ────────
    private void ActualizarMapeoAutomatico(Dictionary<string, int>? mapa = null)
    {
        MapeoColumnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cols = mapa != null
            ? (IEnumerable<string>)mapa.Keys
            : UltimasColumnas;

        string? Find(params string[] alias)
        {
            foreach (var a in alias)
                foreach (var c in cols)
                    if (string.Equals(c, a, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        string? c;
        c = Find("id"); if (c != null) MapeoColumnas[c] = "id";
        c = Find("nombre", "name", "jugador", "pais", "country", "titulo", "title", "player", "empleado"); if (c != null) MapeoColumnas[c] = "nombre";
        c = Find("categoria", "category", "genero", "genre", "region", "tipo", "type", "departamento"); if (c != null) MapeoColumnas[c] = "categoria";
        c = Find("valor", "value", "puntos", "score", "precio", "price", "ventas", "sales", "total", "salario", "puntaje"); if (c != null) MapeoColumnas[c] = "valor";
        c = Find("fecha", "date", "fecha_registro", "fecha_reporte", "created_at", "updated_at", "anio"); if (c != null) MapeoColumnas[c] = "fecha";
    }

    // ── Helpers ──────────────────────────────────────────────────
    private static string? LeerStrCol(MySqlDataReader r, Dictionary<string, int> m, string? col)
    {
        if (col == null || !m.TryGetValue(col, out int i) || r.IsDBNull(i)) return null;
        return r[i].ToString();
    }
    private static double? LeerDblCol(MySqlDataReader r, Dictionary<string, int> m, string? col)
    {
        if (col == null || !m.TryGetValue(col, out int i) || r.IsDBNull(i)) return null;
        return double.TryParse(r[i].ToString(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : null;
    }
    private static DateTime? LeerDateCol(MySqlDataReader r, Dictionary<string, int> m, string? col)
    {
        if (col == null || !m.TryGetValue(col, out int i) || r.IsDBNull(i)) return null;
        return DateTime.TryParse(r[i].ToString(), out DateTime d) ? d : null;
    }
}