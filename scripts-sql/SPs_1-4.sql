USE DB_FondoXYZ;
GO

-- ===================================================================================
-- SP 1: CONSULTAR DISPONIBILIDAD POR RANGO DE FECHAS
-- ===================================================================================
CREATE OR ALTER PROCEDURE sp_ConsultarDisponibilidadPorFechas
    @SedeId INT, 
    @FechaLlegada DATE, 
    @FechaSalida DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.Id, a.Nombre, a.NumeroHabitaciones, a.CapacidadMaxima,
        DATEDIFF(DAY, @FechaLlegada, @FechaSalida) AS NochesTotales,
        CASE WHEN NOT EXISTS (
            SELECT 1 FROM Reservas r 
            WHERE r.AlojamientoId = a.Id AND r.EstadoReserva IN ('Confirmada', 'Pendiente')
            AND (r.FechaLlegada < @FechaSalida AND r.FechaSalida > @FechaLlegada)
        ) THEN 1 ELSE 0 END AS Disponible
    FROM Alojamientos a
    WHERE a.SedeId = @SedeId 
    ORDER BY a.Nombre;
END
GO
PRINT 'SP 1 optimizado';
GO

-- ===================================================================================
-- SP 2: CONSULTAR DISPONIBILIDAD POR FECHAS Y NÚMERO DE PERSONAS
-- ===================================================================================
CREATE OR ALTER PROCEDURE sp_ConsultarDisponibilidadPorFechasYPersonas
    @SedeId INT, 
    @FechaLlegada DATE, 
    @FechaSalida DATE, 
    @CantidadPersonas INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.Id, a.Nombre, a.NumeroHabitaciones, a.CapacidadMaxima,
        DATEDIFF(DAY, @FechaLlegada, @FechaSalida) AS NochesTotales,
        CASE 
            WHEN EXISTS (
                SELECT 1 FROM Reservas r 
                WHERE r.AlojamientoId = a.Id AND r.EstadoReserva IN ('Confirmada', 'Pendiente')
                AND (r.FechaLlegada < @FechaSalida AND r.FechaSalida > @FechaLlegada)
            ) THEN 'Ocupado por fechas'
            WHEN a.CapacidadMaxima < @CantidadPersonas AND (@SedeId NOT BETWEEN 1 AND 5 OR @CantidadPersonas > 10) THEN 'Excede capacidad'
            ELSE 'Disponible' 
        END AS EstadoDisponibilidad
    FROM Alojamientos a
    WHERE a.SedeId = @SedeId
    ORDER BY 
        CASE 
            WHEN a.CapacidadMaxima >= @CantidadPersonas THEN 0 ELSE 1 
        END, a.CapacidadMaxima;
END
GO
PRINT 'SP 2 optimizado';
GO

-- ===================================================================================
-- SP 3: VER TARIFAS (FILTRADO INTELIGENTE POR UNIDAD)
-- ===================================================================================
CREATE OR ALTER PROCEDURE sp_VerTarifas
    @SedeId INT, 
    @AlojamientoId INT, 
    @CantidadPersonas INT, 
    @FechaConsulta DATE
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @EsAltaTemporada BIT = 0, @EsFestivo BIT = 0, @DiaSemana INT;
    DECLARE @NumHabitaciones INT, @AlojNombre NVARCHAR(150);

    SET DATEFIRST 1;
    SET @DiaSemana = DATEPART(dw, @FechaConsulta);
    
    IF EXISTS(SELECT 1 FROM Festivos WHERE Fecha = @FechaConsulta) SET @EsFestivo = 1;
    IF EXISTS(SELECT 1 FROM Temporadas WHERE @FechaConsulta BETWEEN FechaInicio AND FechaFin AND EsAltaTemporada = 1 AND Activa = 1) SET @EsAltaTemporada = 1;
    
    SELECT @NumHabitaciones = NumeroHabitaciones, @AlojNombre = Nombre FROM Alojamientos WHERE Id = @AlojamientoId;
    DECLARE @AptoNum NVARCHAR(10) = RIGHT(@AlojNombre, 3);

    SELECT t.Id, s.Nombre AS Sede, a.Nombre AS Alojamiento, t.Descripcion, t.ValorBase, t.CapacidadBase, t.ValorPersonaAdicional,
        CASE WHEN @CantidadPersonas > t.CapacidadBase THEN (@CantidadPersonas - t.CapacidadBase) * t.ValorPersonaAdicional ELSE 0 END AS CostoPersonasAdicionales,
        @EsAltaTemporada AS EsAltaTemporada, @EsFestivo AS EsFestivo, @DiaSemana AS DiaSemana
    FROM Tarifas t
    INNER JOIN Sedes s ON t.SedeId = s.Id
    INNER JOIN Alojamientos a ON a.Id = @AlojamientoId
    WHERE t.SedeId = @SedeId 
      AND (
            (@SedeId BETWEEN 1 AND 5 AND t.Descripcion LIKE '%(' + CAST(@NumHabitaciones AS NVARCHAR) + ')%')
            OR (@SedeId = 6)
            OR (@SedeId = 7 AND ((@CantidadPersonas = 1 AND t.Descripcion LIKE '%1 Persona%') OR (@CantidadPersonas > 1 AND t.Descripcion LIKE '%2 Personas%')))
            OR (@SedeId = 8 AND t.Descripcion LIKE '%' + @AptoNum + '%')
      )
    ORDER BY t.ValorBase;
END
GO
PRINT 'SP 3 optimizado con segmentación estricta';
GO

-- ===================================================================================
-- SP 4: CALCULAR COSTO DE RESERVA (LIQUIDACIÓN COMPLETA CORREGIDA)
-- ===================================================================================
CREATE OR ALTER PROCEDURE sp_CalcularCostoReserva
    @SedeId INT,
    @AlojamientoId INT,
    @FechaLlegada DATETIME,
    @FechaSalida DATETIME,
    @CantidadPersonas INT,
    @CantidadVisitantes INT = 0,
    @IncluyeLavanderia BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @FechaActual DATE = @FechaLlegada, @DiaSemana INT, @TotalBase DECIMAL(18,2) = 0, @TotalAdicional DECIMAL(18,2) = 0;
    DECLARE @GranTotal DECIMAL(18,2) = 0, @Noches INT = 0, @ValBase DECIMAL(18,2) = 0, @CapBase INT = 0;
    DECLARE @ValPersonaAdic DECIMAL(18,2) = 0, @EsAltaTemporada BIT = 0, @EsFestivo BIT = 0, @CostoLavanderia DECIMAL(18,2) = 0;
    
    DECLARE @NumHabitaciones INT, @AlojamientoNombre NVARCHAR(150);
    
    SET DATEFIRST 1;
    
    -- Extraer propiedades del alojamiento
    SELECT @NumHabitaciones = NumeroHabitaciones, @AlojamientoNombre = Nombre 
    FROM Alojamientos WHERE Id = @AlojamientoId AND SedeId = @SedeId;
    
    -- Calcular costo de lavandería si fue solicitada
    IF @IncluyeLavanderia = 1 AND @SedeId = 8 SET @CostoLavanderia = 18000;
    
    -- Procesamiento día por día del rango de fechas
    WHILE @FechaActual < @FechaSalida
    BEGIN
        SET @Noches = @Noches + 1; 
        SET @DiaSemana = DATEPART(dw, @FechaActual);
        SET @ValBase = 0; SET @CapBase = 0; SET @ValPersonaAdic = 0; SET @EsAltaTemporada = 0; SET @EsFestivo = 0;
        
        IF EXISTS(SELECT 1 FROM Festivos WHERE Fecha = @FechaActual) SET @EsFestivo = 1;
        IF EXISTS(SELECT 1 FROM Temporadas WHERE @FechaActual BETWEEN FechaInicio AND FechaFin AND EsAltaTemporada = 1 AND Activa = 1) SET @EsAltaTemporada = 1;
        
        -- Sedes recreativas (1 a 5)
        IF @SedeId BETWEEN 1 AND 5
        BEGIN
            IF @DiaSemana BETWEEN 1 AND 4 AND @EsAltaTemporada = 0 AND @EsFestivo = 0
                SELECT TOP 1 @ValBase = ValorBase, @CapBase = CapacidadBase, @ValPersonaAdic = ValorPersonaAdicional 
                FROM Tarifas 
                WHERE SedeId = @SedeId AND Temporada = 'Especial' AND Descripcion LIKE '%(' + CAST(@NumHabitaciones AS NVARCHAR) + ')%';
            ELSE
                SELECT TOP 1 @ValBase = ValorBase, @CapBase = CapacidadBase, @ValPersonaAdic = ValorPersonaAdicional 
                FROM Tarifas 
                WHERE SedeId = @SedeId AND Temporada = 'Ordinaria' AND Descripcion LIKE '%(' + CAST(@NumHabitaciones AS NVARCHAR) + ')%';
        END
        
        -- Sede Federman (6)
        ELSE IF @SedeId = 6
        BEGIN
            IF @DiaSemana BETWEEN 1 AND 4 AND @EsAltaTemporada = 0 AND @EsFestivo = 0
                SELECT TOP 1 @ValBase = ValorBase, @CapBase = CapacidadBase, @ValPersonaAdic = ValorPersonaAdicional FROM Tarifas WHERE SedeId = @SedeId AND Temporada = 'Especial' ORDER BY ValorBase;
            ELSE
                SELECT TOP 1 @ValBase = ValorBase, @CapBase = CapacidadBase, @ValPersonaAdic = ValorPersonaAdicional FROM Tarifas WHERE SedeId = @SedeId AND Temporada = 'Ordinaria' ORDER BY ValorBase;
        END
        
        -- Apartamentos Medellín (7)
        ELSE IF @SedeId = 7
        BEGIN
            IF @CantidadPersonas = 1 
                SELECT TOP 1 @ValBase = ValorBase, @CapBase = CapacidadBase, @ValPersonaAdic = 0 FROM Tarifas WHERE SedeId = 7 AND Descripcion LIKE '%1 Persona%';
            ELSE 
                SELECT TOP 1 @ValBase = ValorBase, @CapBase = CapacidadBase, @ValPersonaAdic = 0 FROM Tarifas WHERE SedeId = 7 AND Descripcion LIKE '%2 Personas%';
        END
        
        -- Apartamentos Santa Marta (8): tarifa asignada por número de apartamento
        ELSE IF @SedeId = 8
        BEGIN
            DECLARE @AptoNum NVARCHAR(10) = RIGHT(@AlojamientoNombre, 3);
            IF @EsAltaTemporada = 1
                SELECT TOP 1 @ValBase = ValorBase, @CapBase = CapacidadBase, @ValPersonaAdic = 0 FROM Tarifas WHERE SedeId = 8 AND Descripcion LIKE '%' + @AptoNum + '%Alta%';
            ELSE
                SELECT TOP 1 @ValBase = ValorBase, @CapBase = CapacidadBase, @ValPersonaAdic = 0 FROM Tarifas WHERE SedeId = 8 AND Descripcion LIKE '%' + @AptoNum + '%Baja%';
        END
        
        -- Sumar los valores por cada día
        SET @TotalBase = @TotalBase + @ValBase;
            IF @CantidadPersonas > @CapBase AND @ValPersonaAdic > 0
            BEGIN
                DECLARE @Cobrables INT = @CantidadPersonas - @CapBase;
                SET @TotalAdicional = @TotalAdicional + (@Cobrables * @ValPersonaAdic);
            END

            IF @CantidadVisitantes > 0 AND @SedeId BETWEEN 1 AND 5
            BEGIN
                SET @TotalAdicional = @TotalAdicional + (@CantidadVisitantes * 5500);
            END
        
        SET @FechaActual = DATEADD(day, 1, @FechaActual);
    END;
    
    SET @GranTotal = @TotalBase + @TotalAdicional + @CostoLavanderia;
    
    -- Devolver los resultados
    SELECT @Noches AS NochesTotales, 
           @TotalBase AS SubtotalTarifaBase, 
           @TotalAdicional AS TotalExcedentePersonas, 
           @CostoLavanderia AS CostoLavanderia, 
           @GranTotal AS CostoTotalEstadia;
END
GO
PRINT 'SP 4 optimizado con mapeo relacional estricto';
GO

-- ===================================================================================
-- SP 5: PREVIEW DE TARIFA — Muestra qué tarifa aplica según la fecha de llegada
-- Usado por la API REST para informar al usuario antes de confirmar la reserva
-- ===================================================================================
CREATE OR ALTER PROCEDURE sp_PreviewTarifa
    @SedeId INT,
    @AlojamientoId INT,
    @FechaLlegada DATE,
    @CantidadPersonas INT,
    @CantidadVisitantes INT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET DATEFIRST 1; -- Semana comienza en Lunes

    DECLARE @DiaSemana      INT;
    DECLARE @EsAltaTemporada BIT = 0;
    DECLARE @EsFestivo       BIT = 0;
    DECLARE @TipoTarifa      NVARCHAR(50);
    DECLARE @ValorBase       DECIMAL(18,2) = 0;
    DECLARE @CapBase         INT = 0;
    DECLARE @ValorAdicional  DECIMAL(18,2) = 0;
    DECLARE @NumHabitaciones INT;
    DECLARE @AlojNombre      NVARCHAR(150);

    SET @DiaSemana = DATEPART(dw, @FechaLlegada);

    -- Verificar si es festivo
    IF EXISTS(SELECT 1 FROM Festivos WHERE Fecha = @FechaLlegada)
        SET @EsFestivo = 1;

    -- Verificar si es alta temporada
    IF EXISTS(
        SELECT 1 FROM Temporadas
        WHERE @FechaLlegada BETWEEN FechaInicio AND FechaFin
          AND EsAltaTemporada = 1 AND Activa = 1
    )
        SET @EsAltaTemporada = 1;

    -- Obtener info del alojamiento
    SELECT @NumHabitaciones = NumeroHabitaciones, @AlojNombre = Nombre
    FROM Alojamientos
    WHERE Id = @AlojamientoId AND SedeId = @SedeId;

    -- -------------------------------------------------------
    -- SEDES RECREATIVAS (1 a 5): Especial L-J o Ordinaria
    -- Tarifa ESPECIAL: Lunes(1) a Jueves(4), sin festivo, sin alta temporada
    -- -------------------------------------------------------
    IF @SedeId BETWEEN 1 AND 5
    BEGIN
        IF @DiaSemana BETWEEN 1 AND 4 AND @EsAltaTemporada = 0 AND @EsFestivo = 0
        BEGIN
            SET @TipoTarifa = 'Especial';
            SELECT TOP 1
                @ValorBase      = ValorBase,
                @CapBase        = CapacidadBase,
                @ValorAdicional = ValorPersonaAdicional
            FROM Tarifas
            WHERE SedeId = @SedeId AND Temporada = 'Especial'
              AND Descripcion LIKE '%(' + CAST(@NumHabitaciones AS NVARCHAR) + ')%';
        END
        ELSE
        BEGIN
            SET @TipoTarifa = 'Ordinaria';
            SELECT TOP 1
                @ValorBase      = ValorBase,
                @CapBase        = CapacidadBase,
                @ValorAdicional = ValorPersonaAdicional
            FROM Tarifas
            WHERE SedeId = @SedeId AND Temporada = 'Ordinaria'
              AND Descripcion LIKE '%(' + CAST(@NumHabitaciones AS NVARCHAR) + ')%';
        END
    END

    -- -------------------------------------------------------
    -- FEDERMAN (6): misma lógica Especial/Ordinaria
    -- -------------------------------------------------------
    ELSE IF @SedeId = 6
    BEGIN
        IF @DiaSemana BETWEEN 1 AND 4 AND @EsAltaTemporada = 0 AND @EsFestivo = 0
        BEGIN
            SET @TipoTarifa = 'Especial';
            SELECT TOP 1 @ValorBase = ValorBase, @CapBase = CapacidadBase, @ValorAdicional = ValorPersonaAdicional
            FROM Tarifas WHERE SedeId = 6 AND Temporada = 'Especial' ORDER BY ValorBase;
        END
        ELSE
        BEGIN
            SET @TipoTarifa = 'Ordinaria';
            SELECT TOP 1 @ValorBase = ValorBase, @CapBase = CapacidadBase, @ValorAdicional = ValorPersonaAdicional
            FROM Tarifas WHERE SedeId = 6 AND Temporada = 'Ordinaria' ORDER BY ValorBase;
        END
    END

    -- -------------------------------------------------------
    -- MEDELLÍN (7): Por número de personas, sin variación de día
    -- -------------------------------------------------------
    ELSE IF @SedeId = 7
    BEGIN
        SET @TipoTarifa = 'Normal';
        IF @CantidadPersonas = 1
            SELECT TOP 1 @ValorBase = ValorBase, @CapBase = CapacidadBase, @ValorAdicional = 0
            FROM Tarifas WHERE SedeId = 7 AND Descripcion LIKE '%1 Persona%';
        ELSE
            SELECT TOP 1 @ValorBase = ValorBase, @CapBase = CapacidadBase, @ValorAdicional = 0
            FROM Tarifas WHERE SedeId = 7 AND Descripcion LIKE '%2 Personas%';
    END

    -- -------------------------------------------------------
    -- SANTA MARTA (8): Alta o Baja Temporada por apartamento
    -- -------------------------------------------------------
    ELSE IF @SedeId = 8
    BEGIN
        DECLARE @AptoNum NVARCHAR(10) = RIGHT(@AlojNombre, 3);
        IF @EsAltaTemporada = 1
        BEGIN
            SET @TipoTarifa = 'Alta Temporada';
            SELECT TOP 1 @ValorBase = ValorBase, @CapBase = CapacidadBase, @ValorAdicional = 0
            FROM Tarifas WHERE SedeId = 8 AND Descripcion LIKE '%' + @AptoNum + '%Alta%';
        END
        ELSE
        BEGIN
            SET @TipoTarifa = 'Baja Temporada';
            SELECT TOP 1 @ValorBase = ValorBase, @CapBase = CapacidadBase, @ValorAdicional = 0
            FROM Tarifas WHERE SedeId = 8 AND Descripcion LIKE '%' + @AptoNum + '%Baja%';
        END
    END

    -- Calcular el costo de personas adicionales para el preview
    DECLARE @CostoPersonasAdicionales DECIMAL(18,2) = 0;
    
    IF @CantidadPersonas > @CapBase
        SET @CostoPersonasAdicionales = (@CantidadPersonas - @CapBase) * @ValorAdicional;

    IF @CantidadVisitantes > 0 AND @SedeId BETWEEN 1 AND 5
        SET @CostoPersonasAdicionales = @CostoPersonasAdicionales + (@CantidadVisitantes * 5500);

    -- Retornar resultado
    SELECT
        @TipoTarifa          AS TipoTarifa,
        @ValorBase           AS ValorBaseNoche,
        @CapBase             AS CapacidadIncluida,
        @ValorAdicional      AS ValorPersonaAdicional,
        @CostoPersonasAdicionales AS CostoPersonasAdicionales,
        CASE WHEN @SedeId BETWEEN 1 AND 5 THEN 5500.00 ELSE 0.00 END AS ValorVisitanteDia,
        @EsAltaTemporada     AS EsAltaTemporada,
        @EsFestivo           AS EsFestivo,
        @DiaSemana           AS DiaSemana,
        -- Descripción amigable para la UI
        CASE
            WHEN @TipoTarifa = 'Especial'   THEN 'Tarifa Especial - Lunes a Jueves (fuera de festivos y alta temporada)'
            WHEN @TipoTarifa = 'Ordinaria'  THEN 'Tarifa Ordinaria - Fines de semana, festivos y alta temporada'
            WHEN @TipoTarifa = 'Alta Temporada' THEN 'Tarifa Alta Temporada - Periodo vacacional o festivo'
            WHEN @TipoTarifa = 'Baja Temporada' THEN 'Tarifa Baja Temporada - Periodo escolar normal'
            ELSE 'Tarifa Normal'
        END AS DescripcionTarifa;
END
GO
PRINT 'SP 5 (sp_PreviewTarifa) creado exitosamente';
GO