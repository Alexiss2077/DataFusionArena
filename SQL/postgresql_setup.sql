-- ══════════════════════════════════════════════════════════════
--  Data Fusion Arena  ·  Script de configuración  ·  PostgreSQL
--  Fuente de datos: Kaggle – Video Game Sales (vgsales.csv)
--  URL: https://www.kaggle.com/datasets/gregorut/videogamesales
--  Ejecutar en HeidiSQL o psql como usuario postgres
-- ══════════════════════════════════════════════════════════════

-- 1. Crear base de datos
CREATE DATABASE datafusion
    WITH ENCODING 'UTF8'
    TEMPLATE = template0;

-- Conectarse a la base de datos:  \c datafusion

-- 2. Crear tabla de videojuegos
CREATE TABLE IF NOT EXISTS videojuegos (
    id            SERIAL        PRIMARY KEY,
    nombre        VARCHAR(150)  NOT NULL,       -- nombre del juego
    genero        VARCHAR(80)   NOT NULL,        -- se mapea a "categoria"
    ventas_global DECIMAL(8,2)  NOT NULL,        -- millones de unidades, se mapea a "valor"
    anio          DATE          NOT NULL,        -- año de lanzamiento
    plataforma    VARCHAR(30),
    publicador    VARCHAR(100),
    ventas_na     DECIMAL(8,2),
    ventas_eu     DECIMAL(8,2),
    ventas_jp     DECIMAL(8,2)
);

-- 3. Insertar datos reales del dataset vgsales (top 20 por ventas globales)
INSERT INTO videojuegos (nombre, genero, ventas_global, anio, plataforma, publicador, ventas_na, ventas_eu, ventas_jp) VALUES
('Wii Sports',                     'Sports',       82.74, '2006-01-01', 'Wii',   'Nintendo',               41.49, 29.02, 3.77),
('Super Mario Bros.',              'Platform',     40.24, '1985-01-01', 'NES',   'Nintendo',               29.08,  3.58, 6.81),
('Mario Kart Wii',                 'Racing',       35.82, '2008-01-01', 'Wii',   'Nintendo',               15.85, 12.88, 3.79),
('Wii Sports Resort',              'Sports',       33.00, '2009-01-01', 'Wii',   'Nintendo',               15.75, 11.01, 3.28),
('Pokemon Red/Pokemon Blue',       'Role-Playing', 31.37, '1996-01-01', 'GB',    'Nintendo',               11.27,  8.89,10.22),
('Tetris',                         'Puzzle',       30.26, '1989-01-01', 'GB',    'Nintendo',               23.20,  2.26, 4.22),
('New Super Mario Bros.',          'Platform',     30.01, '2006-01-01', 'DS',    'Nintendo',               11.38,  9.23, 6.50),
('Wii Play',                       'Misc',         29.02, '2006-01-01', 'Wii',   'Nintendo',               14.03,  9.20, 2.93),
('New Super Mario Bros. Wii',      'Platform',     28.62, '2009-01-01', 'Wii',   'Nintendo',               14.59,  7.06, 4.70),
('Duck Hunt',                      'Shooter',      28.31, '1984-01-01', 'NES',   'Nintendo',               26.93,  0.63, 0.28),
('Kinect Adventures!',             'Misc',         22.29, '2010-01-01', 'X360',  'Microsoft Game Studios', 15.00,  5.22, 0.44),
('Pokemon Gold/Pokemon Silver',    'Role-Playing', 23.10, '1999-01-01', 'GB',    'Nintendo',                9.00,  6.18, 7.20),
('Wii Fit',                        'Sports',       22.72, '2007-01-01', 'Wii',   'Nintendo',                8.94,  8.03, 3.60),
('Wii Fit Plus',                   'Sports',       21.99, '2009-01-01', 'Wii',   'Nintendo',                9.09,  8.59, 2.53),
('Grand Theft Auto V',             'Action',       21.40, '2013-01-01', 'PS3',   'Take-Two Interactive',    7.01,  9.27, 0.97),
('Super Mario World',              'Platform',     20.61, '1990-01-01', 'SNES',  'Nintendo',               12.78,  3.75, 3.54),
('Grand Theft Auto: San Andreas',  'Action',       20.81, '2004-01-01', 'PS2',   'Take-Two Interactive',    9.43,  0.40, 0.41),
('Brain Age: Train Your Brain',    'Misc',         20.22, '2005-01-01', 'DS',    'Nintendo',                4.75,  9.14, 4.16),
('Pokemon Diamond/Pokemon Pearl',  'Role-Playing', 18.36, '2006-01-01', 'DS',    'Nintendo',                9.85,  5.40, 6.04),
('Pokemon Ruby/Pokemon Sapphire',  'Role-Playing', 16.22, '2002-01-01', 'GBA',   'Nintendo',                6.42,  4.23, 5.00);

-- 4. Verificar
SELECT COUNT(*) AS total_registros FROM videojuegos;
SELECT nombre, genero AS categoria, ventas_global AS valor, anio AS fecha
FROM videojuegos ORDER BY ventas_global DESC LIMIT 5;
