USE DB_FondoXYZ;
GO

-- 1. Renombrar columnas existentes de Usuarios a las de Identity
EXEC sp_rename 'Usuarios.DireccionEmail', 'Email', 'COLUMN';
EXEC sp_rename 'Usuarios.Celular', 'PhoneNumber', 'COLUMN';
EXEC sp_rename 'Usuarios.Clave', 'PasswordHash', 'COLUMN';
GO

-- 2. Modificar tipos de datos si es necesario
ALTER TABLE Usuarios ALTER COLUMN PhoneNumber NVARCHAR(MAX) NULL;
GO

-- 3. Agregar columnas requeridas por Identity
ALTER TABLE Usuarios ADD 
    UserName NVARCHAR(256) NULL,
    NormalizedUserName NVARCHAR(256) NULL,
    NormalizedEmail NVARCHAR(256) NULL,
    EmailConfirmed BIT NOT NULL DEFAULT 0,
    SecurityStamp NVARCHAR(MAX) NULL,
    ConcurrencyStamp NVARCHAR(MAX) NULL,
    PhoneNumberConfirmed BIT NOT NULL DEFAULT 0,
    TwoFactorEnabled BIT NOT NULL DEFAULT 0,
    LockoutEnd DATETIMEOFFSET NULL,
    LockoutEnabled BIT NOT NULL DEFAULT 0,
    AccessFailedCount INT NOT NULL DEFAULT 0;
GO

-- Copiar NroDocumento a UserName
UPDATE Usuarios SET UserName = NroDocumento, NormalizedUserName = UPPER(NroDocumento), NormalizedEmail = UPPER(Email);
GO

-- 4. Crear Tablas de Roles y Relaciones de Identity
CREATE TABLE Roles (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name NVARCHAR(256) NULL,
    NormalizedName NVARCHAR(256) NULL,
    ConcurrencyStamp NVARCHAR(MAX) NULL
);
GO

CREATE TABLE UsuarioRoles (
    UserId INT NOT NULL,
    RoleId INT NOT NULL,
    PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_UsuarioRoles_Roles FOREIGN KEY (RoleId) REFERENCES Roles(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UsuarioRoles_Usuarios FOREIGN KEY (UserId) REFERENCES Usuarios(Id) ON DELETE CASCADE
);
GO

CREATE TABLE UsuarioClaims (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UserId INT NOT NULL,
    ClaimType NVARCHAR(MAX) NULL,
    ClaimValue NVARCHAR(MAX) NULL,
    CONSTRAINT FK_UsuarioClaims_Usuarios FOREIGN KEY (UserId) REFERENCES Usuarios(Id) ON DELETE CASCADE
);
GO

CREATE TABLE UsuarioLogins (
    LoginProvider NVARCHAR(128) NOT NULL,
    ProviderKey NVARCHAR(128) NOT NULL,
    ProviderDisplayName NVARCHAR(MAX) NULL,
    UserId INT NOT NULL,
    PRIMARY KEY (LoginProvider, ProviderKey),
    CONSTRAINT FK_UsuarioLogins_Usuarios FOREIGN KEY (UserId) REFERENCES Usuarios(Id) ON DELETE CASCADE
);
GO

CREATE TABLE RoleClaims (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    RoleId INT NOT NULL,
    ClaimType NVARCHAR(MAX) NULL,
    ClaimValue NVARCHAR(MAX) NULL,
    CONSTRAINT FK_RoleClaims_Roles FOREIGN KEY (RoleId) REFERENCES Roles(Id) ON DELETE CASCADE
);
GO

CREATE TABLE UsuarioTokens (
    UserId INT NOT NULL,
    LoginProvider NVARCHAR(128) NOT NULL,
    Name NVARCHAR(128) NOT NULL,
    Value NVARCHAR(MAX) NULL,
    PRIMARY KEY (UserId, LoginProvider, Name),
    CONSTRAINT FK_UsuarioTokens_Usuarios FOREIGN KEY (UserId) REFERENCES Usuarios(Id) ON DELETE CASCADE
);
GO

PRINT 'Esquema de Identity actualizado correctamente en la base de datos DB_FondoXYZ.';
GO
