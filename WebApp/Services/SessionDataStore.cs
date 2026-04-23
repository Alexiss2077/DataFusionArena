using DataFusionArena.Shared.Models;
using DataFusionArena.Shared.Readers;
using DataFusionArena.Shared.Processing;

namespace DataFusionArena.Web.Services;

/// <summary>
/// Almacena datos separados por sesión de usuario.
/// Cada navegador/dispositivo tiene su propio dataset independiente.
/// </summary>
public class SessionDataStore
{
    // Diccionario global: sessionId → DataStore del usuario
    private readonly Dictionary<string, DataStore> _sesiones = new();
    private readonly Lock _lock = new();
    
    // Tiempo de expiración de sesión inactiva (2 horas)
    private readonly Dictionary<string, DateTime> _ultimoAcceso = new();
    private readonly TimeSpan _expiracion = TimeSpan.FromHours(2);

    public DataStore ObtenerSesion(string sessionId)
    {
        LimpiarSesionesExpiradas();

        lock (_lock)
        {
            if (!_sesiones.TryGetValue(sessionId, out var store))
            {
                store = new DataStore();
                _sesiones[sessionId] = store;
                Console.WriteLine($"[Session] Nueva sesión creada: {sessionId[..8]}...");
            }
            _ultimoAcceso[sessionId] = DateTime.Now;
            return store;
        }
    }

    private void LimpiarSesionesExpiradas()
    {
        lock (_lock)
        {
            var expiradas = _ultimoAcceso
                .Where(kv => DateTime.Now - kv.Value > _expiracion)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var id in expiradas)
            {
                _sesiones.Remove(id);
                _ultimoAcceso.Remove(id);
                Console.WriteLine($"[Session] Sesión expirada eliminada: {id[..8]}...");
            }
        }
    }

    public int TotalSesiones()
    {
        lock (_lock) return _sesiones.Count;
    }
}