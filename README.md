# 🎮 Data Fusion Arena
### Administración y Organización de Datos · Ingeniería 4.º Semestre · C# .NET 10

---

## 📁 Estructura del Proyecto

```
DataFusionArena/
├── DataFusionArena.sln
│
├── Shared/                          ← Biblioteca compartida (lógica central)
│   ├── Models/
│   │   └── DataItem.cs              ← Modelo universal
│   ├── Readers/
│   │   ├── JsonDataReader.cs        ← Nivel 2: Lector JSON
│   │   ├── CsvDataReader.cs         ← Nivel 2: Lector CSV (maneja columnas desordenadas)
│   │   ├── XmlDataReader.cs         ← Nivel 2: Lector XML
│   │   └── TxtDataReader.cs         ← Nivel 2: Lector TXT (pipe/tab separado)
│   ├── Database/
│   │   ├── PostgreSqlConnector.cs   ← Nivel 3: Conexión PostgreSQL (Npgsql)
│   │   └── MariaDbConnector.cs      ← Nivel 3: Conexión MariaDB (MySqlConnector)
│   └── Processing/
│       └── DataProcessor.cs         ← Niveles 4-5: List<T>, Dictionary, ordenamiento, LINQ bonus
│
├── ConsoleApp/                      ← Proyecto 1: Aplicación de consola
│   └── Program.cs
│
├── WinFormsApp/                     ← Proyecto 2: Aplicación WinForms
│   ├── Program.cs
│   ├── MainForm.cs                  ← Lógica de eventos
│   └── MainForm.Designer.cs         ← Diseño de la interfaz
│
├── SampleData/                      ← Datasets de prueba
│   ├── products.json                ← 15 videojuegos 2024
│   ├── sales.csv                    ← Ventas (columnas DESORDENADAS a propósito)
│   ├── employees.xml                ← Empleados del estudio
│   └── records.txt                  ← Récords de jugadores (pipe-separated)
│
└── SQL/
    ├── postgresql_setup.sql         ← Script para PostgreSQL
    └── mariadb_setup.sql            ← Script para MariaDB
```

---

## 🚀 Cómo abrir y ejecutar

### Requisitos
- Visual Studio 2022 (v17.x o superior)
- .NET 10 SDK instalado
- (Opcional) PostgreSQL + pgAdmin o HeidiSQL
- (Opcional) MariaDB + HeidiSQL

### Pasos
1. Abre `DataFusionArena.sln` en Visual Studio
2. Haz clic derecho en la solución → **Restaurar paquetes NuGet**
3. Para ejecutar la **Consola**: clic derecho en `DataFusionArena.Console` → **Establecer como proyecto de inicio** → F5
4. Para ejecutar **WinForms**: clic derecho en `DataFusionArena.WinForms` → **Establecer como proyecto de inicio** → F5

---

## 🗄️ Configuración de Bases de Datos

### PostgreSQL
1. Abre HeidiSQL → Nueva sesión → PostgreSQL
2. Ejecuta el script `SQL/postgresql_setup.sql`
3. En la app, modifica la cadena de conexión:
   ```
   Host=localhost;Port=5432;Database=datafusion;Username=postgres;Password=TU_PASSWORD;
   ```

### MariaDB
1. Abre HeidiSQL → Nueva sesión → MariaDB
2. Ejecuta el script `SQL/mariadb_setup.sql`
3. En la app, modifica la cadena de conexión:
   ```
   Server=localhost;Port=3306;Database=datafusion;User=root;Password=TU_PASSWORD;
   ```

---

## 🧠 Conceptos aplicados por nivel

| Nivel | Descripción | Dónde verlo |
|-------|-------------|-------------|
| 1 | Datasets reales (simulados) | `SampleData/` |
| 2 | Lectura de JSON, CSV, XML, TXT | `Shared/Readers/` |
| 3 | Conexión PostgreSQL y MariaDB | `Shared/Database/` |
| 4 | `List<T>` y `Dictionary<TKey,TValue>` | `DataProcessor.cs` |
| 5 | Filtrar, ordenar (QuickSort/BubbleSort), agrupar, deduplicar | `DataProcessor.cs` |
| 6 | Tabla consola + gráfica barras ASCII · DataGridView + Chart | `Program.cs` / `MainForm.cs` |
| Bonus | LINQ: `.Where()`, `.GroupBy()`, `.OrderBy()` | `DataProcessor.cs` |

---

## 🎯 Estructura de datos principales

### `List<T>` → almacenamiento principal
```csharp
List<DataItem> _datos = new();          // todos los registros
DataProcessor.AgregarDatos(_datos, nuevos);
```

### `Dictionary<TKey,TValue>` → organización y acceso rápido
```csharp
// Agrupación por categoría:  O(1) por acceso
Dictionary<string, List<DataItem>> _porCategoria = DataProcessor.AgruparPorCategoria(_datos);

// Índice por ID:  O(1) búsqueda
Dictionary<int, DataItem> _porId = DataProcessor.IndexarPorId(_datos);
```

---

## 🧨 Eventos sorpresa manejados

| Evento | Dónde se maneja |
|--------|-----------------|
| JSON mal formado | `JsonDataReader` → recuperación parcial objeto por objeto |
| CSV con columnas desordenadas | `CsvDataReader` → detección por nombre de encabezado |
| Datos duplicados | `DataProcessor.DetectarDuplicados()` + `EliminarDuplicados()` |

---

## 📊 Evaluación

| Criterio | % | Implementado en |
|----------|---|-----------------|
| Uso de List/Dictionary | 30% | `DataProcessor.cs` |
| Integración de datos | 25% | Todos los Readers + DB Connectors |
| Visualización | 20% | Consola ASCII table/barras + WinForms DataGridView/Chart |
| Código limpio | 15% | Comentarios XML, nombres descriptivos, separación de responsabilidades |
| Bonus LINQ | 10% | `DataProcessor.FiltrarLinq/AgruparLinq/OrdenarLinq/TopN` |

---

## 💬 Reflexión final

> *"La estrategia de administración y organización de datos más importante en mi
> solución fue el uso de **Dictionary<string, List\<DataItem\>>** para agrupar
> por categoría porque permite acceder a cualquier grupo en O(1) sin necesidad
> de recorrer toda la lista cada vez que se necesita filtrar, lo que hace el
> procesamiento significativamente más eficiente conforme crece el volumen de datos."*
