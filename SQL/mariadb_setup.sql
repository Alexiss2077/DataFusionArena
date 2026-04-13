-- ══════════════════════════════════════════════════════════════
--  Data Fusion Arena  ·  Script de configuración  ·  MariaDB
--  Fuente de datos: World Happiness Report 2023 (Gallup World Poll)
--  URL: https://worldhappiness.report/ed/2023/
--  Ejecutar en HeidiSQL conectado a MariaDB (root / localhost:3306)
-- ══════════════════════════════════════════════════════════════

-- 1. Crear base de datos
CREATE DATABASE IF NOT EXISTS `datafusion`
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_spanish_ci;

USE `datafusion`;

-- 2. Crear tabla de índice de felicidad mundial
CREATE TABLE IF NOT EXISTS `felicidad_mundial` (
    `id`              INT AUTO_INCREMENT PRIMARY KEY,
    `pais`            VARCHAR(100) NOT NULL,    -- se mapea a "nombre"
    `region`          VARCHAR(80)  NOT NULL,    -- se mapea a "categoria"
    `puntaje`         DECIMAL(5,3) NOT NULL,    -- se mapea a "valor" (escala 0-10)
    `fecha_reporte`   DATE         NOT NULL,    -- fecha de publicación del reporte
    `log_gdp`         DECIMAL(6,3),
    `apoyo_social`    DECIMAL(5,3),
    `esperanza_vida`  DECIMAL(5,1)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 3. Insertar datos reales del WHR 2023 (top 20 + países representativos)
INSERT INTO `felicidad_mundial`
    (`pais`, `region`, `puntaje`, `fecha_reporte`, `log_gdp`, `apoyo_social`, `esperanza_vida`)
VALUES
('Finland',         'Western Europe',             7.804, '2023-03-20', 11.30, 0.954, 71.9),
('Denmark',         'Western Europe',             7.586, '2023-03-20', 11.38, 0.954, 72.0),
('Iceland',         'Western Europe',             7.530, '2023-03-20', 11.29, 0.983, 73.0),
('Israel',          'Middle East and North Africa',7.473,'2023-03-20', 10.98, 0.930, 72.4),
('Netherlands',     'Western Europe',             7.403, '2023-03-20', 11.22, 0.942, 72.2),
('Sweden',          'Western Europe',             7.395, '2023-03-20', 11.18, 0.934, 72.9),
('Norway',          'Western Europe',             7.315, '2023-03-20', 11.47, 0.954, 73.3),
('Switzerland',     'Western Europe',             7.240, '2023-03-20', 11.59, 0.942, 74.1),
('Luxembourg',      'Western Europe',             7.228, '2023-03-20', 11.92, 0.906, 72.4),
('New Zealand',     'ANZ',                        7.123, '2023-03-20', 10.87, 0.944, 73.0),
('Austria',         'Western Europe',             7.097, '2023-03-20', 11.18, 0.927, 73.3),
('Australia',       'ANZ',                        7.095, '2023-03-20', 11.09, 0.944, 73.8),
('Canada',          'North America and ANZ',      6.961, '2023-03-20', 11.08, 0.935, 73.6),
('Ireland',         'Western Europe',             6.911, '2023-03-20', 11.59, 0.928, 72.3),
('United States',   'North America and ANZ',      6.894, '2023-03-20', 11.38, 0.899, 68.3),
('Germany',         'Western Europe',             6.892, '2023-03-20', 11.17, 0.908, 72.6),
('Belgium',         'Western Europe',             6.859, '2023-03-20', 11.09, 0.920, 72.0),
('Czechia',         'Central and Eastern Europe', 6.845, '2023-03-20', 10.82, 0.928, 70.1),
('United Kingdom',  'Western Europe',             6.796, '2023-03-20', 10.97, 0.914, 72.0),
('Lithuania',       'Central and Eastern Europe', 6.763, '2023-03-20', 10.85, 0.892, 68.0),
('Costa Rica',      'Latin America and Caribbean',6.609, '2023-03-20', 10.10, 0.898, 71.2),
('Chile',           'Latin America and Caribbean',6.587, '2023-03-20', 10.51, 0.890, 71.0),
('Mexico',          'Latin America and Caribbean',6.330, '2023-03-20', 10.28, 0.867, 68.2),
('Brazil',          'Latin America and Caribbean',6.125, '2023-03-20', 10.11, 0.855, 66.8),
('Argentina',       'Latin America and Caribbean',6.096, '2023-03-20', 10.21, 0.900, 68.8);

-- 4. Verificar
SELECT COUNT(*) AS total FROM `felicidad_mundial`;
SELECT pais AS nombre, region AS categoria, puntaje AS valor
FROM `felicidad_mundial` ORDER BY `puntaje` DESC LIMIT 5;
