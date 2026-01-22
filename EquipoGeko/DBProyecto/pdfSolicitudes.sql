-- =============================================
-- Tabla para almacenar PDFs de solicitudes comprimidos con Brotli
-- =============================================

use DBProyectoGrupalDojoGeko; 

CREATE TABLE SolicitudPDF (
    IdSolicitudPDF INT IDENTITY(1,1) PRIMARY KEY,
    FK_IdSolicitud INT NOT NULL,
    NombreArchivo NVARCHAR(255) NOT NULL,
    ContenidoPDFComprimido VARBINARY(MAX) NOT NULL, -- PDF comprimido con Brotli
    TamanoOriginal BIGINT NOT NULL, -- Tamaño antes de comprimir
    TamanoComprimido BIGINT NOT NULL, -- Tamaño después de comprimir
    FechaCreacion DATETIME DEFAULT GETDATE(),
    FK_IdEstado INT DEFAULT 1, -- 1=Disponible, 4=Restringido
    
    CONSTRAINT FK_SolicitudPDF_Solicitud 
        FOREIGN KEY (FK_IdSolicitud) 
            REFERENCES SolicitudEncabezado(IdSolicitud) 
                ON DELETE CASCADE,
    CONSTRAINT FK_SolicitudPDF_Estado 
        FOREIGN KEY (FK_IdEstado) 
            REFERENCES Estados(IdEstado)
);
GO

-- SP para insertar PDF de solicitud
CREATE PROCEDURE sp_InsertarSolicitudPDF
    @FK_IdSolicitud INT,
    @NombreArchivo NVARCHAR(255),
    @ContenidoPDFComprimido VARBINARY(MAX),
    @TamanoOriginal BIGINT,
    @TamanoComprimido BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Eliminar PDF anterior si existe
    DELETE FROM SolicitudPDF WHERE FK_IdSolicitud = @FK_IdSolicitud;
    
    -- Insertar nuevo PDF
    INSERT INTO SolicitudPDF (
        FK_IdSolicitud, 
        NombreArchivo, 
        ContenidoPDFComprimido, 
        TamanoOriginal, 
        TamanoComprimido
    )
    VALUES (
        @FK_IdSolicitud, 
        @NombreArchivo, 
        @ContenidoPDFComprimido, 
        @TamanoOriginal, 
        @TamanoComprimido
    );
    
    SELECT SCOPE_IDENTITY() AS IdSolicitudPDF;
END;
GO

-- SP para obtener PDF de solicitud
CREATE PROCEDURE sp_ObtenerSolicitudPDF
    @FK_IdSolicitud INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        sp.IdSolicitudPDF,
        sp.FK_IdSolicitud,
        sp.NombreArchivo,
        sp.ContenidoPDFComprimido,
        sp.TamanoOriginal,
        sp.TamanoComprimido,
        sp.FechaCreacion,
        sp.FK_IdEstado,
        se.FK_IdEstadoSolicitud
    FROM SolicitudPDF sp
    INNER JOIN SolicitudEncabezado se ON sp.FK_IdSolicitud = se.IdSolicitud
    WHERE sp.FK_IdSolicitud = @FK_IdSolicitud;
END;
GO

-- SP para restringir descarga cuando se aprueba la solicitud
CREATE PROCEDURE sp_RestringirDescargaPDF
    @FK_IdSolicitud INT
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE SolicitudPDF 
    SET FK_IdEstado = 4 -- Estado restringido
    WHERE FK_IdSolicitud = @FK_IdSolicitud;
END;
GO


CREATE TABLE UserSignatures
(
    Id              INT IDENTITY(1,1) NOT NULL,
    UserId          NVARCHAR(128) NOT NULL,
    SignatureImage  VARBINARY(MAX) NOT NULL,
    MimeType        NVARCHAR(50) NOT NULL,   -- 'image/png' o 'image/jpeg'
    UpdatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_UserSignatures_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_UserSignatures PRIMARY KEY (Id),
    CONSTRAINT UQ_UserSignatures_UserId UNIQUE (UserId)
);
GO
CREATE OR ALTER PROCEDURE UserSignatures_Upsert
    @UserId         NVARCHAR(128),
    @SignatureImage VARBINARY(MAX),
    @MimeType       NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.UserSignatures WHERE UserId = @UserId)
    BEGIN
        UPDATE dbo.UserSignatures
        SET SignatureImage = @SignatureImage,
            MimeType = @MimeType,
            UpdatedAt = SYSUTCDATETIME()
        WHERE UserId = @UserId;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.UserSignatures (UserId, SignatureImage, MimeType)
        VALUES (@UserId, @SignatureImage, @MimeType);
    END
END;
GO
CREATE OR ALTER PROCEDURE UserSignatures_Get
    @UserId NVARCHAR(128)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT UserId, SignatureImage, MimeType, UpdatedAt
    FROM dbo.UserSignatures
    WHERE UserId = @UserId;
END;
GO


CREATE TABLE BoletasDisponibilidad
(
    Id                  INT IDENTITY(1,1) NOT NULL,
    CreatedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_BoletasDisponibilidad_CreatedAt DEFAULT (SYSUTCDATETIME()),

    -- Datos del documento
    Fecha               NVARCHAR(20)  NOT NULL,
    Nombre              NVARCHAR(200) NOT NULL,
    Puesto              NVARCHAR(200) NOT NULL,
    Departamento        NVARCHAR(200) NOT NULL,
    Periodo             NVARCHAR(300) NOT NULL,
    Observaciones       NVARCHAR(500) NULL,

    -- Participantes (quién firma)
    EmpleadoId      NVARCHAR(128) NOT NULL,
    AutorizadorId         NVARCHAR(128) NOT NULL,

    -- Estado de firma
    FirmadoEmpleado     BIT NOT NULL CONSTRAINT DF_BoletasDisponibilidad_FirmadoEmpleado DEFAULT (0),
    FirmadoAutorizador     BIT NOT NULL CONSTRAINT DF_BoletasDisponibilidad_FirmadoAutorizador DEFAULT (0),

    CONSTRAINT PK_BoletasDisponibilidad PRIMARY KEY (Id)
);
GO

CREATE INDEX IX_BoletasDisponibilidad_DirectorUserId ON dbo.BoletasDisponibilidad(EmpleadoId);
CREATE INDEX IX_BoletasDisponibilidad_SocioUserId ON dbo.BoletasDisponibilidad(AutorizadorId);
GO

CREATE OR ALTER PROCEDURE BoletasDisponibilidad_Crear
    @Fecha          NVARCHAR(20),
    @Nombre         NVARCHAR(200),
    @Puesto         NVARCHAR(200),
    @Departamento   NVARCHAR(200),
    @Periodo        NVARCHAR(300),
    @Observaciones  NVARCHAR(500) = NULL,
    @EmpleadoId  NVARCHAR(128),
    @AutorizadorId    NVARCHAR(128),
    @NewId          INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.BoletasDisponibilidad
    (Fecha, Nombre, Puesto, Departamento, Periodo, Observaciones, EmpleadoId, AutorizadorId)
    VALUES
    (@Fecha, @Nombre, @Puesto, @Departamento, @Periodo, @Observaciones, @EmpleadoId, @AutorizadorId);

    SET @NewId = SCOPE_IDENTITY();
END;
GO

CREATE OR ALTER PROCEDURE BoletasDisponibilidad_MarcarFirmado
    @Id     INT,
    @UserId NVARCHAR(128)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.BoletasDisponibilidad
    SET
        FirmadoEmpleado = CASE WHEN EmpleadoId = @UserId THEN 1 ELSE FirmadoEmpleado END,
        FirmadoAutorizador   = CASE WHEN AutorizadorId    = @UserId THEN 1 ELSE FirmadoAutorizador END
    WHERE Id = @Id;
END;
GO


-- Comentario sobre los valores:
-- 1 = Formato GDG (Solicitud de Días de Disponibilidad)
-- 2 = Formato Digital Geko Corp (Solicitud de Días de Vacaciones)


-- Ver los últimos logs de error relacionados con PDF
-- Ver todos los logs relacionados con PDF
--SELECT 
--    IdLog,
--    Accion,
--    Descripcion,
--    FechaEntrada,
--    Estado
--FROM Logs
--WHERE (Accion LIKE '%PDF%' OR Descripcion LIKE '%PDF%')
--   OR (Accion LIKE '%Error%' AND FechaEntrada > DATEADD(MINUTE, -30, GETDATE()))
--ORDER BY FechaEntrada DESC;

ALTER TABLE Logs
ALTER COLUMN Descripcion NVARCHAR(MAX)
-- ============================================
-- Actualizar sp_InsertarSolicitudEncabezado
-- ============================================

-- 1. Eliminar el stored procedure existente
IF OBJECT_ID('sp_InsertarSolicitudEncabezado', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE sp_InsertarSolicitudEncabezado;
    PRINT 'Stored procedure eliminado correctamente';
END
GO

-- 2. Crear el stored procedure corregido
CREATE PROCEDURE sp_InsertarSolicitudEncabezado
    @IdEmpleado INT,
    @NombresEmpleado NVARCHAR(100),
    @DiasSolicitadosTotal DECIMAL(10,2),
    @FechaIngresoSolicitud DATETIME,
    @SolicitudLider NVARCHAR(10),
    @Observaciones NVARCHAR(MAX) = NULL,
    @Estado INT,
    @DocumentoFirmado VARBINARY(MAX) = NULL,
    @DocumentoContentType NVARCHAR(100) = NULL,
    @TipoFormatoPdf INT = 1,
    @IdAutorizador INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    INSERT INTO SolicitudEncabezado (
        FK_IdEmpleado,
        NombresEmpleado,
        DiasSolicitadosTotal,
        FechaIngresoSolicitud,
        SolicitudLider,
        Observaciones,
        FK_IdEstadoSolicitud,
        DocumentoFirmado,
        DocumentoContentType,
        TipoFormatoPdf,
        FK_IdAutorizador
    )
    VALUES (
        @IdEmpleado,
        @NombresEmpleado,
        @DiasSolicitadosTotal,
        @FechaIngresoSolicitud,
        @SolicitudLider,
        @Observaciones,
        @Estado,
        @DocumentoFirmado,
        @DocumentoContentType,
        @TipoFormatoPdf,
        @IdAutorizador
    );
    
    -- Retornar el ID de la solicitud creada
    SELECT SCOPE_IDENTITY() AS IdSolicitud;
END
GO

-- 3. Verificar que se creó correctamente
SELECT 
    name AS StoredProcedure,
    modify_date AS FechaModificacion,
    'Actualizado correctamente' AS Estado
FROM sys.procedures 
WHERE name = 'sp_InsertarSolicitudEncabezado';
GO

PRINT 'Stored procedure actualizado exitosamente!';


ALTER PROCEDURE sp_ListarSolicitudEncabezado_Autorizador
    @FK_IdAutorizador INT
AS
BEGIN
    SELECT
        se.IdSolicitud,
        se.FK_IdEmpleado,
        e.NombresEmpleado,
        se.DiasSolicitadosTotal,
        se.FechaIngresoSolicitud,
        se.FK_IdEstadoSolicitud
    FROM
        SolicitudEncabezado se
    INNER JOIN
        Empleados e ON se.FK_IdEmpleado = e.IdEmpleado
    WHERE
        se.FK_IdEstadoSolicitud = 1 -- Filtra por estado "Ingresada"
        AND se.FK_IdAutorizador = @FK_IdAutorizador -- Filtra por autorizador
END;




------------------------------------
--MODIFICAMOS EL SP PARA QUE LOS DÍAS ACUMULADOS SE ACTUALICEN
-- Modificar el procedimiento almacenado
ALTER PROCEDURE sp_AutorizarSolicitud
    @IdSolicitud INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @IdEmpleado INT;
    DECLARE @DiasSolicitados DECIMAL(10,2);
    DECLARE @EstadoActual INT;
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Obtener datos de la solicitud
        SELECT 
            @IdEmpleado = FK_IdEmpleado,
            @DiasSolicitados = DiasSolicitadosTotal,
            @EstadoActual = FK_IdEstadoSolicitud
        FROM SolicitudEncabezado
        WHERE IdSolicitud = @IdSolicitud;
        
        -- Verificar que la solicitud existe
        IF @IdEmpleado IS NULL
        BEGIN
            RAISERROR('La solicitud no existe', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- Verificar que la solicitud no esté ya autorizada (evitar descuentos duplicados)
        IF @EstadoActual >= 2
        BEGIN
            RAISERROR('La solicitud ya está autorizada', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- Actualizar el estado de la solicitud a Autorizada (2)
        UPDATE SolicitudEncabezado
        SET FK_IdEstadoSolicitud = 2,
            FechaAutorizacion = GETDATE()
        WHERE IdSolicitud = @IdSolicitud;
        
        -- DESCONTAR los días del empleado
        UPDATE Empleados
        SET DiasVacacionesAcumulados = DiasVacacionesAcumulados - @DiasSolicitados
        WHERE IdEmpleado = @IdEmpleado;
        
        COMMIT TRANSACTION;
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
    END CATCH
END;
GO

ALTER TABLE SolicitudEncabezado
ADD TipoFormatoPdf INT NOT NULL DEFAULT 1;
GO

-- Verificar que se agregó correctamente
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'SolicitudEncabezado'
  AND COLUMN_NAME = 'TipoFormatoPdf';
GO

PRINT 'Columna TipoFormatoPdf agregada exitosamente!';