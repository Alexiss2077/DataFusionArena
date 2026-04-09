-- ══════════════════════════════════════════════════════════════
--  Data Fusion Arena  ·  Script de configuración  ·  MariaDB
--  Ejecutar en HeidiSQL conectado a MariaDB (root / localhost:3306)
-- ══════════════════════════════════════════════════════════════

-- 1. Crear base de datos
CREATE DATABASE IF NOT EXISTS `datafusion`
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_spanish_ci;

USE `datafusion`;

-- 2. Crear tabla de puntuaciones (high scores)
CREATE TABLE IF NOT EXISTS `puntuaciones` (
    `id`             INT AUTO_INCREMENT PRIMARY KEY,
    `jugador`        VARCHAR(100) NOT NULL,   -- se mapeará a "nombre"
    `nivel`          VARCHAR(50)  NOT NULL,   -- se mapeará a "categoria"
    `score`          DECIMAL(12,2) NOT NULL,  -- se mapeará a "valor"
    `fecha_registro` DATE NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 3. Insertar datos de muestra
INSERT INTO `puntuaciones` (`jugador`, `nivel`, `score`, `fecha_registro`) VALUES
('AlphaGamer2025',    'Maestro',      1050000.00, '2025-01-15'),
('BetaTester_X',      'Experto',       920500.00, '2025-01-20'),
('GammaSniperX',      'Maestro',      1100000.00, '2025-02-01'),
('DeltaForceHero',    'Avanzado',      780000.00, '2025-02-10'),
('EpsilonBlade99',    'Avanzado',      830000.00, '2025-02-28'),
('ZetaWingman',       'Intermedio',    560000.00, '2025-03-05'),
('EtaRocketLeague',   'Experto',       950000.00, '2025-03-15'),
('ThetaStormX',       'Maestro',      1200000.00, '2025-03-20'),
('IotaFlameWar',      'Avanzado',      710000.00, '2025-04-01'),
('KappaLegendPro',    'Experto',       880000.00, '2025-04-05'),
('LambdaCodeBreaker', 'Maestro',      1350000.00, '2025-04-10'),
('MuDragonborn',      'Principiante',  220000.00, '2025-01-05'),
('NuChaosAgent',      'Avanzado',      695000.00, '2025-03-30'),
('XiPixelPerfect',    'Experto',       875000.00, '2025-04-02'),
('OmicronPulsarX',    'Maestro',      1450000.00, '2025-04-08');

-- 4. Verificar
SELECT COUNT(*) AS total FROM `puntuaciones`;
SELECT * FROM `puntuaciones` ORDER BY `score` DESC LIMIT 5;
