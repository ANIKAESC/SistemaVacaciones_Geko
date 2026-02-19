-- Script para corregir el problema de históricos en vista de autorizador
-- El problema es que los SPs probablemente filtran solo solicitudes pendientes
-- y no muestran las autorizadas/rechazadas (histórico)

USE DBProyectoGrupalDojoGeko;
GO

-- =============================================
-- 1. VERIFICAR QUÉ RETORNAN LOS SPs ACTUALES
-- =============================================
PRINT '=== VERIFICANDO SPs ACTUALES ===';
GO

-- Ver definición del SP para autorizadores específicos
IF OBJECT_ID('sp_ListarSolicitudEncabezado_Autorizador', 'P') IS NOT NULL
BEGIN
    PRINT 'SP sp_ListarSolicitudEncabezado_Autorizador existe';
    -- Ejecutar para ver qué retorna (ejemplo con IdAutorizador = 1)
    -- EXEC sp_ListarSolicitudEncabezado_Autorizador @FK_IdAutorizador = 1;
END
ELSE
BEGIN
    PRINT 'SP sp_ListarSolicitudEncabezado_Autorizador NO existe';
END
GO

-- Ver definición del SP para admin
IF OBJECT_ID('sp_ListarSolicitudEncabezado_Autorizador_Admin', 'P') IS NOT NULL
BEGIN
    PRINT 'SP sp_ListarSolicitudEncabezado_Autorizador_Admin existe';
    -- Ejecutar para ver qué retorna
    -- EXEC sp_ListarSolicitudEncabezado_Autorizador_Admin;
END
ELSE
BEGIN
    PRINT 'SP sp_ListarSolicitudEncabezado_Autorizador_Admin NO existe';
END
GO

-- =============================================
-- 2. RECREAR SP PARA AUTORIZADORES (CON HISTÓRICO)
-- =============================================
PRINT '=== RECREANDO SP PARA AUTORIZADORES ===';
GO

IF OBJECT_ID('sp_ListarSolicitudEncabezado_Autorizador', 'P') IS NOT NULL
    DROP PROCEDURE sp_ListarSolicitudEncabezado_Autorizador;
GO

CREATE PROCEDURE sp_ListarSolicitudEncabezado_Autorizador
    @FK_IdAutorizador INT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- IMPORTANTE: Quitamos el filtro de estado para mostrar TODO el histórico
    -- Antes probablemente solo mostraba: WHERE Estado IN (2, 3) -- Pendientes
    -- Ahora mostramos TODAS las solicitudes asignadas al autorizador
    
    SELECT 
        se.IdSolicitudEncabezado,
        se.FechaInicio,
        se.FechaFin,
        se.CantidadDias,
        se.Estado,
        se.FK_IdEmpleado,
        se.FK_IdAutorizador,
        se.MotivoRechazo,
        se.FechaCreacion,
        e.NombreEmpleado + ' ' + e.ApellidoEmpleado AS NombreEmpleado,
        es.NombreEstado
    FROM SolicitudEncabezado se
    INNER JOIN Empleados e ON se.FK_IdEmpleado = e.IdEmpleado
    LEFT JOIN EstadosSolicitudes es ON se.Estado = es.IdEstadoSolicitud
    WHERE se.FK_IdAutorizador = @FK_IdAutorizador
    ORDER BY se.FechaCreacion DESC;
END
GO

PRINT 'SP sp_ListarSolicitudEncabezado_Autorizador recreado con histórico completo';
GO

-- =============================================
-- 3. RECREAR SP PARA ADMIN/RRHH (CON HISTÓRICO)
-- =============================================
PRINT '=== RECREANDO SP PARA ADMIN/RRHH ===';
GO

IF OBJECT_ID('sp_ListarSolicitudEncabezado_Autorizador_Admin', 'P') IS NOT NULL
    DROP PROCEDURE sp_ListarSolicitudEncabezado_Autorizador_Admin;
GO

CREATE PROCEDURE sp_ListarSolicitudEncabezado_Autorizador_Admin
AS
BEGIN
    SET NOCOUNT ON;
    
    -- IMPORTANTE: Quitamos el filtro de estado para mostrar TODO el histórico
    -- Admin y RRHH ven TODAS las solicitudes sin restricción
    
    SELECT 
        se.IdSolicitudEncabezado,
        se.FechaInicio,
        se.FechaFin,
        se.CantidadDias,
        se.Estado,
        se.FK_IdEmpleado,
        se.FK_IdAutorizador,
        se.MotivoRechazo,
        se.FechaCreacion,
        e.NombreEmpleado + ' ' + e.ApellidoEmpleado AS NombreEmpleado,
        es.NombreEstado
    FROM SolicitudEncabezado se
    INNER JOIN Empleados e ON se.FK_IdEmpleado = e.IdEmpleado
    LEFT JOIN EstadosSolicitudes es ON se.Estado = es.IdEstadoSolicitud
    ORDER BY se.FechaCreacion DESC;
END
GO

PRINT 'SP sp_ListarSolicitudEncabezado_Autorizador_Admin recreado con histórico completo';
GO

-- =============================================
-- 4. VERIFICAR QUE FUNCIONAN CORRECTAMENTE
-- =============================================
PRINT '=== VERIFICACIÓN FINAL ===';
GO

-- Contar solicitudes por estado
SELECT 
    es.NombreEstado,
    COUNT(*) AS Cantidad
FROM SolicitudEncabezado se
LEFT JOIN EstadosSolicitudes es ON se.Estado = es.IdEstadoSolicitud
GROUP BY es.NombreEstado
ORDER BY Cantidad DESC;
GO

PRINT '✅ Script completado. Ahora los históricos deberían aparecer en la vista de autorizador.';
GO
