# Data Fusion Arena
### Administración y Organización de Datos · Ingeniería 4.º Semestre · C# .NET 10

---

## 📋 Descripción

**Data Fusion Arena** es una aplicación de escritorio, Web y consola para cargar, visualizar, procesar y exportar datos desde múltiples fuentes. Permite integrar archivos JSON, CSV, XML y TXT junto con bases de datos PostgreSQL y MariaDB en un único modelo universal, aplicar operaciones de filtrado, ordenamiento y estadísticas, y visualizar los resultados en tablas y gráficas.

---

## 📁 Estructura del Proyecto

```
DataFusionArena/
├── DataFusionArena.sln
│
├── Shared/                              ← Biblioteca central compartida
│   ├── Models/
│   │   └── DataItem.cs                  ← Modelo universal de datos
│   ├── Readers/
│   │   ├── JsonDataReader.cs            ← Lector JSON con recuperación parcial
│   │   ├── CsvDataReader.cs             ← Lector CSV con columnas desordenadas
│   │   ├── XmlDataReader.cs             ← Lector XML con detección automática
│   │   └── TxtDataReader.cs             ← Lector TXT (pipe/tab separado)
│   ├── Database/
│   │   ├── PostgreSqlConnector.cs       ← Conector PostgreSQL (pgAdmin 4)
│   │   ├── MariaDbConnector.cs          ← Conector MariaDB (HeidiSQL)
│   │   └── DatabaseWriter.cs           ← Exportación a bases de datos
│   ├── Processing/
│   │   └── DataProcessor.cs            ← Filtrado, ordenamiento, estadísticas, LINQ
│   └── Services/
│       └── FileExportService.cs        ← Exportación a archivos
│
├── ConsoleApp/                          ← Interfaz de consola
│   └── Program.cs
│
├── WinFormsApp/                         ← Interfaz de escritorio Windows
│   ├── MainForm.cs
│   ├── MainForm.Designer.cs
│   ├── ChartPanel.cs                   ← Gráficas GDI+ personalizadas
│   ├── Dialogs.cs                      ← Diálogos de conexión y mapeo
│   └── Program.cs
│
├── WebApp/                              ← Interfaz web ASP.NET Core MVC
│   ├── Controllers/
│   │   ├── HomeController.cs
│   │   └── ApiExternaController.cs     ← Integración con Open-Meteo API
│   ├── Models/ViewModels.cs
│   ├── Services/
│   │   ├── DataStore.cs
│   │   └── ExportService.cs
│   └── Views/
│
├── SampleData/                          ← Datasets de prueba
│   ├── products.json                   ← World Happiness Report 2023
│   ├── sales.csv                       ← Video Game Sales (columnas desordenadas)
│   ├── employees.xml                   ← Iris Dataset (UCI)
│   └── records.txt                     ← Récords Mundiales de Atletismo
│
└── SQL/
    ├── postgresql_setup.sql
    └── mariadb_setup.sql
```

---

## 🏗️ Arquitectura

El proyecto sigue una **arquitectura de N-capas** con una biblioteca compartida central:

```
┌─────────────────────────────────────────────┐
│              DataFusionArena.Shared         │
│  Models │ Readers │ Database │ Processing  │
└────────────────────┬────────────────────────┘
                     │ referenciado por
        ┌────────────┼────────────┐
        ▼            ▼            ▼
   ConsoleApp    WinFormsApp    WebApp
```

> La capa Web (`WebApp`) sí implementa el patrón **MVC** completo con Controllers, Views y ViewModels. Las capas Console y WinForms siguen un esquema de **N-capas** sin separación formal MVC/MVP.

---



## 🗄️ Configuración de Bases de Datos

### PostgreSQL

```sql
-- Ejecutar en HeidiSQL o psql
-- Ver script completo en: SQL/postgresql_setup.sql
CREATE DATABASE datafusion WITH ENCODING 'UTF8';
-- Crea tabla: videojuegos (top 20 ventas globales históricas)
```

Cadena de conexión:
```
Host=localhost;Port=5432;Database=datafusion;Username=postgres;Password=TU_PASSWORD;
```

### MariaDB

```sql
-- Ejecutar en HeidiSQL o MySQL Workbench
-- Ver script completo en: SQL/mariadb_setup.sql
CREATE DATABASE datafusion CHARACTER SET utf8mb4;
-- Crea tabla: felicidad_mundial (World Happiness Report 2023)
```

Cadena de conexión:
```
Server=localhost;Port=3306;Database=datafusion;User=root;Password=TU_PASSWORD;
```

---

## 📊 Fuentes de Datos Incluidas

| Archivo | Fuente real | Registros |
|---|---|---|
| `products.json` | World Happiness Report 2023 | 30 países |
| `sales.csv` | Kaggle – Video Game Sales | 40 juegos |
| `employees.xml` | UCI – Iris Dataset (Fisher 1936) | 30 muestras |
| `records.txt` | World Athletics – Récords Mundiales | 25 marcas |


---

## ⚙️ Funcionalidades

### Carga de datos
- JSON, CSV, XML y TXT con detección automática de columnas
- Columnas desordenadas en CSV (mapeo por nombre de encabezado)
- Recuperación parcial de JSON mal formado
- Conexión interactiva a PostgreSQL y MariaDB
- Mapeo manual de columnas BD → modelo universal

### Procesamiento
- Filtrado por cualquier campo (propiedades estándar y campos extra)
- Ordenamiento **QuickSort** (>20 registros) y **BubbleSort** (≤20 registros)
- Detección y eliminación de duplicados
- Estadísticas por categoría: conteo, promedio, máximo, mínimo, suma

### Visualización
- Tabla dinámica con columnas detectadas automáticamente
- Paginación horizontal en consola para datasets con muchas columnas
- Gráficas de barras ASCII en consola
- Gráficas GDI+ (columnas, barras horizontales, pastel) en WinForms
- Dashboard con Chart.js en la webapp
- Filtro por fuente de datos activa

### Exportación
- Archivos: CSV, JSON, XML, TXT (pipe-separated)
- Bases de datos: PostgreSQL y MariaDB (crea la tabla si no existe)
- La tabla exportada replica exactamente las columnas visibles

### Bonus
- LINQ: `.Where()`, `.GroupBy()`, `.OrderByDescending()`, `.Take()`
- API REST externa: pronóstico del clima desde **Open-Meteo** (sin API key)
- Los datos del clima se integran al DataSet como `DataItem`

---

## 🧠 Estructuras de datos utilizadas

```csharp
// Almacenamiento principal
List<DataItem> _datos = new();

// Acceso O(1) por categoría
Dictionary<string, List<DataItem>> _porCategoria = DataProcessor.AgruparPorCategoria(_datos);

// Índice O(1) por ID
Dictionary<int, DataItem> _porId = DataProcessor.IndexarPorId(_datos);
```

> **Estrategia clave:** el uso de `Dictionary<string, List<DataItem>>` permite agrupar y acceder a cualquier categoría en O(1), evitando recorrer toda la lista en cada consulta conforme el dataset crece.

---

## 📦 Paquetes NuGet

| Paquete | Versión | Uso |
|---|---|---|
| `Npgsql` | 9.0.2 | Conexión a PostgreSQL |
| `MySqlConnector` | 2.3.7 | Conexión a MariaDB |

---

## 🧩 Conceptos aplicados por nivel

| Nivel | Concepto | Implementación |
|---|---|---|
| 1 | Datasets reales | `SampleData/` |
| 2 | Lectura de archivos | `Shared/Readers/` |
| 3 | Bases de datos | `Shared/Database/` |
| 4 | `List<T>` y `Dictionary` | `DataProcessor.cs` |
| 5 | Filtrado, ordenamiento, duplicados | `DataProcessor.cs` |
| 6 | Visualización | Consola ASCII · WinForms GDI+ · Web Chart.js |
| Bonus | LINQ + API REST | `DataProcessor.cs` · `ApiExternaController.cs` |

---

## 🔧 Eventos especiales manejados

| Situación | Manejo |
|---|---|
| JSON mal formado | Recuperación objeto por objeto con `JsonDocument` |
| CSV con columnas desordenadas | Mapeo por nombre de encabezado, no por posición |
| Tags XML con espacios | Reemplazo con regex antes de parsear |
| Datos duplicados | Detección por `Id + Nombre + Categoría` |
| Dataset muy grande | Límite de visualización configurable (`DISPLAY_LIMIT`) |
| Tabla BD inexistente | Se crea automáticamente al exportar |

---

## 📐 Modelo universal

```csharp
public class DataItem
{
    public int      Id         { get; set; }
    public string   Nombre     { get; set; }
    public string   Categoria  { get; set; }
    public double   Valor      { get; set; }
    public string   Fuente     { get; set; }  // "json" | "csv" | "xml" | "txt" | "postgresql" | "mariadb"
    public DateTime Fecha      { get; set; }

    // Campos adicionales de cualquier dataset personalizado
    public Dictionary<string, string> CamposExtra { get; set; }
}
```

---

## 👥 Créditos de datos

- [World Happiness Report 2023](https://worldhappiness.report/ed/2023/) – Gallup World Poll
- [Video Game Sales](https://www.kaggle.com/datasets/gregorut/videogamesales) – Kaggle / GregorySmith
- [Iris Dataset](https://archive.ics.uci.edu/dataset/53/iris) – R.A. Fisher / UCI ML Repository
- [World Athletics](https://worldathletics.org) – Récords Mundiales 2023
- [Open-Meteo](https://open-meteo.com) – API de pronóstico del clima (gratuita, sin key)

---

*Data Fusion Arena · Administración y Organización de Datos · Ingeniería 4.º Semestre · C# .NET 10*
