-- =====================================================================================
-- FONDO XYZ - SCRIPT MAESTRO DE BASE DE DATOS
-- Versión: 2.0 con ASP.NET Core Identity integrado
-- INSTRUCCIÓN: Ejecutar este script COMPLETO en SQL Server Management Studio
--              antes de lanzar el proyecto por primera vez.
--
-- NOTA: El nombre de la base de datos es FondoXYZ_DB, que corresponde al valor
--       configurado en appsettings.json (ConnectionStrings.DefaultConnection).
-- =====================================================================================

USE master;
GO

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'FondoXYZ_DB')
BEGIN
    CREATE DATABASE FondoXYZ_DB;
    PRINT 'Base de datos FondoXYZ_DB creada exitosamente.';
END
ELSE
BEGIN
    PRINT 'La base de datos FondoXYZ_DB ya existe. Se procederá a recrear las tablas.';
END
GO

USE FondoXYZ_DB;
GO

-- =====================================================================================
-- PASO 1: ELIMINAR TABLAS EN ORDEN (respetando FK) PARA RECREACIÓN LIMPIA
-- =====================================================================================
IF OBJECT_ID('UsuarioTokens', 'U') IS NOT NULL   DROP TABLE UsuarioTokens;
IF OBJECT_ID('UsuarioLogins', 'U') IS NOT NULL   DROP TABLE UsuarioLogins;
IF OBJECT_ID('UsuarioClaims', 'U') IS NOT NULL   DROP TABLE UsuarioClaims;
IF OBJECT_ID('RoleClaims', 'U') IS NOT NULL      DROP TABLE RoleClaims;
IF OBJECT_ID('UsuarioRoles', 'U') IS NOT NULL    DROP TABLE UsuarioRoles;
IF OBJECT_ID('Reservas', 'U') IS NOT NULL        DROP TABLE Reservas;
IF OBJECT_ID('Tarifas', 'U') IS NOT NULL         DROP TABLE Tarifas;
IF OBJECT_ID('Alojamientos', 'U') IS NOT NULL    DROP TABLE Alojamientos;
IF OBJECT_ID('Sedes', 'U') IS NOT NULL           DROP TABLE Sedes;
IF OBJECT_ID('Usuarios', 'U') IS NOT NULL        DROP TABLE Usuarios;
IF OBJECT_ID('Roles', 'U') IS NOT NULL           DROP TABLE Roles;
IF OBJECT_ID('Temporadas', 'U') IS NOT NULL      DROP TABLE Temporadas;
IF OBJECT_ID('Festivos', 'U') IS NOT NULL        DROP TABLE Festivos;
PRINT 'Tablas anteriores eliminadas.';
GO

-- =====================================================================================
-- PASO 2: CREAR TABLAS DE NEGOCIO
-- =====================================================================================

-- 2.1 Temporadas y Festivos (no tienen FK, van primero)
CREATE TABLE Temporadas (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(100) NOT NULL,
    FechaInicio DATE NOT NULL,
    FechaFin DATE NOT NULL,
    Descripcion NVARCHAR(MAX) NULL,
    EsAltaTemporada BIT NOT NULL DEFAULT 1,
    Activa BIT NOT NULL DEFAULT 1
);
GO

CREATE TABLE Festivos (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Fecha DATE NOT NULL UNIQUE,
    Nombre NVARCHAR(150) NOT NULL,
    EsAltaTemporada BIT NOT NULL DEFAULT 1
);
GO

-- 2.2 Tabla Roles de Identity
CREATE TABLE Roles (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name NVARCHAR(256) NULL,
    NormalizedName NVARCHAR(256) NULL,
    ConcurrencyStamp NVARCHAR(MAX) NULL
);
GO

-- 2.3 Tabla Usuarios — Esquema ASP.NET Core Identity (IdentityUser<int>)
--     Campos de negocio: solo los que se usan activamente en el aplicativo.
CREATE TABLE Usuarios (
    -- Columnas requeridas por ASP.NET Core Identity
    Id                   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserName             NVARCHAR(256) NULL,
    NormalizedUserName   NVARCHAR(256) NULL,
    Email                NVARCHAR(256) NULL,
    NormalizedEmail      NVARCHAR(256) NULL,
    EmailConfirmed       BIT NOT NULL DEFAULT 0,
    PasswordHash         NVARCHAR(MAX) NULL,
    SecurityStamp        NVARCHAR(MAX) NULL,
    ConcurrencyStamp     NVARCHAR(MAX) NULL,
    PhoneNumber          NVARCHAR(MAX) NULL,
    PhoneNumberConfirmed BIT NOT NULL DEFAULT 0,
    TwoFactorEnabled     BIT NOT NULL DEFAULT 0,
    LockoutEnd           DATETIMEOFFSET NULL,
    LockoutEnabled       BIT NOT NULL DEFAULT 0,
    AccessFailedCount    INT NOT NULL DEFAULT 0,
    -- Columnas de negocio del Fondo XYZ (usadas en Registro y sesión)
    NroDocumento         NVARCHAR(50) NOT NULL UNIQUE,
    NombreCompleto       NVARCHAR(150) NOT NULL,
    FechaRegistro        DATETIME DEFAULT GETDATE()
);
GO

-- 2.4 Tablas auxiliares de Identity
CREATE TABLE UsuarioRoles (
    UserId INT NOT NULL,
    RoleId INT NOT NULL,
    PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_UsuarioRoles_Roles    FOREIGN KEY (RoleId) REFERENCES Roles(Id)    ON DELETE CASCADE,
    CONSTRAINT FK_UsuarioRoles_Usuarios FOREIGN KEY (UserId)  REFERENCES Usuarios(Id) ON DELETE CASCADE
);
GO

CREATE TABLE UsuarioClaims (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId INT NOT NULL,
    ClaimType  NVARCHAR(MAX) NULL,
    ClaimValue NVARCHAR(MAX) NULL,
    CONSTRAINT FK_UsuarioClaims_Usuarios FOREIGN KEY (UserId) REFERENCES Usuarios(Id) ON DELETE CASCADE
);
GO

CREATE TABLE UsuarioLogins (
    LoginProvider       NVARCHAR(128) NOT NULL,
    ProviderKey         NVARCHAR(128) NOT NULL,
    ProviderDisplayName NVARCHAR(MAX) NULL,
    UserId INT NOT NULL,
    PRIMARY KEY (LoginProvider, ProviderKey),
    CONSTRAINT FK_UsuarioLogins_Usuarios FOREIGN KEY (UserId) REFERENCES Usuarios(Id) ON DELETE CASCADE
);
GO

CREATE TABLE RoleClaims (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    RoleId INT NOT NULL,
    ClaimType  NVARCHAR(MAX) NULL,
    ClaimValue NVARCHAR(MAX) NULL,
    CONSTRAINT FK_RoleClaims_Roles FOREIGN KEY (RoleId) REFERENCES Roles(Id) ON DELETE CASCADE
);
GO

CREATE TABLE UsuarioTokens (
    UserId        INT NOT NULL,
    LoginProvider NVARCHAR(128) NOT NULL,
    Name          NVARCHAR(128) NOT NULL,
    Value         NVARCHAR(MAX) NULL,
    PRIMARY KEY (UserId, LoginProvider, Name),
    CONSTRAINT FK_UsuarioTokens_Usuarios FOREIGN KEY (UserId) REFERENCES Usuarios(Id) ON DELETE CASCADE
);
GO

-- 2.5 Tabla Sedes
CREATE TABLE Sedes (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    Nombre       NVARCHAR(150) NOT NULL UNIQUE,
    Descripcion  NVARCHAR(MAX) NULL,
    Ubicacion    NVARCHAR(200) NULL,
    Tipo         NVARCHAR(100) NOT NULL,  -- 'Sede Recreativa' | 'Apartamento'
    CapacidadMaxima INT NOT NULL
);
GO

-- 2.6 Tabla Alojamientos
CREATE TABLE Alojamientos (
    Id                INT IDENTITY(1,1) PRIMARY KEY,
    SedeId            INT NOT NULL FOREIGN KEY REFERENCES Sedes(Id),
    Nombre            NVARCHAR(150) NOT NULL,
    Descripcion       NVARCHAR(MAX) NULL,
    NumeroHabitaciones INT NOT NULL,
    CapacidadMaxima   INT NOT NULL,
    EstadoDisponible  BIT DEFAULT 1
);
GO

-- 2.7 Tabla Tarifas
CREATE TABLE Tarifas (
    Id                    INT IDENTITY(1,1) PRIMARY KEY,
    SedeId                INT NOT NULL FOREIGN KEY REFERENCES Sedes(Id),
    Descripcion           NVARCHAR(150) NOT NULL,
    ValorBase             DECIMAL(18,2) NOT NULL,
    CapacidadBase         INT NOT NULL,
    ValorPersonaAdicional DECIMAL(18,2) NOT NULL DEFAULT 0,
    AplicaDia             INT NULL,     -- 1=Lunes, 5=Viernes (referencia)
    Temporada             NVARCHAR(100) NULL  -- 'Especial' | 'Ordinaria' | 'Normal' | 'Baja Temporada' | 'Alta Temporada'
);
GO

-- 2.8 Tabla Reservas
CREATE TABLE Reservas (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    UsuarioId        INT NOT NULL FOREIGN KEY REFERENCES Usuarios(Id),
    SedeId           INT NOT NULL FOREIGN KEY REFERENCES Sedes(Id),
    AlojamientoId    INT NOT NULL FOREIGN KEY REFERENCES Alojamientos(Id),
    FechaLlegada     DATE NOT NULL,
    FechaSalida      DATE NOT NULL,
    CantidadPersonas INT NOT NULL,
    IncluyeLavanderia BIT DEFAULT 0,
    CostoTotal       DECIMAL(18,2) NOT NULL,
    EstadoReserva    NVARCHAR(50) DEFAULT 'Pendiente',  -- Pendiente | Confirmada | Cancelada
    FechaReserva     DATETIME DEFAULT GETDATE(),
    FechaPago        DATETIME NULL
);
GO

PRINT 'Todas las tablas creadas exitosamente.';
GO

-- =====================================================================================
-- PASO 3: DATOS DE PRUEBA — USUARIO ADMINISTRADOR
-- Documento: 12345678 / Contraseña: Admin1234!
-- PasswordHash generado por ASP.NET Core Identity v3 (BCrypt PBKDF2)
-- =====================================================================================
INSERT INTO Usuarios (
    UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
    PasswordHash, SecurityStamp, ConcurrencyStamp,
    PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount,
    NroDocumento, NombreCompleto
)
VALUES (
    '12345678',
    '12345678',
    'admin@fondoxyz.com',
    'ADMIN@FONDOXYZ.COM',
    1,
    'AQAAAAIAAYagAAAAEK9Z1P6b3wJEkTGRvh3+VFvL9eMjABF2Gy9I2VRXlx1eD3LfA8nYXgJ2K5Mk3FGWSA==',
    NEWID(),
    NEWID(),
    '3001234567',
    0, 0, 1, 0,
    '12345678',
    'Administrador del Sistema'
);
PRINT 'Usuario de prueba insertado: Documento=12345678 / Contraseña=Admin1234!';
GO

-- =====================================================================================
-- PASO 4: SEDES (8 en total: 6 recreativas + 2 apartamentos)
-- =====================================================================================
INSERT INTO Sedes (Nombre, Descripcion, Ubicacion, Tipo, CapacidadMaxima) VALUES
('Villeta',          'Sede Recreativa Villeta. Ocho habitaciones, cada una con cama doble, camarote, baño, nevera, TV y terraza.', 'Villeta, Cundinamarca',               'Sede Recreativa', 32),
('El Placer',        'Sede Recreativa El Placer - Fusagasugá. Alojamientos individuales y bloque de cabañas.',                    'Fusagasugá, Cundinamarca',            'Sede Recreativa', 34),
('Gonzalo Morante',  'Sede Recreativa Gonzalo Morante - Chinchiná. Alojamientos y cabañas tipo A y B.',                           'Chinchiná, Caldas',                   'Sede Recreativa', 30),
('Tablones',         'Sede Recreativa Tablones - Palmira. Cuatro alojamientos con habitaciones y cocineta.',                      'Palmira, Valle del Cauca',            'Sede Recreativa', 24),
('Manguruma',        'Sede Recreativa Manguruma - Santa Fe de Antioquia. Alojamientos clásicos y bloque nuevo.',                  'Santa Fe de Antioquia, Antioquia',    'Sede Recreativa', 46),
('Federman',         'Sede Recreativa Federman - Bogotá. Zona húmeda, gimnasio, billar y salas de esparcimiento.',               'Bogotá, Cundinamarca',                'Sede Recreativa', 16),
('Suramericana',     'Apartamentos Suramericana - Medellín. 5 habitaciones con camas sencillas y baños privados.',               'Medellín, Antioquia',                 'Apartamento',    9),
('El Rodadero',      'Apartamentos El Rodadero - Santa Marta. Tres apartamentos con sala-comedor, cocina y parqueadero.',        'Santa Marta, Magdalena',              'Apartamento',    20);
PRINT 'Sedes insertadas (8 total)';
GO

-- =====================================================================================
-- PASO 5: ALOJAMIENTOS
-- =====================================================================================
-- VILLETA (SedeId=1): 8 habitaciones
INSERT INTO Alojamientos (SedeId, Nombre, Descripcion, NumeroHabitaciones, CapacidadMaxima) VALUES
(1, 'Alojamiento 1', 'Habitación con cama doble y camarote, baño, nevera, TV y terraza cubierta.', 1, 4),
(1, 'Alojamiento 2', 'Habitación con cama doble y camarote, baño, nevera, TV y terraza cubierta.', 1, 4),
(1, 'Alojamiento 3', 'Habitación con cama doble y camarote, baño, nevera, TV y terraza cubierta.', 1, 4),
(1, 'Alojamiento 4', 'Habitación con cama doble y camarote, baño, nevera, TV y terraza cubierta.', 1, 4),
(1, 'Alojamiento 5', 'Habitación con cama doble y camarote, baño, nevera, TV y terraza cubierta.', 1, 4),
(1, 'Alojamiento 6', 'Habitación con cama doble y camarote, baño, nevera, TV y terraza cubierta.', 1, 4),
(1, 'Alojamiento 7', 'Habitación con cama doble y camarote, baño, nevera, TV y terraza cubierta.', 1, 4),
(1, 'Alojamiento 8', 'Habitación con cama doble y camarote, baño, nevera, TV y terraza cubierta.', 1, 4);

-- EL PLACER - FUSAGASUGÁ (SedeId=2)
INSERT INTO Alojamientos (SedeId, Nombre, Descripcion, NumeroHabitaciones, CapacidadMaxima) VALUES
(2, 'Alojamiento 1',  'Dos habitaciones, baño y TV: cama doble + sencilla, y otra con cama sencilla.',                 2, 4),
(2, 'Alojamiento 2',  'Dos habitaciones, baño y TV: cama doble y cuatro camas sencillas.',                             2, 6),
(2, 'Alojamiento 3',  'Una habitación con cama doble y 2 sencillas, baño y TV.',                                       1, 4),
(2, 'Alojamiento 4',  'Dos habitaciones, baño y TV: cama doble+sencilla y una sencilla.',                              2, 4),
(2, 'Cabaña 5',       'Cabaña: sala con sofá-cama, TV, baño, habitación con cama doble y sencilla, cocineta, terraza.', 2, 4),
(2, 'Cabaña 6',       'Cabaña: sala con sofá-cama, TV, baño, habitación con cama doble y sencilla, cocineta, terraza.', 2, 4),
(2, 'Cabaña 7',       'Cabaña: sala con sofá-cama, TV, baño, habitación con cama doble y sencilla, cocineta, terraza.', 2, 4),
(2, 'Cabaña 8',       'Cabaña: sala con sofá-cama, TV, baño, habitación con cama doble y sencilla, cocineta, terraza.', 2, 4);

-- GONZALO MORANTE - CHINCHINÁ (SedeId=3)
INSERT INTO Alojamientos (SedeId, Nombre, Descripcion, NumeroHabitaciones, CapacidadMaxima) VALUES
(3, 'Alojamiento 1',  'Cocineta, baño, TV, 2 hab (2 sencillas + 2 adicionales | doble + 1 sencilla).',                 2, 7),
(3, 'Alojamiento 2',  'Cocineta, baño, TV, 2 hab (doble + auxiliar doble | 2 sencillas + 2 auxiliares).',              2, 8),
(3, 'Alojamiento 3 (Cabaña A)', 'Cocineta, 2 baños, sala-comedor, TV, 2 hab (doble | 2 sencillas + 2 auxiliares).',    2, 6),
(3, 'Alojamiento 4',  'Cocineta, baño, TV, 1 habitación con cama doble y sencilla.',                                   1, 3),
(3, 'Alojamiento 5 (Cabaña B)', 'Cocineta, baño, sala con sofá, TV, hab con doble y sencilla.',                        1, 3),
(3, 'Alojamiento 6 (Cabaña B)', 'Cocineta, baño, sala con sofá, TV, hab con doble y sencilla.',                        1, 3);

-- TABLONES - PALMIRA (SedeId=4)
INSERT INTO Alojamientos (SedeId, Nombre, Descripcion, NumeroHabitaciones, CapacidadMaxima) VALUES
(4, 'Alojamiento 1', 'Una habitación con cama doble y camarote. TV, baño, cocineta, nevera y comedor.',                1, 4),
(4, 'Alojamiento 2', 'Una habitación con cama doble y camarote. TV, baño y cocineta con nevera.',                      1, 4),
(4, 'Alojamiento 3', 'Dos habitaciones (doble+camarote | dos camarotes). Sala-TV, baño y cocineta.',                   2, 8),
(4, 'Alojamiento 4', 'Dos habitaciones (doble+camarote | dos camarotes). Sala-TV, baño y cocineta.',                   2, 8);

-- MANGURUMA - STA. FE DE ANTIOQUIA (SedeId=5)
INSERT INTO Alojamientos (SedeId, Nombre, Descripcion, NumeroHabitaciones, CapacidadMaxima) VALUES
(5, 'Alojamiento 1',  'Cama doble y camarote. Baño, terraza y TV.',                                                    1, 4),
(5, 'Alojamiento 2',  'Cama doble, camarote y sofá-cama. Baño, terraza y TV.',                                         1, 5),
(5, 'Alojamiento 3',  'Cama doble, camarote y sofá-cama. Baño, terraza y TV.',                                         1, 5),
(5, 'Bloque Nuevo 1', 'Dos camas gemelas + camarote, baño, terraza-comedor, cocina, nevera y TV. (Bloque Nuevo)',      2, 4),
(5, 'Bloque Nuevo 2', 'Dos camas gemelas + camarote, baño, terraza-comedor, cocina, nevera y TV. (Bloque Nuevo)',      2, 4),
(5, 'Bloque Nuevo 3', 'Dos camas gemelas + camarote, baño, terraza-comedor, cocina, nevera y TV. (Bloque Nuevo)',      2, 4),
(5, 'Bloque Nuevo 4', 'Dos camas gemelas + camarote, baño, terraza-comedor, cocina, nevera y TV. (Bloque Nuevo)',      2, 4),
(5, 'Bloque Nuevo 5', 'Dos camas gemelas + camarote, baño, terraza-comedor, cocina, nevera y TV. (Bloque Nuevo)',      2, 4),
(5, 'Bloque Nuevo 6', 'Dos camas gemelas + camarote, baño, terraza-comedor, cocina, nevera y TV. (Bloque Nuevo)',      2, 4),
(5, 'Bloque Nuevo 7', 'Dos camas gemelas + camarote, baño, terraza-comedor, cocina, nevera y TV. (Bloque Nuevo)',      2, 4),
(5, 'Bloque Nuevo 8', 'Dos camas gemelas + camarote, baño, terraza-comedor, cocina, nevera y TV. (Bloque Nuevo)',      2, 4);

-- FEDERMAN - BOGOTÁ (SedeId=6)
INSERT INTO Alojamientos (SedeId, Nombre, Descripcion, NumeroHabitaciones, CapacidadMaxima) VALUES
(6, 'Alojamiento 1', 'Habitación para asociados. Acceso a zona húmeda, gimnasio, salas y cafetería.', 1, 2),
(6, 'Alojamiento 2', 'Habitación para asociados. Acceso a zona húmeda, gimnasio, salas y cafetería.', 1, 2),
(6, 'Alojamiento 3', 'Habitación para asociados. Acceso a zona húmeda, gimnasio, salas y cafetería.', 1, 2),
(6, 'Alojamiento 4', 'Habitación para asociados. Acceso a zona húmeda, gimnasio, salas y cafetería.', 1, 2);

-- SURAMERICANA - MEDELLÍN (SedeId=7)
INSERT INTO Alojamientos (SedeId, Nombre, Descripcion, NumeroHabitaciones, CapacidadMaxima) VALUES
(7, 'Habitación 1', '2 camas sencillas y baño privado.',                  1, 2),
(7, 'Habitación 2', '2 camas sencillas. Baño compartido.',                1, 2),
(7, 'Habitación 3', '2 camas sencillas. Baño compartido.',                1, 2),
(7, 'Habitación 4', '2 camas sencillas. Baño compartido.',                1, 2),
(7, 'Habitación 5', '1 cama sencilla y baño privado (capacidad 1).',      1, 1);

-- EL RODADERO - SANTA MARTA (SedeId=8)
INSERT INTO Alojamientos (SedeId, Nombre, Descripcion, NumeroHabitaciones, CapacidadMaxima) VALUES
(8, 'Apartamento 202', 'Sala-comedor, cocina, 2 baños, 3 habitaciones y parqueadero. Capacidad máxima: 8 personas.', 3, 8),
(8, 'Apartamento 301', 'Sala-comedor, cocina, 1 baño, 2 habitaciones y parqueadero. Capacidad máxima: 6 personas.',  2, 6),
(8, 'Apartamento 401', 'Sala-comedor, cocina, 1 baño, 2 habitaciones y parqueadero. Capacidad máxima: 6 personas.',  2, 6);

PRINT 'Alojamientos insertados correctamente.';
GO

-- =====================================================================================
-- PASO 6: TARIFAS
-- Sedes Recreativas (1-5): Tarifa Especial L-J / Tarifa Ordinaria resto de días
-- Federman (6): Tarifa propia
-- Medellín (7): Por número de personas (1 o 2)
-- Santa Marta (8): Por apartamento y temporada (Alta/Baja)
-- =====================================================================================

-- SEDES RECREATIVAS 1 a 5 (Villeta, El Placer, Gonzalo Morante, Tablones, Manguruma)
-- Tarifa Especial: $27.000 (1 hab) / $37.000 (2 hab) → Lunes a Jueves, sin festivos ni alta temporada
-- Tarifa Ordinaria: $70.000 (1 hab) / $90.000 (2 hab) → Viernes a Domingo, festivos y alta temporada
-- Persona adicional (>4): $11.000 en especial, $16.000 en ordinaria
DECLARE @sedeId INT = 1;
WHILE @sedeId <= 5
BEGIN
    INSERT INTO Tarifas (SedeId, Descripcion, ValorBase, CapacidadBase, ValorPersonaAdicional, AplicaDia, Temporada) VALUES
    (@sedeId, 'Alojamiento (1) - Tarifa Especial (L-J)',    27000, 4, 11000, 1, 'Especial'),
    (@sedeId, 'Alojamiento (2) - Tarifa Especial (L-J)',    37000, 4, 11000, 1, 'Especial'),
    (@sedeId, 'Alojamiento (1) - Tarifa Ordinaria (V-D)',   70000, 4, 16000, 5, 'Ordinaria'),
    (@sedeId, 'Alojamiento (2) - Tarifa Ordinaria (V-D)',   90000, 4, 16000, 5, 'Ordinaria');
    SET @sedeId = @sedeId + 1;
END

-- FEDERMAN (6) — Tarifa propia (sin datos oficiales en el documento; usamos referencia razonable)
INSERT INTO Tarifas (SedeId, Descripcion, ValorBase, CapacidadBase, ValorPersonaAdicional, AplicaDia, Temporada) VALUES
(6, 'Habitación - Tarifa Especial (L-J)',   50000, 2, 0, 1, 'Especial'),
(6, 'Habitación - Tarifa Ordinaria (V-D)',  80000, 2, 0, 5, 'Ordinaria');

-- SURAMERICANA MEDELLÍN (7) — Tarifa por persona (sin temporada diferenciada)
INSERT INTO Tarifas (SedeId, Descripcion, ValorBase, CapacidadBase, ValorPersonaAdicional, Temporada) VALUES
(7, 'Medellín 1 Persona / noche',   63000, 1, 0, 'Normal'),
(7, 'Medellín 2 Personas / noche',  75000, 2, 0, 'Normal');

-- EL RODADERO SANTA MARTA (8) — Por apartamento y temporada
INSERT INTO Tarifas (SedeId, Descripcion, ValorBase, CapacidadBase, ValorPersonaAdicional, Temporada) VALUES
(8, 'Apto 301-401 Baja Temporada / noche',   89000, 6, 0, 'Baja Temporada'),
(8, 'Apto 202 Baja Temporada / noche',      103000, 8, 0, 'Baja Temporada'),
(8, 'Apto 301-401 Alta Temporada / noche',  124000, 6, 0, 'Alta Temporada'),
(8, 'Apto 202 Alta Temporada / noche',      143000, 8, 0, 'Alta Temporada');

PRINT 'Tarifas insertadas correctamente.';
GO

-- =====================================================================================
-- PASO 7: TEMPORADAS 2025-2026 (Colombia)
-- =====================================================================================
INSERT INTO Temporadas (Nombre, FechaInicio, FechaFin, Descripcion, EsAltaTemporada, Activa) VALUES
('Navidad y Reyes',             '2025-12-15', '2026-01-15', 'Período navideño y de reyes',                   1, 1),
('Baja - Enero/Mayo',           '2026-01-16', '2026-05-24', 'Período escolar. Tarifa especial disponible.',  0, 1),
('Semana Santa',                '2026-03-29', '2026-04-05', 'Semana Santa. Todos los días = Tarifa Ordinaria', 1, 1),
('Puente festivo mayo',         '2026-05-14', '2026-05-18', 'Puente festivo de mayo 2026',                   1, 1),
('Alta - Vacaciones Escolares', '2026-06-01', '2026-07-31', 'Vacaciones escolares. Alta temporada.',         1, 1),
('Baja - Agosto/Noviembre',     '2026-08-01', '2026-11-29', 'Período escolar. Tarifa especial disponible.',  0, 1),
('Alta - Fin de Año',           '2026-12-01', '2026-12-31', 'Temporada alta de fin de año.',                 1, 1);
PRINT 'Temporadas insertadas.';
GO

-- =====================================================================================
-- PASO 8: FESTIVOS COLOMBIA 2026
-- =====================================================================================
INSERT INTO Festivos (Fecha, Nombre, EsAltaTemporada) VALUES
('2026-01-01', 'Año Nuevo',                      1),
('2026-01-12', 'Reyes Magos (trasladado)',        1),
('2026-03-23', 'San José (trasladado)',           1),
('2026-04-02', 'Jueves Santo',                   1),
('2026-04-03', 'Viernes Santo',                  1),
('2026-05-01', 'Día del Trabajo',                1),
('2026-05-18', 'Ascensión del Señor',            1),
('2026-06-08', 'Corpus Christi',                 1),
('2026-06-15', 'Sagrado Corazón',                1),
('2026-06-29', 'San Pedro y San Pablo',          1),
('2026-07-20', 'Día de la Independencia',        1),
('2026-08-07', 'Batalla de Boyacá',              1),
('2026-08-17', 'Asunción de la Virgen',          1),
('2026-10-12', 'Día de la Raza',                 1),
('2026-11-02', 'Todos los Santos',               1),
('2026-11-16', 'Independencia de Cartagena',     1),
('2026-12-08', 'Inmaculada Concepción',          1),
('2026-12-25', 'Navidad',                        1);
PRINT 'Festivos colombianos 2026 insertados.';
GO

-- =====================================================================================
-- PASO 9: VERIFICACIÓN FINAL
-- =====================================================================================
PRINT '========================================';
PRINT 'VERIFICACIÓN DE DATOS INSERTADOS:';
SELECT 'Usuarios'     AS Tabla, COUNT(*) AS Total FROM Usuarios     UNION ALL
SELECT 'Sedes',               COUNT(*) FROM Sedes                   UNION ALL
SELECT 'Alojamientos',        COUNT(*) FROM Alojamientos            UNION ALL
SELECT 'Tarifas',             COUNT(*) FROM Tarifas                 UNION ALL
SELECT 'Temporadas',          COUNT(*) FROM Temporadas              UNION ALL
SELECT 'Festivos',            COUNT(*) FROM Festivos;
PRINT '========================================';
PRINT 'SCRIPT MAESTRO COMPLETADO EXITOSAMENTE.';
PRINT 'Usuario de prueba: Documento=12345678 / Contraseña=Admin1234!';
PRINT '========================================';
GO