-- ══════════════════════════════════════════════════════════════
--  Data Fusion Arena  ·  Script de configuración  ·  PostgreSQL
--  Ejecutar en HeidiSQL o psql como usuario postgres
-- ══════════════════════════════════════════════════════════════

-- 1. Crear base de datos (si no existe)
-- En psql: \c postgres  luego ejecuta esto
CREATE DATABASE datafusion
    WITH ENCODING 'UTF8'
    LC_COLLATE = 'es_MX.UTF-8'
    TEMPLATE = template0;

-- Conectarse a la base de datos:  \c datafusion

-- 2. Crear tabla de videojuegos
CREATE TABLE IF NOT EXISTS videojuegos (
    id       SERIAL        PRIMARY KEY,
    nombre   VARCHAR(150)  NOT NULL,
    genero   VARCHAR(80)   NOT NULL,   -- se mapeará a "categoria"
    precio   DECIMAL(10,2) NOT NULL,   -- se mapeará a "valor"
    fecha    DATE          NOT NULL    -- fecha de lanzamiento
);

-- 3. Insertar datos de muestra
INSERT INTO videojuegos (nombre, genero, precio, fecha) VALUES
('Doom: The Dark Ages',       'Shooter',      69.99,  '2025-05-15'),
('Grand Theft Auto VI',        'Aventura',     79.99,  '2025-10-01'),
('Death Stranding 2',          'Aventura',     69.99,  '2025-03-26'),
('Ghost of Tsushima 2',        'Acción',       59.99,  '2025-08-12'),
('FromSoftware Next Title',    'RPG',          69.99,  '2025-11-20'),
('Borderlands 4',              'Shooter',      59.99,  '2025-09-23'),
('The Witcher 4',              'RPG',          69.99,  '2025-12-05'),
('Metal Gear Solid Delta',     'Sigilo',       59.99,  '2025-08-28'),
('Fable 2025',                 'RPG',          69.99,  '2025-07-15'),
('Assassins Creed Shadows',    'Aventura',     69.99,  '2025-03-20'),
('Street Fighter 6 DLC Pack',  'Pelea',        29.99,  '2025-04-01'),
('Tekken 8 Season 2',          'Pelea',        19.99,  '2025-02-14'),
('No Mans Sky Beyond 2',       'Supervivencia',39.99,  '2025-06-30'),
('Stardew Valley 2',           'Simulación',   14.99,  '2025-05-01'),
('Hollow Knight 2 Finale',     'Metroidvania', 29.99,  '2025-09-10');

-- 4. Verificar inserción
SELECT COUNT(*) AS total_registros FROM videojuegos;
SELECT * FROM videojuegos ORDER BY fecha DESC LIMIT 5;
